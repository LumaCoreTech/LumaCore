// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Data.Common;

using LumaCore.Data.Providers;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Encapsulates a <see cref="LumaCoreDbContext"/>, <see cref="IMigrator"/>,
/// <see cref="IDatabaseProviderOperations"/>, and <see cref="IDatabaseTestOperations"/> for integration tests
/// that need a real database.
/// </summary>
/// <remarks>
///     <para>
///     For <see cref="DbProvider.SqliteInMemory"/>, a shared-cache in-memory database
///     (<c>Data Source={name};Mode=Memory;Cache=Shared</c>) is used. A dedicated keeper
///     <see cref="SqliteConnection"/> is held open for the lifetime of the harness so the database
///     persists. Independent connections (e.g., from DataPort readers/writers) can access the same
///     database via <see cref="ConnectionString"/>. For <see cref="DbProvider.Sqlite"/>, a temporary
///     file is used to exercise real file I/O. For external providers (PostgreSQL, SQL Server), the
///     test database is dropped via <see cref="DatabaseFacade.EnsureDeletedAsync"/>.
///     </para>
///     <para>
///     Use <see cref="CreateAsync"/> to build a harness. The <c>ensureCreated</c> parameter controls whether
///     the schema is created immediately via <see cref="DatabaseFacade.EnsureCreatedAsync"/> (for provider
///     operations tests) or left empty for migration-driven tests.
///     </para>
/// </remarks>
sealed class IntegrationTestHarness : IAsyncDisposable
{
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
	/// The owned SQLite shared-cache keeper connection. Kept open for the harness lifetime so the
	/// in-memory database persists. <see langword="null"/> for file-based SQLite and external providers.
	/// </summary>
	private readonly SqliteConnection? mConnection;

	/// <summary>
	/// Abstracts low-level database operations (count rows, list tables/indexes) so that test assertions
	/// remain provider-agnostic.
	/// </summary>
	private readonly IDatabaseTestOperations mTestOperations;

