// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Import.Implementations;
using LumaCore.Data.DataPort.Models;

using Microsoft.Data.Sqlite;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteImportWriterTests
{
	/// <summary>
	/// Name of the checkpoint table created by the import writer.
	/// </summary>
	private const string CheckpointTableName = "_shuttle_import_checkpoint";

	/// <summary>
	/// A fixed shuttle ID for tests that don't care about mismatch scenarios.
	/// </summary>
	private const string TestShuttleId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

	/// <summary>
	/// A different shuttle ID used to trigger mismatch detection.
	/// </summary>
	private const string OtherShuttleId = "11111111-2222-3333-4444-555555555555";

	/// <summary>
	/// Creates a <see cref="SqliteImportWriter"/> pointing at the given database file.
	/// </summary>
	/// <param name="dbPath">Absolute path to the SQLite database file.</param>
	/// <returns>An un-initialized writer. The caller must call <see cref="SqliteImportWriter.InitializeAsync"/>.</returns>
	private static SqliteImportWriter CreateWriter(string dbPath) => new(
		$"Data Source={dbPath}",
		TimeProvider.System,
		logger: null);

	/// <summary>
	/// Creates a minimal SQLite database with two test tables (<c>Items</c> and <c>Orders</c>) and two
	/// fake migration history entries, providing a realistic multi-table setup for import tests.
	/// </summary>
	/// <param name="dbPath">Absolute path for the database file.</param>
	/// <param name="tableName">Name of the primary test data table to create.</param>
	/// <param name="seedRows">
	/// Optional rows to seed into <paramref name="tableName"/>. Each tuple is <c>(id, name)</c>.
	/// Pass <see langword="null"/> for an empty table.
	/// </param>
	/// <remarks>
	/// The schema uses simple two-column tables (<c>Id</c> + one text column) because the import writer
	/// is column-agnostic — it only needs to INSERT rows and manage checkpoints. This differs from the
	/// export reader tests which use a richer schema (<c>Users</c>, <c>Messages</c>) to exercise column
	/// metadata discovery (PK, nullable, NOT NULL, FK-like columns).
	/// </remarks>
	private static async Task CreateTestDatabaseAsync(
		string                   dbPath,
		string                   tableName = "Items",
		(int Id, string Name)[]? seedRows  = null)
	{
		string connStr = $"Data Source={dbPath}";
		await using var conn = new SqliteConnection(connStr);
		await conn.OpenAsync();

		await using SqliteCommand cmd = conn.CreateCommand();

		// Create the primary test data table.
		cmd.CommandText = $"""
		                   CREATE TABLE "{tableName}" (
		                       "Id"   INTEGER PRIMARY KEY AUTOINCREMENT,
		                       "Name" TEXT NOT NULL
		                   )
		                   """;
		await cmd.ExecuteNonQueryAsync();

		// Create a second table so the DB has multiple user tables (mirrors production reality).
		cmd.CommandText = """
		                  CREATE TABLE "Orders" (
		                      "Id"          INTEGER PRIMARY KEY AUTOINCREMENT,
		                      "Description" TEXT NOT NULL
		                  )
		                  """;
		await cmd.ExecuteNonQueryAsync();

		// Create a fake migration history table with two entries so
		// GetMigrationHistoryAsync() exercises multi-row iteration and ORDER BY.
		cmd.CommandText = """
		                  CREATE TABLE "__EFMigrationsHistory" (
		                      "MigrationId"    TEXT NOT NULL PRIMARY KEY,
		                      "ProductVersion" TEXT NOT NULL
		                  )
		                  """;
		await cmd.ExecuteNonQueryAsync();

		cmd.CommandText = """
		                  INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
		                  VALUES ('20260101000000_Init', '10.0.0'), ('20260201000000_AddOrders', '10.0.0')
		                  """;
		await cmd.ExecuteNonQueryAsync();

		// Seed rows if provided.
		if (seedRows != null)
		{
			foreach ((int id, string name) in seedRows)
			{
				cmd.CommandText = $"""INSERT INTO "{tableName}" ("Id", "Name") VALUES ({id}, '{name}')""";
				await cmd.ExecuteNonQueryAsync();
			}
		}
	}

	/// <summary>
	/// Creates a <see cref="TableSnapshot"/> backed by the given rows.
	/// </summary>
	/// <param name="tableName">Name of the target table.</param>
	/// <param name="rows">The rows to include in the snapshot. Each tuple is <c>(id, name)</c>.</param>
	/// <returns>A snapshot with <c>Id</c> (INTEGER) and <c>Name</c> (TEXT) columns.</returns>
	private static TableSnapshot CreateSnapshot(string tableName, (int Id, string Name)[] rows) => new()
	{
		Name = tableName,
		Columns =
		[
			new ColumnDefinition { Name = "Id", DbType = "INTEGER" },
			new ColumnDefinition { Name = "Name", DbType = "TEXT" }
		],
		EstimatedRowCount = rows.Length,
		Rows = ToAsyncEnumerable(rows)
	};

	/// <summary>
	/// Creates an empty <see cref="TableSnapshot"/>.
	/// </summary>
	/// <param name="tableName">Name of the target table.</param>
	/// <returns>A snapshot with no rows.</returns>
	private static TableSnapshot CreateEmptySnapshot(string tableName) => CreateSnapshot(tableName, []);

	/// <summary>
	/// Converts an array of <c>(Id, Name)</c> tuples into an <see cref="IAsyncEnumerable{T}"/> of row arrays.
	/// </summary>
	private static async IAsyncEnumerable<object?[]> ToAsyncEnumerable((int Id, string Name)[] rows)
	{
		foreach ((int id, string name) in rows)
		{
			yield return [id, name];
		}

		await Task.CompletedTask;
	}

	/// <summary>
	/// Converts raw row arrays into an <see cref="IAsyncEnumerable{T}"/>.
	/// Used for tests requiring non-standard column layouts (e.g., FK violation tests).
	/// </summary>
	private static async IAsyncEnumerable<object?[]> ToAsyncEnumerable(object?[][] rows)
	{
		foreach (object?[] row in rows)
		{
			yield return row;
		}

		await Task.CompletedTask;
	}

	/// <summary>
	/// Reads all rows from the given table in the target database.
	/// </summary>
	/// <param name="dbPath">Path to the SQLite database file.</param>
	/// <param name="tableName">Table to read from.</param>
	/// <returns>A list of <c>(Id, Name)</c> tuples.</returns>
	private static async Task<List<(int Id, string Name)>> ReadAllRowsAsync(string dbPath, string tableName)
	{
		var result = new List<(int, string)>();
		await using var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		await using SqliteCommand cmd = conn.CreateCommand();
		cmd.CommandText = $"""
		                   SELECT "Id", "Name"
		                   FROM "{tableName}"
		                   ORDER BY "Id"
		                   """;

		await using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			result.Add((reader.GetInt32(0), reader.GetString(1)));
		}

		return result;
	}

	/// <summary>
	/// Checks whether the checkpoint table exists in the target database.
	/// </summary>
	/// <param name="dbPath">Path to the SQLite database file.</param>
	/// <returns><see langword="true"/> if the table exists.</returns>
	private static async Task<bool> CheckpointTableExistsAsync(string dbPath)
	{
		await using var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		await using SqliteCommand cmd = conn.CreateCommand();
		cmd.CommandText = $"""
		                   SELECT 1
		                   FROM sqlite_master
		                   WHERE type = 'table'
		                     AND name = '{CheckpointTableName}'
		                   """;
		object? result = await cmd.ExecuteScalarAsync();
		return result != null && result != DBNull.Value;
	}

	/// <summary>
	/// Reads checkpoint records from the target database.
	/// </summary>
	/// <param name="dbPath">Path to the SQLite database file.</param>
	/// <returns>A list of checkpoint records as tuples.</returns>
	private static async Task<List<(string ShuttleId, string TableName, int ChunksCompleted, long TotalRows)>>
		ReadCheckpointsAsync(string dbPath)
	{
		var result = new List<(string, string, int, long)>();
		await using var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		await using SqliteCommand cmd = conn.CreateCommand();
		cmd.CommandText = $"""
		                   SELECT "shuttle_id", "table_name", "chunks_completed", "total_rows_imported"
		                   FROM "{CheckpointTableName}"
		                   """;

		await using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			result.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3)));
		}

		return result;
	}

	/// <summary>
	/// Inserts a checkpoint record directly into the target database (for resume/mismatch tests).
	/// </summary>
	private static async Task InsertCheckpointAsync(
		string dbPath,
		string shuttleId,
		string tableName,
		int    chunksCompleted,
		long   totalRowsImported)
	{
		await using var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		await using SqliteCommand cmd = conn.CreateCommand();
		cmd.CommandText = $"""
		                   CREATE TABLE IF NOT EXISTS "{CheckpointTableName}" (
		                       "shuttle_id"          TEXT    NOT NULL,
		                       "table_name"          TEXT    NOT NULL,
		                       "chunks_completed"    INTEGER NOT NULL,
		                       "total_rows_imported" INTEGER NOT NULL,
		                       "started_utc"         TEXT    NOT NULL,
		                       "updated_utc"         TEXT    NOT NULL,
		                       PRIMARY KEY ("shuttle_id", "table_name")
		                   )
		                   """;
		await cmd.ExecuteNonQueryAsync();

		cmd.CommandText = $"""
		                   INSERT INTO "{CheckpointTableName}"
		                       ("shuttle_id", "table_name", "chunks_completed", "total_rows_imported", "started_utc", "updated_utc")
		                   VALUES (@sid, @tn, @cc, @tr, @now, @now)
		                   """;
		cmd.Parameters.AddWithValue("@sid", shuttleId);
		cmd.Parameters.AddWithValue("@tn", tableName);
		cmd.Parameters.AddWithValue("@cc", chunksCompleted);
		cmd.Parameters.AddWithValue("@tr", totalRowsImported);
		cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
		await cmd.ExecuteNonQueryAsync();
	}

	/// <summary>
	/// Generates N rows of test data as <c>(Id, Name)</c> tuples.
	/// </summary>
	/// <param name="count">Number of rows to generate.</param>
	/// <param name="startId">The starting ID value (inclusive).</param>
	/// <returns>An array of tuples suitable for <see cref="CreateSnapshot"/>.</returns>
	private static (int Id, string Name)[] GenerateRows(int count, int startId = 1)
	{
		var rows = new (int, string)[count];
		for (int i = 0; i < count; i++)
		{
			rows[i] = (startId + i, $"Row_{startId + i}");
		}

		return rows;
	}

	/// <summary>
	/// Creates a SQLite database with a parent/child FK relationship for FK-ordering and FK-violation tests.
	/// </summary>
	/// <remarks>
	/// Schema: <c>Parents(Id PK AUTOINCREMENT, Name TEXT)</c> →
	/// <c>Children(Id PK AUTOINCREMENT, ParentId FK → Parents, Label TEXT)</c>.
	/// FK enforcement is enabled. The <c>__EFMigrationsHistory</c> table is included for
	/// <see cref="SqliteImportWriter.GetMigrationHistoryAsync"/> compatibility.
	/// </remarks>
	private static async Task CreateFkTestDatabaseAsync(string dbPath)
	{
		await using var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		await using SqliteCommand cmd = conn.CreateCommand();

		cmd.CommandText = "PRAGMA foreign_keys = ON;";
		await cmd.ExecuteNonQueryAsync();

		cmd.CommandText = """
		                  CREATE TABLE "Parents" (
		                      "Id"   INTEGER PRIMARY KEY AUTOINCREMENT,
		                      "Name" TEXT NOT NULL
		                  )
		                  """;
		await cmd.ExecuteNonQueryAsync();

		cmd.CommandText = """
		                  CREATE TABLE "Children" (
		                      "Id"       INTEGER PRIMARY KEY AUTOINCREMENT,
		                      "ParentId" INTEGER NOT NULL REFERENCES "Parents"("Id"),
		                      "Label"    TEXT NOT NULL
		                  )
		                  """;
		await cmd.ExecuteNonQueryAsync();

		cmd.CommandText = """
		                  CREATE TABLE "__EFMigrationsHistory" (
		                      "MigrationId"    TEXT NOT NULL PRIMARY KEY,
		                      "ProductVersion" TEXT NOT NULL
		                  )
		                  """;
		await cmd.ExecuteNonQueryAsync();

		cmd.CommandText = """
		                  INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
		                  VALUES ('20260101000000_Init', '10.0.0')
		                  """;
		await cmd.ExecuteNonQueryAsync();
	}

	/// <summary>
	/// Creates a SQLite database with a composite-PK join table for composite key import tests.
	/// </summary>
	/// <remarks>
	/// Schema: <c>Memberships(ConversationId INTEGER, ParticipantId INTEGER, JoinedAtUtc TEXT)</c>
	/// with <c>PRIMARY KEY (ConversationId, ParticipantId)</c>. No <c>AUTOINCREMENT</c>.
	/// </remarks>
	private static async Task CreateCompositePkTestDatabaseAsync(string dbPath)
	{
		await using var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		await using SqliteCommand cmd = conn.CreateCommand();

		cmd.CommandText = """
		                  CREATE TABLE "Memberships" (
		                      "ConversationId" INTEGER NOT NULL,
		                      "ParticipantId"  INTEGER NOT NULL,
		                      "JoinedAtUtc"    TEXT    NOT NULL,
		                      PRIMARY KEY ("ConversationId", "ParticipantId")
		                  )
		                  """;
		await cmd.ExecuteNonQueryAsync();

		cmd.CommandText = """
		                  CREATE TABLE "__EFMigrationsHistory" (
		                      "MigrationId"    TEXT NOT NULL PRIMARY KEY,
		                      "ProductVersion" TEXT NOT NULL
		                  )
		                  """;
		await cmd.ExecuteNonQueryAsync();

		cmd.CommandText = """
		                  INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
		                  VALUES ('20260101000000_Init', '10.0.0')
		                  """;
		await cmd.ExecuteNonQueryAsync();
	}
}
