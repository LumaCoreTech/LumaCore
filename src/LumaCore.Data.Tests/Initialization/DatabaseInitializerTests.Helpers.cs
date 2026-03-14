// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Data.Common;

using LumaCore.Core.Diagnostics;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;
using LumaCore.Data.Security;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace LumaCore.Data.Tests.Initialization;

// Shared test infrastructure: TestHarness, factory methods (CreateHarness, ResolveTestDatabase),
// and assertion helpers (AssertCompletedAsync, AssertOnlyFirstMigrationAppliedAsync, etc.).
public sealed partial class DatabaseInitializerTests
{
	/// <summary>
	/// The full migration ID for the first migration (<c>InitialCreate</c>).
	/// </summary>
	private const string FirstMigrationId = "20260126214435_InitialCreate";

	/// <summary>
	/// The full migration ID for the second migration (<c>AddAiPersonas</c>).
	/// Update when migrations change.
	/// </summary>
	private const string SecondMigrationId = "20260127000000_AddAiPersonas";

	/// <summary>
	/// All migration IDs in chronological order.
	/// Update this array when adding new migrations.
	/// </summary>
	// ReSharper disable once InconsistentNaming
	private static readonly string[] AllMigrationIds =
	[
		FirstMigrationId,
		SecondMigrationId
	];

	/// <summary>
	/// Encapsulates a fully wired <see cref="DatabaseInitializer"/> with a real database and all required
	/// services registered in DI. Disposing the harness tears down the service provider and cleans up the
	/// test database (file deletion for SQLite, <c>EnsureDeleted</c> for external providers).
	/// </summary>
	/// <remarks>
	///     <para>
	///     The database provider is determined by <see cref="DbTestSettingsLoader"/>. Locally, this defaults to
	///     SQLite file-based. In CI, PostgreSQL or SQL Server can be selected via
	///     <c>LUMACORE_TESTS__Db__Provider</c> and <c>LUMACORE_TESTS__Db__ConnectionString</c>.
	///     </para>
	///     <para>
	///     SQLite in-memory is not supported because <see cref="DatabaseInitializer"/> creates multiple scopes
	///     with independent <see cref="LumaCoreDbContext"/> instances; a shared in-memory connection would
	///     conflate them.
	///     </para>
	/// </remarks>
	private sealed class TestHarness : IAsyncDisposable
	{
		/// <summary>
		/// The system under test.
		/// </summary>
		public DatabaseInitializer Sut { get; }

		/// <summary>
		/// The shared status tracker — inspect after <see cref="DatabaseInitializer.StartAsync"/> to verify
		/// state transitions.
		/// </summary>
		public DatabaseInitializationStatus Status { get; }

		/// <summary>
		/// The <see cref="DatabaseOptions"/> used for this test run.
		/// Available for assertions that need to verify option-driven behavior.
		/// </summary>
		public DatabaseOptions Options { get; }

		/// <summary>
		/// Provider-specific operations for quoting identifiers and executing SQL.
		/// Exposed so tests can construct provider-agnostic SQL instead of hardcoding dialect-specific quoting.
		/// </summary>
		public IDatabaseProviderOperations ProviderOperations { get; }

		/// <summary>
		/// The fake time provider used by the <see cref="DatabaseInitializer"/> under test.
		/// Allows checkpoint tests to assert exact <c>StartedUtc</c> timestamps.
		/// </summary>
		public FakeTimeProvider TimeProvider { get; }

		/// <summary>
		/// The built service provider backing the initializer's scoped service resolution.
		/// </summary>
		private readonly ServiceProvider mServiceProvider;

		/// <summary>
		/// Absolute path to the temporary SQLite database file.
		/// <see langword="null"/> for external providers (PostgreSQL, SQL Server) where cleanup is
		/// handled via <see cref="DatabaseFacade.EnsureDeletedAsync"/>.
		/// </summary>
		private readonly string? mDatabasePath;

		/// <summary>
		/// Abstracts low-level database operations (delete rows, count rows, create tables) so that test
		/// helpers do not contain provider-specific SQL directly.
		/// </summary>
		private readonly IDatabaseTestOperations mTestOperations;

