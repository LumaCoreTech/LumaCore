// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Data.Common;

using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LumaCore.Data.Tests.Providers;

// Shared test infrastructure: TestHarness (lightweight connection + DbContext + provider ops)
// and CreateHarnessAsync() factory method.
public sealed partial class ProviderOperationsIntegrationTests
{
	/// <summary>
	/// Encapsulates a database connection, <see cref="LumaCoreDbContext"/>, and
	/// <see cref="IDatabaseProviderOperations"/> for provider-level integration tests.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Unlike the <c>DatabaseInitializerTests.TestHarness</c>, this harness is deliberately lightweight:
	///     no DI container, no <see cref="DatabaseInitializer"/>, no migrations. It uses
	///     <see cref="DatabaseFacade.EnsureCreatedAsync"/> to create the schema directly from the model, which
	///     is sufficient for testing provider-specific SQL operations.
	///     </para>
	///     <para>
	///     For <see cref="DbProvider.SqliteInMemory"/>, a dedicated <see cref="SqliteConnection"/> with
	///     <c>Data Source=:memory:</c> is kept open for the lifetime of the harness — the in-memory database
	///     is destroyed when the connection is disposed. For <see cref="DbProvider.Sqlite"/>, a temporary file
	///     is used to exercise real file I/O. For external providers (PostgreSQL, SQL Server), the test
	///     database is dropped via <see cref="DatabaseFacade.EnsureDeletedAsync"/>.
	///     </para>
	/// </remarks>
	private sealed class TestHarness : IAsyncDisposable
	{
		/// <summary>
		/// The provider operations under test.
		/// </summary>
		public IDatabaseProviderOperations Sut { get; }

		/// <summary>
		/// The database context connected to the test database.
		/// </summary>
		public LumaCoreDbContext DbContext { get; }

		/// <summary>
		/// Absolute path to the temporary SQLite database file.
		/// <see langword="null"/> for in-memory SQLite and external providers (PostgreSQL, SQL Server)
		/// where cleanup is handled via <see cref="DatabaseFacade.EnsureDeletedAsync"/>.
		/// </summary>
		private readonly string? mDatabasePath;

		/// <summary>
		/// The provider name for cleanup decisions.
		/// </summary>
		private readonly string mProviderName;

		/// <summary>
		/// Abstracts low-level database operations (count rows, delete rows) so that test assertions
		/// remain provider-agnostic.
		/// </summary>
		private readonly IDatabaseTestOperations mTestOperations;

		/// <summary>
		/// The owned SQLite in-memory connection. Kept open for the harness lifetime so the in-memory
		/// database persists. <see langword="null"/> for file-based SQLite and external providers.
		/// </summary>
		private readonly SqliteConnection? mConnection;

		/// <summary>
		/// Initializes a new instance of the <see cref="TestHarness"/> class.
		/// </summary>
		/// <param name="sut">The provider operations under test.</param>
		/// <param name="dbContext">The database context connected to the test database.</param>
		/// <param name="databasePath">
		/// The path to the temporary SQLite database file, or <see langword="null"/> for in-memory SQLite
		/// and external providers.
		/// </param>
		/// <param name="providerName">
		/// The provider name (e.g., <see cref="DatabaseProviders.Sqlite"/>), used for cleanup decisions.
		/// </param>
		/// <param name="connection">
		/// The owned SQLite in-memory connection, or <see langword="null"/> for file-based SQLite and
		/// external providers.
		/// </param>
		public TestHarness(
			IDatabaseProviderOperations sut,
			LumaCoreDbContext           dbContext,
			string?                     databasePath,
			string                      providerName,
			SqliteConnection?           connection)
		{
			Sut = sut;
			DbContext = dbContext;
			mDatabasePath = databasePath;
			mProviderName = providerName;
			mConnection = connection;
			mTestOperations = RelationalDatabaseTestOperations.Create(sut);
		}

		/// <summary>
		/// Gets the underlying database connection from the <see cref="DbContext"/>, opening it if necessary.
		/// </summary>
		/// <returns>An open <see cref="DbConnection"/>.</returns>
		public async Task<DbConnection> GetOpenConnectionAsync()
		{
			DbConnection connection = DbContext.Database.GetDbConnection();
			if (connection.State != ConnectionState.Open)
				await connection.OpenAsync().ConfigureAwait(false);
			return connection;
		}

		/// <summary>
		/// Returns the number of rows in the specified table.
		/// </summary>
		/// <param name="tableName">The unquoted table name.</param>
		/// <returns>The row count.</returns>
		public async Task<long> CountRowsAsync(string tableName)
		{
			DbConnection connection = await GetOpenConnectionAsync().ConfigureAwait(false);
			return await mTestOperations
				       .CountRowsAsync(connection, tableName, CancellationToken.None)
				       .ConfigureAwait(false);
		}

		/// <summary>
		/// Disposes the harness: drops the test database for external providers, disposes the
		/// <see cref="DbContext"/>, and cleans up SQLite resources (closes the in-memory connection or
		/// deletes the temporary file).
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			// For external providers, drop the test database before releasing connections.
			if (mProviderName is not DatabaseProviders.Sqlite)
			{
				try
				{
					await DbContext.Database.EnsureDeletedAsync().ConfigureAwait(false);
				}
				catch
				{
					// best-effort — CI runners may leave orphaned databases
				}
			}

