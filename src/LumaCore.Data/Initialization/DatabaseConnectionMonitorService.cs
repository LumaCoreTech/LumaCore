// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Data.Initialization;

/// <summary>
/// Background service that monitors database connectivity and recovers from failures.
/// </summary>
/// <remarks>
///     <para>
///     This service handles two types of database unavailability:
///     </para>
///     <list type="number">
///         <item>
///             <description>
///             <b>Startup failure:</b> When <see cref="DatabaseInitializer"/> fails to complete initialization, this
///             service periodically retries the full initialization flow (migrations, cleanup, seeding) until it succeeds.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>Runtime disconnection:</b> When <see cref="DatabaseConnectionInterceptor"/> detects a connection
///             loss during normal operation, this service polls the database and runs initialization to ensure the
///             database is fully ready before resuming operations.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Retry behavior:</b> The service checks <see cref="DatabaseInitializationStatus.ShouldRetry"/> before each
///     recovery attempt. Retries are skipped when:
///     </para>
///     <list type="bullet">
///         <item><see cref="DatabaseFailureCategory.ConfigurationRequired"/>: Manual config change needed</item>
///         <item><see cref="DatabaseFailureCategory.ManualInterventionRequired"/>: Database is in unknown state</item>
///         <item>After <see cref="DatabaseInitializationStatus.MaxConsecutiveFailures"/> consecutive failures</item>
///     </list>
///     <para>
///     <b>Shared initialization logic:</b> This service delegates to
///     <see cref="DatabaseInitializer.RunInitializationCoreAsync"/>
///     to ensure the same initialization logic (including backup before migrations) is used for both startup and recovery.
///     </para>
///     <para>
///     <b>Configuration:</b> The polling interval is controlled by <see cref="DatabaseOptions.Recovery"/>.
///     The service can be disabled entirely by setting <c>Database:Recovery:Enabled</c> to <see langword="false"/>.
///     </para>
///     <para>
///     <b>Self-healing behavior:</b> Once the database becomes available and initialization completes, the service
///     updates <see cref="DatabaseInitializationStatus"/> and API requests resume normal processing.
///     </para>
/// </remarks>
public sealed class DatabaseConnectionMonitorService : BackgroundService
{
	private readonly DatabaseConnectionInterceptor             mConnectionInterceptor;
	private readonly DatabaseInitializer                       mDatabaseInitializer;
	private readonly DatabaseInitializationStatus              mInitializationStatus;
	private readonly ILogger<DatabaseConnectionMonitorService> mLogger;
	private readonly IOptions<DatabaseOptions>                 mOptions;
	private readonly IServiceProvider                          mServiceProvider;
	private readonly TimeProvider                              mTimeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="DatabaseConnectionMonitorService"/> class.
	/// </summary>
	/// <param name="databaseInitializer">The database initializer for running initialization logic.</param>
	/// <param name="initializationStatus">The database status tracker.</param>
	/// <param name="serviceProvider">The service provider for creating scoped services.</param>
	/// <param name="options">The database configuration options.</param>
	/// <param name="timeProvider">The time provider used for delay operations.</param>
	/// <param name="connectionInterceptor">
	/// The connection interceptor for resetting the failure counter after recovery.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	public DatabaseConnectionMonitorService(
		DatabaseInitializer                       databaseInitializer,
		DatabaseInitializationStatus              initializationStatus,
		IServiceProvider                          serviceProvider,
		IOptions<DatabaseOptions>                 options,
		TimeProvider                              timeProvider,
		DatabaseConnectionInterceptor             connectionInterceptor,
		ILogger<DatabaseConnectionMonitorService> logger)
	{
		mDatabaseInitializer = databaseInitializer;
		mInitializationStatus = initializationStatus;
		mServiceProvider = serviceProvider;
		mOptions = options;
		mTimeProvider = timeProvider;
		mConnectionInterceptor = connectionInterceptor;
		mLogger = logger;
	}

	/// <inheritdoc/>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		DatabaseOptions.RecoveryOptions recoveryOptions = mOptions.Value.Recovery;

		if (!recoveryOptions.Enabled)
		{
			mLogger.LogDebug("Database connection monitoring is disabled via configuration");
			return;
		}

		TimeSpan pollingInterval = TimeSpan.FromSeconds(recoveryOptions.PollingIntervalSeconds);

		mLogger.LogDebug(
			"Database connection monitor started (polling interval: {PollingIntervalSeconds}s when unhealthy)",
			recoveryOptions.PollingIntervalSeconds);

		while (!stoppingToken.IsCancellationRequested)
		{
			DatabaseInitializationState currentState = mInitializationStatus.State;

			// Only attempt recovery when in an unhealthy state.
			// When Completed, the polling loop just idles - actual failure detection
			// happens via DatabaseConnectionInterceptor which sets state to Disconnected.
			if (currentState is DatabaseInitializationState.Failed or DatabaseInitializationState.Disconnected)
			{
				// Check if recovery should be attempted based on failure category.
				if (!mInitializationStatus.ShouldRetry)
				{
					// Log once when we first give up, then stay quiet.
					if (mInitializationStatus.ConsecutiveFailureCount ==
					    DatabaseInitializationStatus.MaxConsecutiveFailures)
					{
						mLogger.LogCritical(
							"Database recovery has been disabled after {FailureCount} consecutive failures. " +
							"Manual intervention is required. Failure: {FailureMessage}",
							mInitializationStatus.ConsecutiveFailureCount,
							mInitializationStatus.FailureMessage);
					}
				}
				else
				{
					await TryRecoverAsync(stoppingToken).ConfigureAwait(false);
				}
			}

			try
			{
				await Task.Delay(pollingInterval, mTimeProvider, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}

		mLogger.LogDebug("Database connection monitor stopped");
	}

	/// <summary>
	/// Attempts to recover from a failed or disconnected database state by running the full initialization flow.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	///     <para>
	///     This method first checks database connectivity, then delegates to
	///     <see cref="DatabaseInitializer.RunInitializationCoreAsync"/> to run the same initialization logic
	///     used during startup. This ensures consistent behavior including:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>Backup creation before migrations (if configured)</description>
	///         </item>
	///         <item>
	///             <description>Automatic restore on migration failure (if configured)</description>
	///         </item>
	///         <item>
	///             <description>Integrity cleanup</description>
	///         </item>
	///         <item>
	///             <description>Data seeding</description>
	///         </item>
	///     </list>
	/// </remarks>
	private async Task TryRecoverAsync(CancellationToken cancellationToken)
	{
		mLogger.LogInformation("Attempting to recover database connection...");

		try
		{
			// First, check if we can connect at all before running full initialization.
			AsyncServiceScope scope = mServiceProvider.CreateAsyncScope();
			try
			{
				var dbContext = scope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();

				bool canConnect = await dbContext.Database
					                  .CanConnectAsync(cancellationToken)
					                  .ConfigureAwait(false);

				if (!canConnect)
				{
					mLogger.LogDebug(
						"Database still unreachable. Will retry in {PollingIntervalSeconds} seconds",
						mOptions.Value.Recovery.PollingIntervalSeconds);

					return;
				}

				mLogger.LogInformation("Database connection restored. Running initialization...");
				mInitializationStatus.SetInProgress();

				// Delegate to the shared initialization logic in DatabaseInitializer.
				// This ensures the same code path (including backup before migrations) is used.
				await mDatabaseInitializer.RunInitializationCoreAsync(cancellationToken).ConfigureAwait(false);

				// Reset the circuit breaker failure counter after successful recovery.
				mConnectionInterceptor.ResetFailureCounter();

				mInitializationStatus.SetCompleted();
				mLogger.LogInformation("Database recovery completed successfully");
			}
			finally
			{
				await scope.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			// Cancellation during recovery (typically app shutdown). Restore the pre-recovery
			// state so the status doesn't get stuck at InProgress if the process survives
			// (e.g., the cancellation came from an internal timeout, not the host stopping token).
			if (mInitializationStatus.State == DatabaseInitializationState.InProgress)
			{
				mInitializationStatus.SetFailed(
					new OperationCanceledException(),
					"Recovery was cancelled before completion.",
					DatabaseFailureCategory.Transient);
			}

			throw;
		}
		catch (DatabaseInitializationException ex)
		{
			// Categorized failure — use the embedded category and message.
			mLogger.LogWarning(
				ex,
				"Database recovery attempt failed ({FailureCategory}). {FailureMessage}",
				ex.Category,
				ex.Message);

			mInitializationStatus.SetFailed(ex.InnerException ?? ex, ex.Message, ex.Category);
		}
		catch (Exception ex)
		{
			// Uncategorized exception — treat as transient (connection issue, etc.).
			mLogger.LogWarning(
				ex,
				"Database recovery attempt failed. Will retry in {PollingIntervalSeconds} seconds",
				mOptions.Value.Recovery.PollingIntervalSeconds);

			mInitializationStatus.SetFailed(
				ex,
				"Database recovery failed. See logs for details.",
				DatabaseFailureCategory.Transient);
		}
	}
}
