// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Export.Implementations;

using Microsoft.Data.Sqlite;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteExportReaderTests
{
	/// <summary>
	/// Returns a unique shared in-memory connection string. Multiple connections to the same
	/// <paramref name="dbName"/> see the same database as long as at least one connection remains open.
	/// </summary>
	/// <param name="dbName">A unique database name for isolation between tests.</param>
	private static string SharedMemoryConnectionString(string dbName) =>
		$"Data Source={dbName};Mode=Memory;Cache=Shared";

	/// <summary>
	/// Returns a unique database name to prevent cross-test contamination.
	/// </summary>
	private static string UniqueDbName() => $"export_{Guid.NewGuid():N}";

	/// <summary>
	/// Creates a shared in-memory SQLite database populated with test tables and data,
	/// and returns the keeper connection that must stay open for the database to persist.
	/// </summary>
	/// <param name="connectionString">The shared in-memory connection string.</param>
	/// <returns>An open keeper connection. Dispose it after the test completes.</returns>
	/// <remarks>
	/// The schema is a simplified synthetic schema (<c>Users</c>, <c>Messages</c>) — not the production
	/// <c>LumaCoreDbContext</c> schema. It covers the column variants relevant to the reader:
	/// <list type="bullet">
	///     <item>
	///         <description>Primary key column (<c>Id INTEGER PRIMARY KEY</c>)</description>
	///     </item>
	///     <item>
	///         <description>Non-nullable column (<c>Name TEXT NOT NULL</c>)</description>
	///     </item>
	///     <item>
	///         <description>Nullable column (<c>Email TEXT</c>)</description>
	///     </item>
	///     <item>
	///         <description>Foreign-key-like column (<c>UserId INTEGER</c>)</description>
	///     </item>
	///     <item>
	///         <description>Row with a <see langword="null"/> value (Bob's <c>Email</c>)</description>
	///     </item>
	/// </list>
	/// Additionally, the <c>__EFMigrationsHistory</c> table is included to verify migration-history
	/// reading and table-name filtering.
	/// </remarks>
	private static async Task<SqliteConnection> CreatePopulatedDatabaseAsync(string connectionString)
	{
		var keeper = new SqliteConnection(connectionString);
		await keeper.OpenAsync();

		SqliteCommand cmd = keeper.CreateCommand();
		try
		{
			cmd.CommandText =
				"""
				CREATE TABLE Users (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Email TEXT);
				CREATE TABLE Messages (Id INTEGER PRIMARY KEY, Content TEXT NOT NULL, UserId INTEGER);
				CREATE TABLE __EFMigrationsHistory (MigrationId TEXT PRIMARY KEY, ProductVersion TEXT NOT NULL);

				INSERT INTO Users (Id, Name, Email) VALUES (1, 'Alice', 'alice@test.com');
				INSERT INTO Users (Id, Name, Email) VALUES (2, 'Bob', NULL);
				INSERT INTO Messages (Id, Content, UserId) VALUES (1, 'Hello', 1);

				INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
				VALUES ('20260101000000_Initial', '10.0.0');
				INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
				VALUES ('20260201000000_AddMessages', '10.0.0');
				""";
			await cmd.ExecuteNonQueryAsync();
		}
		finally
		{
			await cmd.DisposeAsync();
		}

		return keeper;
	}

	/// <summary>
	/// Creates a shared in-memory SQLite database with no tables.
	/// </summary>
	/// <param name="connectionString">The shared in-memory connection string.</param>
	/// <returns>An open keeper connection.</returns>
	private static async Task<SqliteConnection> CreateEmptyDatabaseAsync(string connectionString)
	{
		var keeper = new SqliteConnection(connectionString);
		await keeper.OpenAsync();
		return keeper;
	}

	/// <summary>
	/// Creates an initialized <see cref="SqliteExportReader"/> for the given connection string.
	/// </summary>
	/// <param name="connectionString">The SQLite connection string.</param>
	/// <returns>An initialized reader. The caller must dispose it.</returns>
	private static async Task<SqliteExportReader> CreateInitializedReaderAsync(string connectionString)
	{
		var reader = new SqliteExportReader(connectionString);
		await reader.InitializeAsync();
		return reader;
	}
}