	/// <summary>
	/// Initializes a new instance of the <see cref="IntegrationTestHarness"/> class.
	/// </summary>
	/// <param name="providerOperations">The provider operations for provider-agnostic schema verification.</param>
	/// <param name="dbContext">The database context connected to the test database.</param>
	/// <param name="databasePath">
	/// The path to the temporary SQLite database file, or <see langword="null"/> for in-memory SQLite
	/// and external providers.
	/// </param>
	/// <param name="providerName">
	/// The provider name (e.g., <see cref="DatabaseProviders.Sqlite"/>), used for cleanup decisions.
	/// </param>
	/// <param name="connectionString">The connection string used to create the database.</param>
	/// <param name="connection">
	/// The owned SQLite shared-cache keeper connection, or <see langword="null"/> for file-based SQLite
	/// and external providers.
	/// </param>
	private IntegrationTestHarness(
		IDatabaseProviderOperations providerOperations,
		LumaCoreDbContext           dbContext,
		string?                     databasePath,
		string                      providerName,
		string                      connectionString,
		SqliteConnection?           connection)
	{
		ProviderOperations = providerOperations;
		DbContext = dbContext;
		Migrator = dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
		ConnectionString = connectionString;
		mDatabasePath = databasePath;
		mProviderName = providerName;
		mConnection = connection;
		mTestOperations = RelationalDatabaseTestOperations.Create(providerOperations);
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
				await DbContext
					.Database
					.EnsureDeletedAsync()
					.ConfigureAwait(false);
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

	/// <summary>
	/// The provider operations for provider-agnostic schema verification
	/// (e.g., <see cref="IDatabaseProviderOperations.TableExistsAsync"/>).
	/// </summary>
	public IDatabaseProviderOperations ProviderOperations { get; }

	/// <summary>
	/// The database context connected to the test database.
	/// </summary>
	public LumaCoreDbContext DbContext { get; }

	/// <summary>
	/// The EF Core migrator for applying and reverting migrations.
	/// </summary>
	public IMigrator Migrator { get; }

	/// <summary>
	/// The connection string used to create the test database. Components that open their own
	/// connections (e.g., DataPort readers/writers) can use this to reach the same database.
	/// </summary>
	public string ConnectionString { get; }

	/// <summary>
	/// Builds an <see cref="IntegrationTestHarness"/> with a fresh database determined by the test
	/// configuration (defaults to SQLite in-memory; switchable to any supported provider).
	/// </summary>
	/// <param name="dbNamePrefix">
	/// A short prefix for the database name (e.g., <c>"provops"</c>, <c>"migration"</c>), used in
	/// temporary file names and database names for external providers.
	/// </param>
	/// <param name="ensureCreated">
	/// When <see langword="true"/> (default), calls <see cref="DatabaseFacade.EnsureCreatedAsync"/> to create
	/// the schema from the EF Core model. When <see langword="false"/>, the database is left empty so that
	/// tests can drive schema creation exclusively through <see cref="IMigrator"/>.
	/// </param>
	/// <returns>A disposable harness containing the provider operations and all infrastructure.</returns>
	/// <exception cref="InvalidOperationException">
	/// An external provider is selected but no connection string is configured.
	/// </exception>
	/// <exception cref="NotSupportedException">MySQL/MariaDB or an unknown provider is selected.</exception>
	/// <remarks>
	/// The database provider is determined by <see cref="DbTestSettingsLoader"/>: <c>appsettings.json</c>,
	/// <c>appsettings.Development.json</c>, and environment variables (in ascending priority). Defaults to
	/// SQLite in-memory. When <see cref="DbProvider.Sqlite"/> is selected, a temporary file in the system
	/// temp directory is used instead.
	/// </remarks>
	public static async Task<IntegrationTestHarness> CreateAsync(string dbNamePrefix, bool ensureCreated = true)
	{
		DbTestSettings settings = DbTestSettingsLoader.Load();

		string? databasePath = null;
		SqliteConnection? sqliteConnection = null;
		string providerName;
		string connectionString;
		LumaCoreDbContext dbContext;

		switch (settings.Provider)
		{
			case DbProvider.SqliteInMemory:
			{
				// Use shared-cache mode so that independent connections (e.g., DataPort readers/writers)
				// can access the same in-memory database via ConnectionString. A keeper connection is
				// held open for the harness lifetime — the database is destroyed when it is disposed.
				providerName = DatabaseProviders.Sqlite;
				string sharedDbName = $"{dbNamePrefix}_{Guid.NewGuid():N}";
				connectionString = $"Data Source={sharedDbName};Mode=Memory;Cache=Shared";
				sqliteConnection = new SqliteConnection(connectionString);
				await sqliteConnection.OpenAsync().ConfigureAwait(false);

				DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
					.UseSqlite(connectionString)
					.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
					.Options;
				dbContext = new LumaCoreDbContext(options);
				break;
			}

			case DbProvider.Sqlite:
			{
				providerName = DatabaseProviders.Sqlite;
				databasePath = Path.Combine(Path.GetTempPath(), $"{dbNamePrefix}-test-{Guid.NewGuid():N}.db");
				connectionString = $"Data Source={databasePath}";

				DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
					.UseSqlite(connectionString)
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
					["Database"] = $"{dbNamePrefix}_test_{Guid.NewGuid():N}"
				};
				connectionString = csBuilder.ConnectionString;

				DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
					.UseNpgsql(connectionString)
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
				csBuilder[dbKey] = $"{dbNamePrefix}_test_{Guid.NewGuid():N}";
				connectionString = csBuilder.ConnectionString;

				DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
					.UseSqlServer(connectionString)
					.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
					.Options;
				dbContext = new LumaCoreDbContext(options);
				break;
			}

			case DbProvider.MySql:
			{
				// TODO: Implement MySQL/MariaDB support once Pomelo.EntityFrameworkCore.MySql releases an
				//       EF Core 10 compatible version.
				throw new NotSupportedException(
					"MySQL/MariaDB support is temporarily unavailable. " +
					"Pomelo.EntityFrameworkCore.MySql has not yet released an EF Core 10 compatible version. " +
					"Track progress at: " +
					"https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues");
			}

			default:
				throw new NotSupportedException($"Unsupported database provider: {settings.Provider}");
		}

		if (ensureCreated)
		{
			await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
		}

		IDatabaseProviderOperations providerOps = DatabaseProviderFactory.GetProvider(providerName);

		return new IntegrationTestHarness(
			providerOps,
			dbContext,
			databasePath,
			providerName,
			connectionString,
			sqliteConnection);
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
	/// Returns the names of all user-defined tables, excluding EF Core infrastructure tables.
	/// </summary>
	/// <returns>An alphabetically sorted array of user table names.</returns>
	public async Task<string[]> GetUserTableNamesAsync()
	{
		DbConnection connection = await GetOpenConnectionAsync().ConfigureAwait(false);
		return await mTestOperations
			       .GetUserTableNamesAsync(connection, CancellationToken.None)
			       .ConfigureAwait(false);
	}

	/// <summary>
	/// Returns the names of all explicitly-created indexes, excluding auto-generated indexes for
	/// PRIMARY KEY and UNIQUE constraints.
	/// </summary>
	/// <returns>An alphabetically sorted array of explicit index names.</returns>
	public async Task<string[]> GetExplicitIndexNamesAsync()
	{
		DbConnection connection = await GetOpenConnectionAsync().ConfigureAwait(false);
		return await mTestOperations
			       .GetExplicitIndexNamesAsync(connection, CancellationToken.None)
			       .ConfigureAwait(false);
	}
}