		/// <summary>
		/// Initializes a new instance of the <see cref="TestHarness"/> class.
		/// </summary>
		/// <param name="sut">The database initializer under test.</param>
		/// <param name="status">The initialization status tracker.</param>
		/// <param name="options">The database options used for this test.</param>
		/// <param name="serviceProvider">The service provider for cleanup.</param>
		/// <param name="databasePath">
		/// The path to the temporary SQLite database file, or <see langword="null"/> for external providers.
		/// </param>
		/// <param name="timeProvider">The fake time provider used by the initializer.</param>
		public TestHarness(
			DatabaseInitializer          sut,
			DatabaseInitializationStatus status,
			DatabaseOptions              options,
			ServiceProvider              serviceProvider,
			string?                      databasePath,
			FakeTimeProvider             timeProvider)
		{
			Sut = sut;
			Status = status;
			Options = options;
			ProviderOperations = serviceProvider.GetRequiredService<IDatabaseProviderOperations>();
			TimeProvider = timeProvider;
			mServiceProvider = serviceProvider;
			mDatabasePath = databasePath;
			mTestOperations = new RelationalDatabaseTestOperations(ProviderOperations);
		}

		/// <summary>
		/// Disposes the service provider (releasing all DB connections) and cleans up the test database.
		/// For SQLite, the temporary file is deleted. For external providers (PostgreSQL, SQL Server),
		/// the database is dropped via <see cref="DatabaseFacade.EnsureDeletedAsync"/>.
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			// For external providers, drop the test database before releasing connections.
			if (Options.Provider is not DatabaseProviders.Sqlite)
			{
				try
				{
					AsyncServiceScope scope = mServiceProvider.CreateAsyncScope();
					try
					{
						var dbContext = scope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();
						await dbContext.Database.EnsureDeletedAsync().ConfigureAwait(false);
					}
					finally
					{
						await scope.DisposeAsync().ConfigureAwait(false);
					}
				}
				catch
				{
					/* best-effort — CI runners may leave orphaned databases */
				}
			}

			await mServiceProvider.DisposeAsync().ConfigureAwait(false);

			// For SQLite file-based, delete the temporary database file.
			if (mDatabasePath is not null)
			{
				try { File.Delete(mDatabasePath); }
				catch
				{
					/* best-effort cleanup */
				}
			}
		}

		/// <summary>
		/// Creates a new scoped <see cref="LumaCoreDbContext"/> from the test service provider.
		/// </summary>
		/// <returns>
		/// A tuple of the scope (for disposal) and the scoped <see cref="LumaCoreDbContext"/>.
		/// The caller must dispose the scope after use.
		/// </returns>
		/// <remarks>
		/// Use this for checkpoint tests that need direct <see cref="LumaCoreDbContext"/> access without going
		/// through <see cref="DatabaseInitializer.StartAsync"/>.
		/// </remarks>
		public (AsyncServiceScope Scope, LumaCoreDbContext DbContext) CreateScopedDbContext()
		{
			AsyncServiceScope scope = mServiceProvider.CreateAsyncScope();
			var dbContext = scope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();
			return (scope, dbContext);
		}

