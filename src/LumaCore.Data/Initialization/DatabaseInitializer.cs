// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Providers;
using LumaCore.Data.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Data.Initialization;

/// <summary>
/// Initializes the database on application startup.
/// </summary>
/// <remarks>
///     <para>
///     This hosted service runs during application startup and handles database initialization tasks in the following
///     order:
///     </para>
///     <list type="number">
///         <item>
///             <description>Apply pending migrations (with optional backup/restore)</description>
///         </item>
///         <item>
///             <description>Run integrity cleanup (remove orphaned conversations)</description>
///         </item>
///         <item>
///             <description>Seed default data (roles, etc.)</description>
///         </item>
///     </list>
///     <para>
///     <b>Configuration:</b> The following <see cref="DatabaseOptions"/> settings affect initialization behavior:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <b>Schema creation:</b> <see cref="DatabaseOptions.AutoCreate"/>
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Migrations:</b> <see cref="DatabaseOptions.AutoMigration"/>
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Cleanup:</b> <see cref="DatabaseOptions.CleanupConversationsWithNoUsersOnStartup"/>
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Recovery:</b> <see cref="DatabaseOptions.Recovery"/>
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Self-healing:</b> If initialization fails (e.g., database temporarily unavailable), the
///     <see cref="DatabaseConnectionMonitorService"/> will periodically retry the full initialization flow. When the
///     database becomes available again, initialization completes and API requests resume.
///     </para>
///     <para>
///     <b>Deployment note:</b> Automatic migrations can cause concurrency issues in scale-out scenarios where multiple
///     instances start simultaneously. Consider running migrations as a separate step in production.
///     </para>
/// </remarks>
public sealed partial class DatabaseInitializer : IHostedService
{
	private readonly DatabaseInitializationStatus mInitializationStatus;
	private readonly ILogger<DatabaseInitializer> mLogger;
	private readonly IOptions<DatabaseOptions>    mOptions;
	private readonly IServiceProvider             mServiceProvider;
	private readonly IShuttleReaderFactory        mShuttleReaderFactory;
	private readonly TimeProvider                 mTimeProvider;
	private readonly IDatabaseProviderOperations  mProviderOperations;

	/// <summary>
	/// Serializes concurrent calls to <see cref="RunInitializationCoreAsync"/>.
	/// </summary>
	/// <remarks>
	/// In the current architecture, concurrent execution is unlikely because <see cref="StartAsync"/> completes
	/// before <see cref="DatabaseConnectionMonitorService"/> starts, and the monitor loop is sequential. However,
	/// this gate makes the single-execution invariant explicit rather than relying on external scheduling guarantees.
	/// </remarks>
	private readonly SemaphoreSlim mInitializationGate = new(1, 1);

	/// <summary>
	/// Path to the most recent pre-migration backup created by <c>HandleUpdateMigrationsAsync()</c>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     When a migration fails and the database is restored, the recovery service retries initialization.
	///     Without this field, every retry would create a new (identical) backup of the unchanged database state,
	///     wasting time and disk space. By remembering the last backup path, subsequent retries reuse the
	///     existing backup instead of re-exporting the entire database.
	///     </para>
	///     <para>
	///     <b>Lifecycle:</b> Set after a successful backup creation, reused on retry if the file still exists,
	///     cleared after successful migration (the backup is no longer needed for rollback). Also cleared if the
	///     file has been deleted between retries (e.g., operator cleanup), which triggers a fresh backup.
	///     </para>
	///     <para>
	///     <b>Thread safety:</b> Only accessed inside <see cref="mInitializationGate"/>, so no additional
	///     synchronization is needed.
	///     </para>
	/// </remarks>
	private string? mLastBackupPath;

	/// <summary>
	/// Initializes a new instance of the <see cref="DatabaseInitializer"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for creating scoped services.</param>
	/// <param name="options">The database configuration options.</param>
	/// <param name="initializationStatus">The status tracker for database initialization.</param>
	/// <param name="shuttleReaderFactory">The factory for creating shuttle reader instances during backup/restore.</param>
	/// <param name="providerOperations">The provider-specific database operations (SQL dialect, error detection, DDL).</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <param name="logger">The logger instance.</param>
	public DatabaseInitializer(
		IServiceProvider             serviceProvider,
		IOptions<DatabaseOptions>    options,
		DatabaseInitializationStatus initializationStatus,
		IShuttleReaderFactory        shuttleReaderFactory,
		IDatabaseProviderOperations  providerOperations,
		TimeProvider                 timeProvider,
		ILogger<DatabaseInitializer> logger)
	{
		mServiceProvider = serviceProvider;
		mOptions = options;
		mInitializationStatus = initializationStatus;
		mShuttleReaderFactory = shuttleReaderFactory;
		mProviderOperations = providerOperations;
		mTimeProvider = timeProvider;
		mLogger = logger;
	}

	/// <summary>
	/// Starts the database initialization process.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	///     <para>
	///     Executes migrations, cleanup, and seeding. See the class-level documentation for the complete list of
	///     configuration options that affect initialization behavior.
	///     </para>
	///     <para>
	///     <b>Status tracking:</b> Sets <see cref="DatabaseInitializationStatus"/> to
	///     <see cref="DatabaseInitializationState.InProgress"/> at start, then
	///     <see cref="DatabaseInitializationState.Completed"/> on success or
	///     <see cref="DatabaseInitializationState.Failed"/> on failure.
	///     Failed initialization allows health endpoints to report the issue while database-dependent requests are
	///     rejected by <c>DatabaseNotReadyMiddleware</c> which is part of the <c>LumaCore.Api</c> project.
	///     </para>
	/// </remarks>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		mInitializationStatus.SetInProgress();

