// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;

using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using Xunit;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Shared database fixture used by data tests.
/// </summary>
/// <remarks>
///     <para>
///     The default mode uses a shared SQLite in-memory connection so multiple <see cref="LumaCoreDbContext"/>
///     instances can observe the same database state. Depending on <see cref="DbTestSettings"/>, the fixture can
///     also target external providers.
///     </para>
///     <para>
///     <b>Database naming for external providers:</b> Each fixture combines <see cref="DbTestSettings.DatabasePrefix"/>
///     with a GUID suffix (e.g., <c>lumacore_test_a1b2c3…</c>) so that test classes can run in parallel without
///     interfering with each other. The connection string provides only transport and authentication — the database
///     name is derived entirely from the prefix. <see cref="DisposeAsync"/> drops the unique database after the
///     test class finishes (best-effort). If cleanup fails or the test runner is killed, orphaned databases may
///     remain — on CI this is irrelevant because the database container is destroyed; locally they can be removed
///     manually.
///     </para>
/// </remarks>
public sealed class DbFixture : IAsyncLifetime
{
	/// <summary>
	/// The shared SQLite in-memory connection kept open for the lifetime of the fixture.
	/// <see langword="null"/> when using an external provider (PostgreSQL, SQL Server).
	/// </summary>
	private SqliteConnection? mConnection;

	/// <summary>
	/// The temporary folder containing the SQLite database file and its journal files (WAL, SHM).
	/// <see langword="null"/> for in-memory or external providers.
	/// </summary>
	private TemporaryFolder? mDatabaseFolder;

	/// <summary>
	/// The resolved connection string for external providers (PostgreSQL, SQL Server), including the
	/// GUID-suffixed database name. <see langword="null"/> for SQLite providers.
	/// </summary>
	private string? mConnectionString;

	/// <summary>
	/// The resolved test settings indicating which provider and connection string to use.
	/// </summary>
	private DbTestSettings? mSettings;

	/// <summary>
	/// Disposes the fixture database context, drops any unique test database, and releases underlying connections.
	/// </summary>
	/// <remarks>
	/// For external providers (PostgreSQL, SQL Server, MySQL) each fixture creates a unique database with a GUID
	/// suffix. <c>EnsureDeletedAsync</c> drops that database so test runs don't leave orphaned databases behind.
	/// The deletion is best-effort — on CI the container is destroyed anyway.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		// External providers: drop the unique per-fixture database before disposing the context.
		if (mSettings?.Provider is DbProvider.PostgreSql or DbProvider.SqlServer or DbProvider.MySql)
		{
			try
			{
				await DbContext.Database.EnsureDeletedAsync();
			}
			catch
			{
				/* best-effort cleanup — CI containers are destroyed anyway */
			}
		}

		await DbContext.DisposeAsync();
		if (mConnection is not null)
		{
			await mConnection.DisposeAsync();
			mConnection = null;
		}