		/// <summary>
		/// Applies only the first migration (<see cref="FirstMigrationId"/>) to the database, leaving all
		/// subsequent migrations as pending. This simulates the "existing database with pending migrations"
		/// scenario needed to exercise the
		/// <see cref="DatabaseInitializer"/>.<c>HandleUpdateMigrationsAsync</c> code path.
		/// </summary>
		/// <remarks>
		/// Uses <see cref="RelationalDatabaseFacadeExtensions.MigrateAsync(DatabaseFacade, string, CancellationToken)"/>
		/// with an explicit target migration name to apply only the first migration.
		/// </remarks>
		public async Task MigrateToFirstMigrationOnlyAsync()
		{
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = CreateScopedDbContext();
			try
			{
				await dbContext.Database.MigrateAsync(FirstMigrationId).ConfigureAwait(false);
			}
			finally
			{
				await scope.DisposeAsync().ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Deletes all rows from the specified table.
		/// </summary>
		/// <param name="dbContext">The database context whose connection is used.</param>
		/// <param name="tableName">The unquoted table name.</param>
		public async Task DeleteAllRowsAsync(LumaCoreDbContext dbContext, string tableName)
		{
			DbConnection connection = dbContext.Database.GetDbConnection();
			if (connection.State != ConnectionState.Open)
				await connection.OpenAsync().ConfigureAwait(false);

			await mTestOperations
				.DeleteAllRowsAsync(connection, tableName, CancellationToken.None)
				.ConfigureAwait(false);
		}

		/// <summary>
		/// Returns the number of rows in the specified table.
		/// </summary>
		/// <param name="dbContext">The database context whose connection is used.</param>
		/// <param name="tableName">The unquoted table name.</param>
		/// <returns>The row count.</returns>
		public async Task<long> CountRowsAsync(LumaCoreDbContext dbContext, string tableName)
		{
			DbConnection connection = dbContext.Database.GetDbConnection();
			if (connection.State != ConnectionState.Open)
				await connection.OpenAsync().ConfigureAwait(false);

			return await mTestOperations
				       .CountRowsAsync(connection, tableName, CancellationToken.None)
				       .ConfigureAwait(false);
		}

		/// <summary>
		/// Creates a minimal table that conflicts with a pending migration, causing <c>MigrateAsync</c>
		/// to fail with a "table already exists" error.
		/// </summary>
		/// <param name="dbContext">The database context whose connection is used.</param>
		/// <param name="tableName">The unquoted table name to create (e.g., <c>"Personas"</c>).</param>
		public async Task CreateConflictingTableAsync(LumaCoreDbContext dbContext, string tableName)
		{
			DbConnection connection = dbContext.Database.GetDbConnection();
			if (connection.State != ConnectionState.Open)
				await connection.OpenAsync().ConfigureAwait(false);

			await mTestOperations
				.CreateMinimalTableAsync(connection, tableName, CancellationToken.None)
				.ConfigureAwait(false);
		}

		/// <summary>
		/// Creates a real LumaCore Shuttle backup of the current database state using
		/// <see cref="IDatabaseMaintenanceService.CreateShuttleBackupAsync"/>.
		/// </summary>
		/// <returns>The full path to the created backup file.</returns>
		/// <remarks>
		/// The backup directory must be configured via <see cref="DatabaseOptions.AutoMigration"/>
		/// before calling this method. The caller is responsible for cleaning up the backup directory.
		/// </remarks>
		public async Task<string> CreateShuttleBackupAsync()
		{
			(AsyncServiceScope scope, LumaCoreDbContext _) = CreateScopedDbContext();
			try
			{
				var maintenanceService = scope.ServiceProvider
					.GetRequiredService<IDatabaseMaintenanceService>();
				return await maintenanceService
					       .CreateShuttleBackupAsync(CancellationToken.None)
					       .ConfigureAwait(false);
			}
			finally
			{
				await scope.DisposeAsync().ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Creates a minimal but valid LumaCore Shuttle file with no data tables, only the
		/// <see cref="SqliteShuttleSchema.BackupInfoTableName"/> metadata (including
		/// <see cref="SqliteShuttleSchema.CreatedUtcKey"/>). The file passes
		/// <see cref="SqliteShuttleReader.InitializeAsync"/> validation.
		/// </summary>
		/// <param name="filePath">The absolute path where the shuttle file will be created.</param>
		/// <param name="createdUtc">
		/// The creation timestamp to embed in the shuttle metadata. This is the value that
		/// <see cref="IShuttleReader.GetCreatedUtcAsync"/> will return.
		/// </param>
		public static async Task CreateMinimalShuttleFileAsync(string filePath, DateTimeOffset createdUtc)
		{
			var timeProvider = new FakeTimeProvider(createdUtc);
			var writer = new SqliteShuttleWriter(filePath, NullLogger.Instance, timeProvider);
			try
			{
				await writer.InitializeAsync().ConfigureAwait(false);
				await writer.FinalizeAsync().ConfigureAwait(false);
			}
			finally
			{
				await writer.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Asserts that the <see cref="DatabaseInitializationStatus"/> is in its initial default state
	/// (<see cref="DatabaseInitializationState.NotStarted"/>) with no failure information.
	/// Used to verify that utility methods (e.g., backup cleanup) do not accidentally modify the
	/// initialization status as a side effect. Verifies all 7 observable properties:
	/// </summary>
	/// <remarks>
	///     <list type="bullet">
	///         <item>
	///         <see cref="DatabaseInitializationStatus.State"/> equals
	///         <see cref="DatabaseInitializationState.NotStarted"/>
	///         </item>
	///         <item><see cref="DatabaseInitializationStatus.IsReady"/> is <see langword="false"/></item>
	///         <item><see cref="DatabaseInitializationStatus.FailureCategory"/> is <see langword="null"/></item>
	///         <item><see cref="DatabaseInitializationStatus.FailureException"/> is <see langword="null"/></item>
	///         <item><see cref="DatabaseInitializationStatus.FailureMessage"/> is <see langword="null"/></item>
	///         <item><see cref="DatabaseInitializationStatus.ConsecutiveFailureCount"/> is 0</item>
	///         <item><see cref="DatabaseInitializationStatus.ShouldRetry"/> is <see langword="false"/></item>
	///     </list>
	/// </remarks>
	/// <param name="status">The status instance to verify.</param>
	private static void AssertNotStartedStatus(DatabaseInitializationStatus status)
	{
		Assert.Equal(DatabaseInitializationState.NotStarted, status.State);
		Assert.False(status.IsReady);
		Assert.Null(status.FailureCategory);
		Assert.Null(status.FailureException);
		Assert.Null(status.FailureMessage);
		Assert.Equal(0, status.ConsecutiveFailureCount);
		Assert.False(status.ShouldRetry);
	}

	/// <summary>
	/// Asserts that the <see cref="DatabaseInitializationStatus"/> reflects a fully successful initialization
	/// with no residual failure state. Verifies all 7 observable properties:
	/// </summary>
	/// <remarks>
	///     <list type="bullet">
	///         <item>
	///         <see cref="DatabaseInitializationStatus.State"/> equals
	///         <see cref="DatabaseInitializationState.Completed"/>
	///         </item>
	///         <item><see cref="DatabaseInitializationStatus.IsReady"/> is <see langword="true"/></item>
	///         <item><see cref="DatabaseInitializationStatus.FailureCategory"/> is <see langword="null"/></item>
	///         <item><see cref="DatabaseInitializationStatus.FailureException"/> is <see langword="null"/></item>
	///         <item><see cref="DatabaseInitializationStatus.FailureMessage"/> is <see langword="null"/></item>
	///         <item><see cref="DatabaseInitializationStatus.ConsecutiveFailureCount"/> is 0</item>
	///         <item><see cref="DatabaseInitializationStatus.ShouldRetry"/> is <see langword="false"/></item>
	///     </list>
	/// </remarks>
	/// <param name="status">The status instance to verify.</param>
	private static void AssertCompletedStatus(DatabaseInitializationStatus status)
	{
		Assert.Equal(DatabaseInitializationState.Completed, status.State);
		Assert.True(status.IsReady);
		Assert.Null(status.FailureCategory);
		Assert.Null(status.FailureException);
		Assert.Null(status.FailureMessage);
		Assert.Equal(0, status.ConsecutiveFailureCount);
		Assert.False(status.ShouldRetry);
	}

	/// <summary>
	/// Asserts that the <see cref="DatabaseInitializationStatus"/> reflects an interrupted initialization
	/// where <see cref="OperationCanceledException"/> propagated before the status could transition to
	/// <see cref="DatabaseInitializationState.Completed"/> or
	/// <see cref="DatabaseInitializationState.Failed"/>. Verifies all 7 observable properties:
	/// </summary>
	/// <remarks>
	///     <list type="bullet">
	///         <item>
	///         <see cref="DatabaseInitializationStatus.State"/> equals
	///         <see cref="DatabaseInitializationState.InProgress"/>
	///         </item>
	///         <item><see cref="DatabaseInitializationStatus.IsReady"/> is <see langword="false"/></item>
	///         <item><see cref="DatabaseInitializationStatus.FailureCategory"/> is <see langword="null"/></item>
	///         <item><see cref="DatabaseInitializationStatus.FailureException"/> is <see langword="null"/></item>
	///         <item><see cref="DatabaseInitializationStatus.FailureMessage"/> is <see langword="null"/></item>
	///         <item><see cref="DatabaseInitializationStatus.ConsecutiveFailureCount"/> is 0</item>
	///         <item><see cref="DatabaseInitializationStatus.ShouldRetry"/> is <see langword="false"/></item>
	///     </list>
	/// </remarks>
	/// <param name="status">The status instance to verify.</param>
	private static void AssertInProgressStatus(DatabaseInitializationStatus status)
	{
		Assert.Equal(DatabaseInitializationState.InProgress, status.State);
		Assert.False(status.IsReady);
		Assert.Null(status.FailureCategory);
		Assert.Null(status.FailureException);
		Assert.Null(status.FailureMessage);
		Assert.Equal(0, status.ConsecutiveFailureCount);
		Assert.False(status.ShouldRetry);
	}

	/// <summary>
	/// Asserts the 4 mechanical framework properties of a <see cref="DatabaseInitializationState.Failed"/>
	/// status that are identical across all failure tests. The caller is responsible for asserting the
	/// content-specific properties (<see cref="DatabaseInitializationStatus.FailureException"/> and
	/// <see cref="DatabaseInitializationStatus.FailureMessage"/>) inline — those vary per test case.
	/// </summary>
	/// <remarks>
	///     <para>Verified properties:</para>
	///     <list type="bullet">
	///         <item>
	///         <see cref="DatabaseInitializationStatus.State"/> equals
	///         <see cref="DatabaseInitializationState.Failed"/>
	///         </item>
	///         <item><see cref="DatabaseInitializationStatus.IsReady"/> is <see langword="false"/></item>
	///         <item>
	///         <see cref="DatabaseInitializationStatus.FailureCategory"/> equals
	///         <paramref name="expectedCategory"/>
	///         </item>
	///         <item>
	///         <see cref="DatabaseInitializationStatus.ConsecutiveFailureCount"/> equals
	///         <paramref name="expectedConsecutiveFailures"/>
	///         </item>
	///         <item>
	///         <see cref="DatabaseInitializationStatus.ShouldRetry"/> equals <paramref name="expectedShouldRetry"/>
	///         </item>
	///     </list>
	/// </remarks>
	/// <param name="status">The status instance to verify.</param>
	/// <param name="expectedCategory">The expected failure category.</param>
	/// <param name="expectedConsecutiveFailures">The expected consecutive failure count (defaults to 1).</param>
	/// <param name="expectedShouldRetry">The expected retry flag.</param>
	private static void AssertFailedStatusCore(
		DatabaseInitializationStatus status,
		DatabaseFailureCategory      expectedCategory,
		int                          expectedConsecutiveFailures,
		bool                         expectedShouldRetry)
	{
		Assert.Equal(DatabaseInitializationState.Failed, status.State);
		Assert.False(status.IsReady);
		Assert.Equal(expectedCategory, status.FailureCategory);
		Assert.Equal(expectedConsecutiveFailures, status.ConsecutiveFailureCount);
		Assert.Equal(expectedShouldRetry, status.ShouldRetry);
	}

	/// <summary>
	/// Verifies that the database has all known migrations applied and no pending migrations remain.
	/// Creates a scoped <see cref="LumaCoreDbContext"/>, queries applied/pending migrations, and asserts
	/// that every entry in <see cref="AllMigrationIds"/> is present in the applied set.
	/// </summary>
	/// <param name="harness">The test harness providing scoped DB access.</param>
	private static async Task AssertAllMigrationsAppliedAsync(TestHarness harness)
	{
		(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
		try
		{
			List<string> applied = [..await dbContext.Database.GetAppliedMigrationsAsync()];
			Assert.Equal(AllMigrationIds.Order(), applied.Order());

			List<string> pending = [..await dbContext.Database.GetPendingMigrationsAsync()];
			Assert.Empty(pending);
		}
		finally
		{
			await scope.DisposeAsync();
		}
	}

	/// <summary>
	/// Asserts that the <see cref="DatabaseInitializer"/> completed successfully: the
	/// <see cref="DatabaseInitializationStatus"/> reflects a fully successful initialization (via
	/// <see cref="AssertCompletedStatus"/>), and the database has all known migrations applied with none
	/// pending (via <see cref="AssertAllMigrationsAppliedAsync"/>).
	/// </summary>
	/// <param name="harness">The test harness providing status and scoped DB access.</param>
	private static Task AssertCompletedAsync(TestHarness harness)
	{
		AssertCompletedStatus(harness.Status);
		return AssertAllMigrationsAppliedAsync(harness);
	}

	/// <summary>
	/// Verifies that only the first migration (<see cref="FirstMigrationId"/>) is applied to the database
	/// and no subsequent migrations were executed. Creates a scoped <see cref="LumaCoreDbContext"/> and
	/// queries the applied migrations list.
	/// </summary>
	/// <param name="harness">The test harness providing scoped DB access.</param>
	private static async Task AssertOnlyFirstMigrationAppliedAsync(TestHarness harness)
	{
		(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
		try
		{
			List<string> applied = [..await dbContext.Database.GetAppliedMigrationsAsync()];
			Assert.Single(applied);
			Assert.Equal(FirstMigrationId, applied[0]);
		}
		finally
		{
			await scope.DisposeAsync();
		}
	}

	/// <summary>
	/// Opens the specified shuttle backup file via <see cref="SqliteShuttleReaderFactory"/>, runs format
	/// validation (<see cref="IShuttleReader.InitializeAsync"/>), deep integrity check
	/// (<see cref="IShuttleReader.ValidateIntegrityAsync"/>), verifies that all required metadata keys are
	/// present (<see cref="SqliteShuttleSchema.ExportStatusKey"/>,
	/// <see cref="SqliteShuttleSchema.ShuttleFormatVersionKey"/>, <see cref="SqliteShuttleSchema.ShuttleIdKey"/>,
	/// <see cref="SqliteShuttleSchema.CreatedUtcKey"/>), and optionally checks migration history.
	/// </summary>
	/// <param name="filePath">Absolute path to the <c>.shuttle.sqlite</c> file.</param>
	/// <param name="expectedMigrationIds">
	/// Migration IDs that must be present in the shuttle's migration history. When <see langword="null"/>,
	/// the migration history check is skipped.
	/// </param>
	private static async Task AssertShuttleBackupIntegrityAsync(
		string                       filePath,
		IReadOnlyCollection<string>? expectedMigrationIds = null)
	{
		var factory = new SqliteShuttleReaderFactory(NullLogger<SqliteShuttleReader>.Instance);
		IShuttleReader reader = factory.Create(filePath);
		try
		{
			await reader.InitializeAsync().ConfigureAwait(false);
			await reader.ValidateIntegrityAsync().ConfigureAwait(false);

			// Verify all required metadata keys are present with expected values.
			// --------------------------------------------------------------------------------------------------------
			Dictionary<string, string> metadata = await reader.GetMetadataAsync().ConfigureAwait(false);

			// ExportStatus must be "Completed" for the shuttle to be considered valid.
			Assert.Equal(SqliteShuttleSchema.CompletedValue, metadata[SqliteShuttleSchema.ExportStatusKey]);

			// ShuttleFormatVersion must match the current version defined in SqliteShuttleSchema.
			Assert.Equal(
				SqliteShuttleSchema.CurrentShuttleFormatVersion.ToString(),
				metadata[SqliteShuttleSchema.ShuttleFormatVersionKey]);

			// ShuttleId must be a valid GUID.
			Assert.True(
				Guid.TryParse(metadata[SqliteShuttleSchema.ShuttleIdKey], out Guid _),
				$"ShuttleId is not a valid GUID: '{metadata[SqliteShuttleSchema.ShuttleIdKey]}'");

			// CreatedUtc must be present and parseable as a DateTimeOffset.
			DateTimeOffset? createdUtc = await reader.GetCreatedUtcAsync().ConfigureAwait(false);
			Assert.NotNull(createdUtc);

			// Optionally verify migration history contains the expected migration IDs.
			// This ensures the shuttle is not only structurally valid, but also contains the expected schema state.
			// --------------------------------------------------------------------------------------------------------
			if (expectedMigrationIds is not null)
			{
				List<MigrationInfo> history = await reader.GetMigrationHistoryAsync().ConfigureAwait(false);
				string[] actualIds = history.Select(m => m.MigrationId).Order().ToArray();
				string[] expectedIds = expectedMigrationIds.Order().ToArray();
				Assert.Equal(expectedIds, actualIds);
			}
		}
		finally
		{
			await reader.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Reads the <see cref="SqliteShuttleSchema.ShuttleIdKey"/> metadata from a shuttle backup file.
	/// </summary>
	/// <param name="filePath">Absolute path to the <c>.shuttle.sqlite</c> file.</param>
	/// <returns>The shuttle identity string (a GUID).</returns>
	private static async Task<string> ReadShuttleIdAsync(string filePath)
	{
		var factory = new SqliteShuttleReaderFactory(NullLogger<SqliteShuttleReader>.Instance);
		IShuttleReader reader = factory.Create(filePath);
		try
		{
			await reader.InitializeAsync().ConfigureAwait(false);
			Dictionary<string, string> metadata = await reader.GetMetadataAsync().ConfigureAwait(false);
			return metadata[SqliteShuttleSchema.ShuttleIdKey];
		}
		finally
		{
			await reader.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Resolves the database provider, connection string, and optional file path based on the test
	/// environment configuration loaded via <see cref="DbTestSettingsLoader"/>.
	/// </summary>
	/// <returns>
	/// A tuple of (provider name for <see cref="DatabaseOptions.Provider"/>,
	/// connection string, optional SQLite file path for cleanup).
	/// </returns>
	/// <remarks>
	///     <para>
	///     <see cref="DbProvider.SqliteInMemory"/> is treated as <see cref="DbProvider.Sqlite"/> because
	///     <see cref="DatabaseInitializer"/> creates multiple scopes with independent
	///     <see cref="LumaCoreDbContext"/> instances — an in-memory database would not be shared across scopes.
	///     </para>
	///     <para>
	///     For external providers (PostgreSQL, SQL Server), the configured connection string is modified to
	///     use a unique database name per test run to ensure isolation when tests run in parallel.
	///     </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// An external provider is selected but no connection string is configured.
	/// </exception>
	/// <exception cref="NotSupportedException">MySQL/MariaDB or an unknown provider is selected.</exception>
	private static (string ProviderName, string ConnectionString, string? DatabasePath) ResolveTestDatabase()
	{
		DbTestSettings settings = DbTestSettingsLoader.Load();

		switch (settings.Provider)
		{
			case DbProvider.SqliteInMemory:
			case DbProvider.Sqlite:
			{
				// Always use file-based SQLite — in-memory doesn't work for multi-scope tests.
				string dbPath = Path.Combine(Path.GetTempPath(), $"dbinit-test-{Guid.NewGuid():N}.db");
				return (DatabaseProviders.Sqlite, $"Data Source={dbPath}", dbPath);
			}

			case DbProvider.PostgreSql:
			case DbProvider.SqlServer:
			{
				if (string.IsNullOrWhiteSpace(settings.ConnectionString))
				{
					throw new InvalidOperationException(
						$"{settings.Provider} selected but no connection string configured " +
						"(set LUMACORE_TESTS__Db__ConnectionString).");
				}

				string providerName = settings.Provider == DbProvider.PostgreSql
					                      ? DatabaseProviders.PostgreSql
					                      : DatabaseProviders.SqlServer;

				// Use a unique database name per test run for isolation.
				var csBuilder = new DbConnectionStringBuilder { ConnectionString = settings.ConnectionString };
				string dbKey = csBuilder.ContainsKey("Initial Catalog") ? "Initial Catalog" : "Database";
				csBuilder[dbKey] = $"dbinit_test_{Guid.NewGuid():N}";
				return (providerName, csBuilder.ConnectionString, null);
			}

			case DbProvider.MySql:
			{
				if (string.IsNullOrWhiteSpace(settings.ConnectionString))
				{
					throw new InvalidOperationException(
						$"{settings.Provider} selected but no connection string configured " +
						"(set LUMACORE_TESTS__Db__ConnectionString).");
				}

				// Connection string and unique database name are ready — once Pomelo ships an
				// EF Core 10 compatible version, remove the throw below and add the UseMySql()
				// call in CreateHarness().
				// Track: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues
				var connectionStringBuilder = new DbConnectionStringBuilder
				{
					ConnectionString = settings.ConnectionString,
					["Database"] = $"dbinit_test_{Guid.NewGuid():N}"
				};

				throw new NotSupportedException(
					"MySQL/MariaDB support is temporarily unavailable. " +
					"Pomelo.EntityFrameworkCore.MySql has not yet released an EF Core 10 compatible version. " +
					"Track progress at: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues");

				// TODO: Uncomment when Pomelo releases EF Core 10 support:
				// return (DatabaseProviders.MySql, csBuilder.ConnectionString, null);
			}

			default:
				throw new NotSupportedException($"Unsupported database provider: {settings.Provider}");
		}
	}

	/// <summary>
	/// Builds a fully wired <see cref="TestHarness"/> with a fresh database determined by the test
	/// configuration (defaults to SQLite file-based; CI may use PostgreSQL or SQL Server).
	/// </summary>
	/// <param name="configure">
	/// Optional callback to override <see cref="DatabaseOptions"/> defaults. The defaults are configured for the
	/// happy path: <c>AutoCreate = true</c>, <c>AutoMigration.Enabled = true</c>, cleanup enabled.
	/// </param>
	/// <param name="configureServices">
	/// Optional callback to override DI service registrations before the container is built. Use this to replace
	/// default services (e.g., <see cref="IDatabaseMaintenanceService"/>) for isolated testing.
	/// </param>
	/// <returns>A disposable harness containing the SUT and all test infrastructure.</returns>
	/// <remarks>
	///     <para>
	///     The service collection mirrors the production <see cref="ServiceRegistration.AddLumaCoreData"/> registration
	///     but with minimal overrides: silent logging, and no hosted service wiring (the test calls
	///     <see cref="DatabaseInitializer.StartAsync"/> explicitly).
	///     </para>
	///     <para>
	///         <b>Registered services:</b>
	///     </para>
	///     <list type="bullet">
	///         <item><see cref="LumaCoreDbContext"/> — scoped, provider from test settings with migrations assembly</item>
	///         <item><see cref="ILumaCoreDataService"/> / <see cref="LumaCoreDataService"/> — scoped</item>
	///         <item><see cref="ISecretProtector"/> / <see cref="AesGcmSecretProtector"/> — singleton</item>
	///         <item>
	///         <see cref="IShuttleReaderFactory"/> / <see cref="SqliteShuttleReaderFactory"/> — singleton (Shuttle
	///         format is always SQLite)
	///         </item>
	///         <item><see cref="DatabaseInitializationStatus"/> — singleton (shared with SUT)</item>
	///         <item>
	///         <see cref="TimeProvider"/> — singleton, <see cref="FakeTimeProvider"/> (deterministic timestamps
	///         for checkpoint and backup-age assertions; also exposed via <see cref="TestHarness.TimeProvider"/>)
	///         </item>
	///         <item><see cref="ILoggerFactory"/> — singleton, silent (cleared providers)</item>
	///     </list>
	/// </remarks>
	private static TestHarness CreateHarness(
		Action<DatabaseOptions>?    configure         = null,
		Action<IServiceCollection>? configureServices = null)
	{
		(string providerName, string connectionString, string? databasePath) = ResolveTestDatabase();

		var options = new DatabaseOptions
		{
			Provider = providerName,
			ConnectionString = connectionString,
			AutoCreate = true,
			EncryptionKey = "DEV-ONLY-CHANGE-THIS-TO-A-LONG-RANDOM-SECRET-STRING",
			CleanupConversationsWithNoUsersOnStartup = true
		};
		configure?.Invoke(options);

		IOptions<DatabaseOptions> wrappedOptions = Options.Create(options);
		var status = new DatabaseInitializationStatus();

		// Build a service collection that mirrors production DI (minus hosted services and interceptor).
		var services = new ServiceCollection();

		// Logging — clear providers for silent tests, but register the full logging infrastructure
		// so ILogger<T> resolves correctly from DI (needed by SeedExecutor, DefaultRolesSeed, etc.).
		services.AddLogging(builder => builder.ClearProviders());

		// Options — singleton so all scoped services see the same configuration.
		services.AddSingleton(wrappedOptions);

		// EF Core — register the provider determined by test settings.
		// Downgrade PendingModelChangesWarning from exception to log entry: the model may have evolved
		// beyond the last-generated migration (per repo convention, all changes are folded into the
		// initial migration before release).
		string migrationsAssembly = typeof(LumaCoreDbContext).Assembly.FullName!;
		services.AddDbContext<LumaCoreDbContext>(dbOpts =>
		{
			switch (options.Provider)
			{
				case DatabaseProviders.Sqlite:
					dbOpts.UseSqlite(
						options.ConnectionString,
						o => o.MigrationsAssembly(migrationsAssembly));
					break;

				case DatabaseProviders.PostgreSql:
					dbOpts.UseNpgsql(
						options.ConnectionString,
						o => o.MigrationsAssembly(migrationsAssembly));
					break;

				case DatabaseProviders.SqlServer:
					dbOpts.UseSqlServer(
						options.ConnectionString,
						o => o.MigrationsAssembly(migrationsAssembly));
					break;

				// TODO: Re-enable when Pomelo releases EF Core 10 compatible version.
				// Track: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues
				// case DatabaseProviders.MySql:
				//     dbOpts.UseMySql(
				//         options.ConnectionString,
				//         ServerVersion.AutoDetect(options.ConnectionString),
				//         o => o.MigrationsAssembly(migrationsAssembly));
				//     break;
			}

			dbOpts.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
		});

		// Data service + dependencies (matches production ServiceRegistration).
		services.AddSingleton<ISecretProtector>(sp =>
			new AesGcmSecretProtector(sp.GetRequiredService<IOptions<DatabaseOptions>>()));
		services.AddScoped<ILumaCoreDataService, LumaCoreDataService>();

		var fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
		services.AddSingleton<TimeProvider>(fakeTimeProvider);

		// Provider operations + DataPort + Maintenance service — required for backup/restore tests.
		services.AddSingleton<IDatabaseProviderOperations>(DatabaseProviderFactory.GetProvider(options.Provider));
		services.AddScoped<DataPortService>();
		services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();

		// Shuttle reader factory — always SQLite because the Shuttle format is SQLite by design.
		services.AddSingleton<IShuttleReaderFactory, SqliteShuttleReaderFactory>();

		// Status tracker — same instance passed to the SUT.
		services.AddSingleton(status);

		// Allow tests to override service registrations (e.g., replace IDatabaseMaintenanceService with a stub).
		configureServices?.Invoke(services);

		ServiceProvider serviceProvider = services.BuildServiceProvider();

		var sut = new DatabaseInitializer(
			serviceProvider,
			wrappedOptions,
			status,
			serviceProvider.GetRequiredService<IShuttleReaderFactory>(),
			serviceProvider.GetRequiredService<IDatabaseProviderOperations>(),
			fakeTimeProvider,
			NullLogger<DatabaseInitializer>.Instance);

		return new TestHarness(sut, status, options, serviceProvider, databasePath, fakeTimeProvider);
	}

	/// <summary>
	/// Test-only <see cref="IShuttleReaderFactory"/> that throws <see cref="OperationCanceledException"/>
	/// on every <see cref="IShuttleReaderFactory.Create"/> call. Used by
	/// <see cref="StartAsync_WhenOCEDuringCheckpointResume_PropagatesOperationCanceledException"/> to
	/// simulate cancellation inside <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/>
	/// where no <see cref="ExecutionStageMonitor"/> stage is available.
	/// </summary>
	/// <remarks>
	/// No code path during the first <see cref="DatabaseInitializer.StartAsync"/> or manual backup creation
	/// uses <see cref="IShuttleReaderFactory"/>, so registering this factory in DI only affects the
	/// checkpoint-resume path in the second <see cref="DatabaseInitializer.StartAsync"/> call.
	/// </remarks>
	private sealed class OceThrowingShuttleReaderFactory : IShuttleReaderFactory
	{
		/// <summary>
		/// Always throws <see cref="OperationCanceledException"/>.
		/// </summary>
		/// <param name="filePath">Ignored.</param>
		public IShuttleReader Create(string filePath) =>
			throw new OperationCanceledException("Simulated cancellation during checkpoint resume");
	}
}