		try
		{
			DatabaseOptions options = mOptions.Value;

			mLogger.LogInformation(
				"Database initialization starting (Provider: {Provider}, AutoCreate: {AutoCreate}, AutoMigrate: {AutoMigrate})",
				options.Provider,
				options.AutoCreate,
				options.AutoMigration.Enabled);

			mLogger.LogInformation(
				"Database auto-migration settings (Enabled: {Enabled}, BackupBeforeMigration: {BackupBeforeMigration}, RestoreOnFailure: {RestoreOnFailure}, BackupRetentionDays: {BackupRetentionDays})",
				options.AutoMigration.Enabled,
				options.AutoMigration.CreateBackupBeforeMigration,
				options.AutoMigration.RestoreOnFailure,
				options.AutoMigration.BackupRetentionDays);

			if (options.PreferCompiledHotPathQueries)
			{
				mLogger.LogInformation(
					"Database option 'Database.PreferCompiledHotPathQueries' is enabled — selected read hot paths may use EF Core compiled queries for lower overhead (trade-off: compiled queries do not accept CancellationToken, so cancellation is best-effort only)");
			}

			await RunInitializationCoreAsync(cancellationToken).ConfigureAwait(false);
			mInitializationStatus.SetCompleted();
		}
		catch (DatabaseInitializationException ex)
		{
			// Categorized initialization failure — use the embedded category and message.
			mLogger.LogCritical(
				ex,
				"Database initialization failed ({FailureCategory}) — the application will continue running but database-dependent requests will be rejected: {FailureMessage}",
				ex.Category,
				ex.Message);

			// Unwrap: DatabaseInitializationException is a transport carrier for (category,
			// message, cause) from HandleUpdateMigrationsAsync(). Category and message go to
			// separate status properties; FailureException gets the unwrapped cause:
			// - InnerException exists: external error or AggregateException (double failure)
			// - InnerException null: the DIE itself IS the cause (config guard, etc.)
			// See DatabaseInitializationStatus.FailureException remarks for full contract.
			mInitializationStatus.SetFailed(ex.InnerException ?? ex, ex.Message, ex.Category);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Uncategorized exception — treat as transient (could be connection issue, etc.).
			mLogger.LogCritical(
				ex,
				"Database initialization failed — the application will continue running but database-dependent requests will be rejected, check the database connection and configuration");

			mInitializationStatus.SetFailed(
				ex,
				"Database initialization failed. See logs for details.",
				DatabaseFailureCategory.Transient);
		}
	}

	/// <summary>
	/// Runs the core database initialization logic (migrations, cleanup, seeding).
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="DatabaseInitializationException">
	/// Initialization fails with a categorized reason (e.g., disabled auto-create/auto-migration,
	/// backup failure, migration failure, or a completed interrupted restore requiring restart).
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// A restore checkpoint references a missing backup file, contains an unrecognized phase,
	/// or the Shuttle backup lacks migration history.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method is called by <see cref="StartAsync"/> during application startup and by
	///     <see cref="DatabaseConnectionMonitorService"/> during recovery. Both paths execute the same initialization
	///     logic to ensure consistent database state. See the class-level documentation for configuration options.
	///     </para>
	///     <para>
	///     <b>Status management:</b> This method does not modify <see cref="DatabaseInitializationStatus"/>. The caller
	///     is responsible for setting <see cref="DatabaseInitializationState.InProgress"/> before calling, and
	///     <see cref="DatabaseInitializationState.Completed"/> or <see cref="DatabaseInitializationState.Failed"/> after.
	///     </para>
	///     <para>
	///     <b>Concurrency:</b> Concurrent calls are serialized via an internal semaphore. If a second caller invokes
	///     this method while the first is still running, it will wait until the first completes before proceeding.
	///     </para>
	/// </remarks>
	internal async Task RunInitializationCoreAsync(CancellationToken cancellationToken)
	{
		await mInitializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await RunInitializationCoreUnsynchronizedAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			mInitializationGate.Release();
		}
	}

	/// <summary>
	/// Unsynchronized implementation of the core initialization logic. Called exclusively by
	/// <see cref="RunInitializationCoreAsync"/> which holds <see cref="mInitializationGate"/>.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	private async Task RunInitializationCoreUnsynchronizedAsync(CancellationToken cancellationToken)
	{
		DatabaseOptions options = mOptions.Value;

		// Create new scope to resolve scoped services (DbContext, DataService).
		AsyncServiceScope scope = mServiceProvider.CreateAsyncScope();
		try
		{
			// Resolve DbContext directly from the scope. This ensures we get a fresh instance with the correct
			// configuration for migrations and seeding.
			var dbContext = scope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();

			// Handle migrations. Migration failures propagate as exceptions.
			await HandleMigrationsAsync(dbContext, options, cancellationToken).ConfigureAwait(false);

			// Integrity cleanup: Remove conversations with no users if configured.
			if (options.CleanupConversationsWithNoUsersOnStartup)
			{
				var dataService = scope.ServiceProvider.GetRequiredService<ILumaCoreDataService>();
				int deleted = await dataService
					              .CleanupConversationsWithNoUsersAsync(cancellationToken)
					              .ConfigureAwait(false);

				if (deleted > 0)
				{
					mLogger.LogWarning(
						"Integrity cleanup removed {OrphanedConversationCount} conversation(s) with no user participants",
						deleted);
				}
				else
				{
					mLogger.LogInformation("Integrity cleanup check completed: no conversations without users found");
				}
			}

			// Seed default data. Seeding failures propagate as exceptions.
			await SeedDefaultDataAsync(
					dbContext,
					scope.ServiceProvider,
					cancellationToken)
				.ConfigureAwait(false);

			mLogger.LogInformation("Database initialization completed");
		}
		finally
		{
			await scope.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Stops the hosted service. No cleanup required.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
