// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Models;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleWriterTests
{
	/// <summary>
	/// Creates a temporary directory and returns a shuttle file path inside it.
	/// </summary>
	/// <param name="prefix">A human-readable prefix for the temporary directory name.</param>
	/// <returns>
	/// A tuple of the <see cref="TemporaryFolder"/> (caller must dispose after the test) and the shuttle file path within it.
	/// </returns>
	private static (TemporaryFolder Folder, string FilePath) CreateTempShuttleFile(string prefix = "writer-test")
	{
		var folder = new TemporaryFolder(prefix);
		return (folder, folder.GetFilePath("test.shuttle.sqlite"));
	}

	/// <summary>
	/// Creates an initialized <see cref="SqliteShuttleWriter"/> for a new shuttle file.
	/// </summary>
	/// <param name="filePath">The file path for the shuttle file.</param>
	/// <returns>An initialized writer ready for write operations. The caller must dispose the returned instance.</returns>
	private static async Task<SqliteShuttleWriter> CreateInitializedWriterAsync(string filePath)
	{
		var writer = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
		await writer.InitializeAsync().ConfigureAwait(false);
		return writer;
	}

	/// <summary>
	/// Creates a simple <see cref="TableSnapshot"/> with a <c>Users</c> table containing three columns
	/// and two rows for testing <see cref="SqliteShuttleWriter.WriteTableAsync"/>.
	/// </summary>
	/// <returns>A <see cref="TableSnapshot"/> with <c>Id</c>, <c>Name</c>, and <c>Email</c> columns and two data rows.</returns>
	private static TableSnapshot CreateUsersTableSnapshot()
	{
		return new TableSnapshot
		{
			Name = "Users",
			Columns =
			[
				new ColumnDefinition
				{
					Name = "Id",
					DbType = "INTEGER",
					ShuttleStorageType = "INTEGER"
				},
				new ColumnDefinition
				{
					Name = "Name",
					DbType = "TEXT",
					ShuttleStorageType = "TEXT"
				},
				new ColumnDefinition
				{
					Name = "Email",
					DbType = "TEXT",
					ShuttleStorageType = "TEXT"
				}
			],
			EstimatedRowCount = 2,
			Rows = GenerateUserRows()
		};
	}

	/// <summary>
	/// Generates two test rows for the <c>Users</c> table: Alice (with email) and Bob (without email).
	/// </summary>
	/// <returns>An async sequence of two rows: Alice (with email) and Bob (without email).</returns>
	private static async IAsyncEnumerable<object?[]> GenerateUserRows()
	{
		await Task.CompletedTask;
		yield return [1L, "Alice", "alice@test.com"];
		yield return [2L, "Bob", null];
	}

	/// <summary>
	/// Opens a read-only raw <see cref="SqliteConnection"/> to verify contents written by the writer.
	/// </summary>
	/// <param name="filePath">The file path to the shuttle file.</param>
	/// <returns>An open read-only <see cref="SqliteConnection"/>. The caller must dispose the returned instance.</returns>
	private static async Task<SqliteConnection> OpenRawReadConnectionAsync(string filePath)
	{
		string connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = filePath,
			Mode = SqliteOpenMode.ReadOnly,
			Pooling = false
		}.ConnectionString;

		var connection = new SqliteConnection(connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		return connection;
	}

	/// <summary>
	/// Reads all rows from a table via raw SQLite and returns them as a list of object arrays.
	/// </summary>
	/// <param name="connection">An open SQLite connection.</param>
	/// <param name="tableName">The name of the table to read.</param>
	/// <returns>A list of object arrays representing all rows, with <see cref="DBNull"/> values converted to <see langword="null"/>.</returns>
	private static async Task<List<object?[]>> ReadAllRowsAsync(SqliteConnection connection, string tableName)
	{
		var rows = new List<object?[]>();
		SqliteCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = $"""SELECT * FROM "{tableName}" """;
			SqliteDataReader reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
			try
			{
				int fieldCount = reader.FieldCount;
				while (await reader.ReadAsync().ConfigureAwait(false))
				{
					object?[] row = new object?[fieldCount];
					for (int i = 0; i < fieldCount; i++)
					{
						object value = reader.GetValue(i);
						row[i] = value is DBNull ? null : value;
					}
					rows.Add(row);
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		return rows;
	}

	/// <summary>
	/// Reads a single metadata value from the <c>__Shuttle_BackupInfo</c> table via raw SQLite.
	/// </summary>
	/// <param name="connection">An open SQLite connection.</param>
	/// <param name="key">The metadata key.</param>
	/// <returns>The metadata value, or <see langword="null"/> if the key does not exist.</returns>
	private static async Task<string?> ReadMetadataValueAsync(SqliteConnection connection, string key)
	{
		SqliteCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = """SELECT "value" FROM "__Shuttle_BackupInfo" WHERE "key" = @key""";
			cmd.Parameters.AddWithValue("@key", key);
			object? result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
			return result is null or DBNull ? null : (string)result;
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Returns the names of all user-defined tables in the SQLite database, excluding internal
	/// <c>sqlite_*</c> tables.
	/// </summary>
	/// <param name="connection">An open SQLite connection.</param>
	/// <returns>A list of table names in the order returned by <c>sqlite_master</c>.</returns>
	private static async Task<List<string>> GetAllTableNamesAsync(SqliteConnection connection)
	{
		var tables = new List<string>();
		SqliteCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = """SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name""";
			SqliteDataReader reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync().ConfigureAwait(false))
				{
					tables.Add(reader.GetString(0));
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		return tables;
	}

	/// <summary>
	/// Checks whether a table exists in the SQLite database via raw SQL.
	/// </summary>
	/// <param name="connection">An open SQLite connection.</param>
	/// <param name="tableName">The table name to check.</param>
	/// <returns><see langword="true"/> if the table exists; otherwise, <see langword="false"/>.</returns>
	private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName)
	{
		SqliteCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name";
			cmd.Parameters.AddWithValue("@name", tableName);
			object? result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
			return result is not null and not DBNull;
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}
}