		if (mDatabaseFolder is not null)
		{
			SqliteConnection.ClearAllPools();
			mDatabaseFolder.Dispose();
			mDatabaseFolder = null;
		}
	}

	/// <summary>
	/// The primary <see cref="LumaCoreDbContext"/> instance for tests.
	/// </summary>
	public required LumaCoreDbContext DbContext { get; init; }

	/// <summary>
	/// Creates a new fixture using settings loaded from the test configuration.
	/// </summary>
	/// <remarks>
	/// The provider is determined by <see cref="DbTestSettingsLoader"/>. For tests that should always
	/// use SQLite in-memory regardless of configuration, use <see cref="CreateSqliteInMemory()"/> instead.
	/// </remarks>
	public static DbFixture Create()
	{
		DbTestSettings settings = DbTestSettingsLoader.Load();

		return settings.Provider switch
		{
			DbProvider.SqliteInMemory => CreateSqliteInMemory(settings),
			DbProvider.Sqlite         => CreateSqliteFile(settings),
			DbProvider.PostgreSql     => CreatePostgreSql(settings),
			DbProvider.SqlServer      => CreateSqlServer(settings),
			// TODO: Add MySQL/MariaDB support in the future (Pomelo compatibility with EF Core 10 is currently
			//       lacking). The factory method should follow the same GUID-based database naming pattern as
			//       CreatePostgreSql() / CreateSqlServer() for parallel test isolation.
			DbProvider.MySql => throw new NotSupportedException(
				                    "MySQL/MariaDB is currently not supported in LumaCore with EF Core 10 (Pomelo compatibility). " +
				                    "This option exists to prepare wiring; selecting it is expected to fail."),
			var unknown => throw new NotSupportedException($"Unsupported database provider: {unknown}")
		};
	}

	/// <summary>
	/// Creates a fixture backed by SQLite in-memory, bypassing <see cref="DbTestSettingsLoader"/>.
	/// </summary>
	/// <returns>A fixture using a shared SQLite in-memory connection.</returns>
	/// <remarks>
	/// Use this for tests that validate provider-independent behavior (e.g., EF model compatibility)
	/// and should not be affected by external database configuration.
	/// </remarks>
	public static DbFixture CreateSqliteInMemory() =>
		CreateSqliteInMemory(new DbTestSettings { Provider = DbProvider.SqliteInMemory });

	/// <summary>
	/// Creates a fresh <see cref="LumaCoreDbContext"/> instance.
	/// </summary>
	/// <remarks>
	/// Some tests deliberately require a new context to reach database constraint paths (e.g. UNIQUE violations)
	/// without being short-circuited by EF change tracking.
	/// </remarks>
	public LumaCoreDbContext CreateDbContext()
	{
		// Special test utility: some tests need a *fresh* DbContext instance to reliably hit database constraint
		// branches (e.g. DbUpdateException on UNIQUE constraints). Reusing the same DbContext can fail earlier with
		// EF tracking errors (identity conflicts) and never reach the database.
		//
		// SQLite (both file and in-memory): the new context reuses the fixture's shared connection so every
		// context the fixture hands out participates in the same SQLite session. A new physical connection on
		// the same file would deadlock on SQLite's file-level write lock the moment two contexts overlap a
		// write transaction. The shared-connection branch at the bottom handles both SQLite providers.

		if (mSettings?.Provider is DbProvider.PostgreSql)
		{
			if (mConnectionString is null)
				throw new InvalidOperationException("Fixture connection string not initialized.");

			DbContextOptions<LumaCoreDbContext> npgsqlOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseNpgsql(mConnectionString)
				.Options;
			return new LumaCoreDbContext(npgsqlOptions);
		}

		if (mSettings?.Provider is DbProvider.SqlServer)
		{
			if (mConnectionString is null)
				throw new InvalidOperationException("Fixture connection string not initialized.");

			DbContextOptions<LumaCoreDbContext> sqlServerOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseSqlServer(mConnectionString)
				.Options;
			return new LumaCoreDbContext(sqlServerOptions);
		}

		if (mConnection is null)
			throw new InvalidOperationException("Fixture connection not initialized.");

		// SQLite (in-memory or file): create a new context using the shared connection so every context
		// observes the same database state and avoids SQLite's file-level write-lock contention.
		DbContextOptions<LumaCoreDbContext> sqliteOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite(mConnection)
			.Options;

		return new LumaCoreDbContext(sqliteOptions);
	}

	/// <summary>
	/// Initializes the database schema for the fixture.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Every provider starts with an empty database — SQLite uses fresh in-memory connections or unique temp
	///     files, external providers (PostgreSQL, SQL Server, MySQL) use a unique database per fixture whose name
	///     includes a GUID suffix (see <see cref="CreatePostgreSql"/> / <see cref="CreateSqlServer"/>).
	///     </para>
	///     <para>
	///     Because the database is always new, only <c>EnsureCreatedAsync</c> is needed here.
	///     Cleanup happens in <see cref="DisposeAsync"/> via <c>EnsureDeletedAsync</c>.
	///     </para>
	/// </remarks>
	public async ValueTask InitializeAsync()
	{
		await DbContext.Database.EnsureCreatedAsync();
	}

	/// <summary>
	/// Creates a fixture backed by a shared SQLite in-memory connection.
	/// </summary>
	/// <param name="settings">The resolved test settings.</param>
	/// <returns>
	/// A fixture whose <see cref="DbContext"/> and <see cref="CreateDbContext"/> share the same in-memory database.
	/// </returns>
	/// <remarks>
	/// The connection is opened immediately and kept alive for the lifetime of the fixture.
	/// This ensures multiple <see cref="LumaCoreDbContext"/> instances observe the same database state.
	/// </remarks>
	private static DbFixture CreateSqliteInMemory(DbTestSettings settings)
	{
		// Use a single shared in-memory SQLite connection for the lifetime of the fixture.
		// This allows us to create multiple DbContext instances that still see the same database state.
		var connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
		connection.Open();

		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite(connection)
			.Options;

		var dbContext = new LumaCoreDbContext(options);
		return new DbFixture
		{
			mConnection = connection,
			mSettings = settings,
			DbContext = dbContext
		};
	}

	/// <summary>
	/// Creates a fixture backed by a temporary SQLite database file.
	/// </summary>
	/// <param name="settings">The resolved test settings.</param>
	/// <returns>
	/// A fixture whose <see cref="DbContext"/> uses a temporary file that is deleted on disposal.
	/// </returns>
	/// <remarks>
	/// This provides closer-to-production SQLite behavior compared to in-memory: file I/O, locking, and
	/// journaling are exercised. A <see cref="TemporaryFolder"/> contains the database file and its journal
	/// files (WAL, SHM) so that disposal cleans up everything.
	/// <para>
	/// A single <see cref="SqliteConnection"/> is opened and shared by every <see cref="LumaCoreDbContext"/>
	/// the fixture hands out — both <see cref="DbContext"/> and any context returned by
	/// <see cref="CreateDbContext"/>. SQLite uses a file-level write lock, so opening a second physical
	/// connection while the first one holds an open transaction immediately fails with
	/// <c>SQLITE_BUSY</c> ("database is locked"). Tests that rely on cooperating contexts (e.g. a side
	/// context inserting a race-winner row while the SUT is mid-upload under an ambient transaction)
	/// would otherwise be unrunnable on the file-based provider.
	/// </para>
	/// </remarks>
	private static DbFixture CreateSqliteFile(DbTestSettings settings)
	{
		var folder = new TemporaryFolder("lumacore-test");
		string connectionString = $"Data Source={folder.GetFilePath("test.db")}";

		// Open the connection once and reuse it for every DbContext the fixture hands out (mirroring the
		// in-memory variant). A fresh-per-context connection would deadlock on SQLite's file-level write
		// lock as soon as one context holds an open transaction and another tries to write.
		var connection = new SqliteConnection(connectionString);
		connection.Open();

		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite(connection)
			.Options;

		var dbContext = new LumaCoreDbContext(options);
		return new DbFixture
		{
			mConnection = connection,
			mDatabaseFolder = folder,
			mSettings = settings,
			DbContext = dbContext
		};
	}

	/// <summary>
	/// Creates a fixture backed by an external PostgreSQL database.
	/// </summary>
	/// <param name="settings">The resolved test settings (must include a non-empty connection string).</param>
	/// <returns>A fixture configured for PostgreSQL.</returns>
	/// <exception cref="InvalidOperationException">
	/// <see cref="DbTestSettings.ConnectionString"/> is <see langword="null"/> or whitespace.
	/// </exception>
	private static DbFixture CreatePostgreSql(DbTestSettings settings)
	{
		if (string.IsNullOrWhiteSpace(settings.ConnectionString))
			throw new InvalidOperationException("PostgreSQL selected but no connection string provided.");

		// The connection string carries transport/auth only — the database name comes from DatabasePrefix + GUID
		// so that each fixture gets an isolated database for parallel execution.
		var builder = new NpgsqlConnectionStringBuilder(settings.ConnectionString)
		{
			Database = $"{settings.DatabasePrefix}_{Guid.NewGuid():N}"
		};

		string resolvedConnectionString = builder.ConnectionString;

		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseNpgsql(resolvedConnectionString)
			.Options;

		return new DbFixture
		{
			mConnectionString = resolvedConnectionString,
			mSettings = settings,
			DbContext = new LumaCoreDbContext(options)
		};
	}

	/// <summary>
	/// Creates a fixture backed by an external SQL Server database.
	/// </summary>
	/// <param name="settings">The resolved test settings (must include a non-empty connection string).</param>
	/// <returns>A fixture configured for SQL Server.</returns>
	/// <exception cref="InvalidOperationException">
	/// <see cref="DbTestSettings.ConnectionString"/> is <see langword="null"/> or whitespace.
	/// </exception>
	private static DbFixture CreateSqlServer(DbTestSettings settings)
	{
		if (string.IsNullOrWhiteSpace(settings.ConnectionString))
			throw new InvalidOperationException("SQL Server selected but no connection string provided.");

		// The connection string carries transport/auth only — the database name comes from DatabasePrefix + GUID
		// so that each fixture gets an isolated database for parallel execution.
		var builder = new SqlConnectionStringBuilder(settings.ConnectionString)
		{
			InitialCatalog = $"{settings.DatabasePrefix}_{Guid.NewGuid():N}"
		};

		string resolvedConnectionString = builder.ConnectionString;

		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlServer(resolvedConnectionString)
			.Options;

		return new DbFixture
		{
			mConnectionString = resolvedConnectionString,
			mSettings = settings,
			DbContext = new LumaCoreDbContext(options)
		};
	}
}