			await DbContext.DisposeAsync().ConfigureAwait(false);

			// For SQLite in-memory, dispose the owned connection (destroys the database).
			if (mConnection is not null)
			{
				await mConnection.DisposeAsync().ConfigureAwait(false);
			}

			// For SQLite file-based, clear the connection pool to release file locks held by
			// pooled connections, then delete the temporary database file.
			if (mDatabasePath is not null)
			{
				SqliteConnection.ClearAllPools();
				try { File.Delete(mDatabasePath); }
				catch
				{
					// best-effort cleanup
				}
			}
		}
	}

	/// <summary>
	/// Builds a <see cref="TestHarness"/> with a fresh database determined by the test configuration
	/// (defaults to SQLite in-memory; CI may use PostgreSQL or SQL Server).
	/// </summary>
	/// <returns>A disposable harness containing the provider operations under test and all infrastructure.</returns>
	/// <exception cref="InvalidOperationException">
	/// An external provider is selected but no connection string is configured.
	/// </exception>
	/// <exception cref="NotSupportedException">MySQL/MariaDB or an unknown provider is selected.</exception>
	/// <remarks>
	///     <para>
	///     The database provider is determined by <see cref="DbTestSettingsLoader"/>. Locally, this defaults to
	///     SQLite in-memory. In CI, PostgreSQL or SQL Server can be selected via environment variables.
	///     When <see cref="DbProvider.Sqlite"/> is selected, a temporary file in the system temp directory
	///     is used instead.
	///     </para>
	/// </remarks>
	private static async Task<TestHarness> CreateHarnessAsync()
	{
		DbTestSettings settings = DbTestSettingsLoader.Load();

		string? databasePath = null;
		SqliteConnection? sqliteConnection = null;
		string providerName;
		LumaCoreDbContext dbContext;

		switch (settings.Provider)
		{
			case DbProvider.SqliteInMemory:
			{
				// Use a dedicated in-memory connection. The database exists as long as the connection
				// stays open — disposing the harness closes it, which destroys the database.
				providerName = DatabaseProviders.Sqlite;
				sqliteConnection = new SqliteConnection("Data Source=:memory:");
				await sqliteConnection.OpenAsync().ConfigureAwait(false);

				DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
					.UseSqlite(sqliteConnection)
					.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
					.Options;
				dbContext = new LumaCoreDbContext(options);
				break;
			}

			case DbProvider.Sqlite:
			{
				providerName = DatabaseProviders.Sqlite;
				databasePath = Path.Combine(Path.GetTempPath(), $"provops-test-{Guid.NewGuid():N}.db");

				DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
					.UseSqlite($"Data Source={databasePath}")
					.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
					.Options;
				dbContext = new LumaCoreDbContext(options);
				break;
			}

			case DbProvider.PostgreSql:
			{
				if (string.IsNullOrWhiteSpace(settings.ConnectionString))
				{
					throw new InvalidOperationException(
						$"{settings.Provider} selected but no connection string configured " +
						"(set LUMACORE_TESTS__Db__ConnectionString).");
				}

				providerName = DatabaseProviders.PostgreSql;
				var csBuilder = new DbConnectionStringBuilder
				{
					ConnectionString = settings.ConnectionString,
					["Database"] = $"provops_test_{Guid.NewGuid():N}"
				};

				DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
					.UseNpgsql(csBuilder.ConnectionString)
					.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
					.Options;
				dbContext = new LumaCoreDbContext(options);
				break;
			}

			case DbProvider.SqlServer:
			{
				if (string.IsNullOrWhiteSpace(settings.ConnectionString))
				{
					throw new InvalidOperationException(
						$"{settings.Provider} selected but no connection string configured " +
						"(set LUMACORE_TESTS__Db__ConnectionString).");
				}

				providerName = DatabaseProviders.SqlServer;
				var csBuilder = new DbConnectionStringBuilder { ConnectionString = settings.ConnectionString };
				string dbKey = csBuilder.ContainsKey("Initial Catalog") ? "Initial Catalog" : "Database";
				csBuilder[dbKey] = $"provops_test_{Guid.NewGuid():N}";

				DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
					.UseSqlServer(csBuilder.ConnectionString)
					.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
					.Options;
				dbContext = new LumaCoreDbContext(options);
				break;
			}

			case DbProvider.MySql:
			{
				// TODO: Implement MySQL/MariaDB support once Pomelo.EntityFrameworkCore.MySql releases an EF Core 10 compatible version.
				throw new NotSupportedException(
					"MySQL/MariaDB support is temporarily unavailable. " +
					"Pomelo.EntityFrameworkCore.MySql has not yet released an EF Core 10 compatible version. " +
					"Track progress at: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues");
			}

			default:
				throw new NotSupportedException($"Unsupported database provider: {settings.Provider}");
		}

		// Create the database and entity schema. This ensures the database exists (important for PG/SQL
		// Server) and provides real tables for TableExistsAsync tests.
		await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);

		// Get the provider operations for the target provider, which will be used by tests to execute
		// provider-specific SQL.
		IDatabaseProviderOperations sut = DatabaseProviderFactory.GetProvider(providerName);

		// Return the harness containing the provider operations, DbContext, and cleanup info.
		return new TestHarness(sut, dbContext, databasePath, providerName, sqliteConnection);
	}
}
