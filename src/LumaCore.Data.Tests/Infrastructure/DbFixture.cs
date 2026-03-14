// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Shared database fixture used by data tests.
/// </summary>
/// <remarks>
/// The default mode uses a shared SQLite in-memory connection so multiple <see cref="LumaCoreDbContext"/> instances can
/// observe the same database state. Depending on <see cref="DbTestSettings"/>, the fixture can also target external
/// providers.
/// </remarks>
public sealed class DbFixture : IAsyncLifetime
{
	/// <summary>
	/// The shared SQLite in-memory connection kept open for the lifetime of the fixture.
	/// <see langword="null"/> when using an external provider (PostgreSQL, SQL Server).
	/// </summary>
	private SqliteConnection? mConnection;

	/// <summary>
	/// Path to a temporary SQLite database file used by the <see cref="DbProvider.Sqlite"/> provider.
	/// <see langword="null"/> for in-memory or external providers.
	/// </summary>
	private string? mDatabasePath;

	/// <summary>
	/// The resolved test settings indicating which provider and connection string to use.
	/// </summary>
	private DbTestSettings? mSettings;

	/// <summary>
	/// Disposes the fixture database context and any underlying connection.
	/// </summary>
	public async Task DisposeAsync()
	{
		await DbContext.DisposeAsync();
		if (mConnection is not null)
		{
			await mConnection.DisposeAsync();
			mConnection = null;
		}

		if (mDatabasePath is not null)
		{
			try { File.Delete(mDatabasePath); }
			catch
			{
				/* best-effort cleanup */
			}
			mDatabasePath = null;
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
			// TODO: Add MySQL/MariaDB support in the future (Pomelo compatibility with EF Core 10 is currently lacking).
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
		if (mSettings?.Provider is DbProvider.Sqlite)
		{
			if (mDatabasePath is null)
				throw new InvalidOperationException("Fixture database path not initialized.");

			DbContextOptions<LumaCoreDbContext> fileOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseSqlite($"Data Source={mDatabasePath}")
				.Options;

			return new LumaCoreDbContext(fileOptions);
		}

		if (mSettings?.Provider is DbProvider.PostgreSql or DbProvider.SqlServer)
		{
			// For non-SQLite providers, the fixture is already configured to use an external DB.
			// These tests are primarily built around SQLite in-memory; if a different provider is selected,
			// return a minimal context to keep helpers usable. (Most tests use sqlite by default.)
			DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseSqlite("Data Source=:memory:")
				.Options;
			return new LumaCoreDbContext(options);
		}

		if (mConnection is null)
			throw new InvalidOperationException("Fixture connection not initialized.");

		DbContextOptions<LumaCoreDbContext> sqliteOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite(mConnection)
			.Options;

		return new LumaCoreDbContext(sqliteOptions);
	}

	/// <summary>
	/// Initializes the database schema for the fixture.
	/// </summary>
	/// <remarks>
	/// When <see cref="DbTestSettings.EnsureDeleted"/> is <see langword="true"/> (typically for external providers
	/// in CI), the database is dropped and recreated. Otherwise, only <c>EnsureCreatedAsync</c> is called.
	/// </remarks>
	public async Task InitializeAsync()
	{
		if (mSettings?.EnsureDeleted == true)
		{
			await DbContext.Database.EnsureDeletedAsync();
		}
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
	/// journaling are exercised. The file is created in the system temp directory with a unique name.
	/// </remarks>
	private static DbFixture CreateSqliteFile(DbTestSettings settings)
	{
		string dbPath = Path.Combine(Path.GetTempPath(), $"lumacore-test-{Guid.NewGuid():N}.db");
		string connectionString = $"Data Source={dbPath}";

		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite(connectionString)
			.Options;

		var dbContext = new LumaCoreDbContext(options);
		return new DbFixture
		{
			mDatabasePath = dbPath,
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

		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseNpgsql(settings.ConnectionString)
			.Options;

		return new DbFixture
		{
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

		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlServer(settings.ConnectionString)
			.Options;

		return new DbFixture
		{
			mSettings = settings,
			DbContext = new LumaCoreDbContext(options)
		};
	}
}
