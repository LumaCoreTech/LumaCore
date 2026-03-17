// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;

using LumaCore.Core.IO;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Models;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleReaderTests
{
	/// <summary>
	/// Creates a valid shuttle file and then corrupts it via SQL by redirecting the <c>Users</c> table's
	/// root page to the same page as <c>__Shuttle_BackupInfo</c>. This causes
	/// <c>PRAGMA integrity_check</c> to detect the page being referenced by two B-trees while leaving
	/// <c>__Shuttle_BackupInfo</c> readable for <see cref="SqliteShuttleReader.InitializeAsync"/>.
	/// </summary>
	/// <remarks>
	/// The corruption is introduced at the schema level (via <c>PRAGMA writable_schema</c>) rather than
	/// through binary file manipulation. This is more reliable because <c>PRAGMA integrity_check</c>
	/// reports shared-page errors as text rows, whereas aggressive binary corruption can cause
	/// <see cref="SqliteException"/> to be thrown before the error rows are returned.
	/// </remarks>
	/// <param name="filePath">The file path for the shuttle file.</param>
	private static async Task CreateCorruptedShuttleFileAsync(string filePath)
	{
		await CreateValidShuttleFileAsync(filePath).ConfigureAwait(false);

		string connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = filePath,
			Pooling = false
		}.ConnectionString;

		var connection = new SqliteConnection(connectionString);
		try
		{
			await connection.OpenAsync().ConfigureAwait(false);

			// Read the root page of BackupInfo.
			SqliteCommand readCmd = connection.CreateCommand();
			long backupInfoRootPage;
			try
			{
				readCmd.CommandText = """SELECT rootpage FROM sqlite_master WHERE name = '__Shuttle_BackupInfo'""";
				backupInfoRootPage = (long)(await readCmd.ExecuteScalarAsync().ConfigureAwait(false))!;
			}
			finally
			{
				await readCmd.DisposeAsync().ConfigureAwait(false);
			}

			// Point Users to the same root page as BackupInfo, creating a double-referenced page
			// that PRAGMA integrity_check will detect as "page X is used by multiple B-trees".
			SqliteCommand writeCmd = connection.CreateCommand();
			try
			{
				writeCmd.CommandText = $"""
				                        PRAGMA writable_schema = ON;
				                        UPDATE sqlite_master SET rootpage = {backupInfoRootPage} WHERE name = 'Users';
				                        PRAGMA writable_schema = OFF;
				                        """;
				await writeCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
			finally
			{
				await writeCmd.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a minimal finalized shuttle file with no user tables, no migrations, and no custom metadata.
	/// Only the completion markers written by <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/>
	/// are present.
	/// </summary>
	/// <param name="filePath">The file path for the shuttle file.</param>
	private static async Task CreateEmptyShuttleFileAsync(string filePath)
	{
		var writer = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
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

	/// <summary>
	/// Creates an initialized <see cref="SqliteShuttleReader"/> for a previously created shuttle file.
	/// The caller must dispose the returned reader.
	/// </summary>
	/// <param name="filePath">The path to the shuttle file.</param>
	private static async Task<SqliteShuttleReader> CreateInitializedReaderAsync(string filePath)
	{
		var reader = new SqliteShuttleReader(filePath, NullLogger.Instance);
		await reader.InitializeAsync().ConfigureAwait(false);
		return reader;
	}

	/// <summary>
	/// Creates a valid shuttle file with a custom <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata
	/// value, allowing tests to control the exact timestamp string stored in the file.
	/// </summary>
	/// <param name="filePath">The file path for the shuttle file.</param>
	/// <param name="createdUtcValue">
	/// The raw string value to store under the <see cref="SqliteShuttleSchema.CreatedUtcKey"/> key.
	/// </param>
	/// <remarks>
	/// Raw SQL is required because <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/> always
	/// writes <c>CreatedUtc</c> as <c>TimeProvider.GetUtcNow().ToString("o")</c> — a valid round-trip
	/// timestamp. There is no writer API to inject an arbitrary string (e.g., <c>"not-a-date"</c>).
	/// This also keeps the test decoupled from the writer's internal execution order.
	/// </remarks>
	private static async Task CreateShuttleFileWithCustomCreatedUtcAsync(string filePath, string createdUtcValue)
	{
		// First create a valid shuttle file.
		await CreateEmptyShuttleFileAsync(filePath).ConfigureAwait(false);

		// Then overwrite the CreatedUtc value directly via raw SQLite.
		string connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = filePath,
			Pooling = false
		}.ConnectionString;

		var connection = new SqliteConnection(connectionString);
		try
		{
			await connection.OpenAsync().ConfigureAwait(false);
			SqliteCommand cmd = connection.CreateCommand();
			try
			{
				cmd.CommandText =
					"""
					INSERT OR REPLACE INTO "__Shuttle_BackupInfo" ("key", "value")
					VALUES ('CreatedUtc', @value)
					""";
				cmd.Parameters.AddWithValue("@value", createdUtcValue);
				await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
			finally
			{
				await cmd.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a shuttle file that has the backup info table but is missing the
	/// <see cref="SqliteShuttleSchema.ExportStatusKey"/> marker. This simulates an incomplete export
	/// where metadata was written but <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/>
	/// was never called.
	/// </summary>
	/// <param name="filePath">The file path for the shuttle file.</param>
	/// <remarks>
	/// Raw SQL is required because <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/> writes
	/// all four system markers (<c>ExportStatus</c>, <c>ShuttleFormatVersion</c>, <c>ShuttleId</c>,
	/// <c>CreatedUtc</c>) atomically. The writer has no API to produce a partial state where only some
	/// markers are present. This also keeps the test decoupled from the writer's internal execution order.
	/// </remarks>
	private static async Task CreateShuttleFileWithMissingStatusMarkerAsync(string filePath)
	{
		// Create the file with only a backup info table containing the version key but no status key.
		string connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = filePath,
			Pooling = false
		}.ConnectionString;

		var connection = new SqliteConnection(connectionString);
		try
		{
			await connection.OpenAsync().ConfigureAwait(false);
			SqliteCommand cmd = connection.CreateCommand();
			try
			{
				cmd.CommandText =
					"""
					CREATE TABLE "__Shuttle_BackupInfo" ("key" TEXT PRIMARY KEY, "value" TEXT NOT NULL);
					INSERT INTO "__Shuttle_BackupInfo" ("key", "value") VALUES ('ShuttleFormatVersion', '1');
					""";
				await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
			finally
			{
				await cmd.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a SQLite database with a dummy table but no
	/// <see cref="SqliteShuttleSchema.BackupInfoTableName"/> table, simulating an export that was
	/// interrupted before <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/> could
	/// create the metadata table.
	/// </summary>
	/// <param name="filePath">The file path for the shuttle file.</param>
	/// <remarks>
	/// Using the writer with only <see cref="SqliteShuttleWriter.InitializeAsync"/> (no
	/// <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/>) would produce a valid but
	/// completely empty SQLite database. A dummy table is added via raw SQL to simulate a more realistic
	/// partial-write scenario where user tables exist but the backup info table does not.
	/// This also keeps the test decoupled from the writer's internal execution order.
	/// </remarks>
	private static async Task CreateShuttleFileWithNoBackupInfoTableAsync(string filePath)
	{
		string connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = filePath,
			Pooling = false
		}.ConnectionString;

		var connection = new SqliteConnection(connectionString);
		try
		{
			await connection.OpenAsync().ConfigureAwait(false);

			// Create a dummy table so the file is a valid SQLite database but has no backup info table.
			SqliteCommand cmd = connection.CreateCommand();
			try
			{
				cmd.CommandText = """CREATE TABLE "DummyTable" ("Id" INTEGER PRIMARY KEY);""";
				await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
			finally
			{
				await cmd.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a shuttle file where the <see cref="SqliteShuttleSchema.ShuttleFormatVersionKey"/> marker
	/// has an unsupported version number.
	/// </summary>
	/// <remarks>
	/// Raw SQL is required because <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/> always
	/// writes <see cref="SqliteShuttleSchema.CurrentShuttleFormatVersion"/>. There is no writer API to
	/// produce a file with an arbitrary version number. This also keeps the test decoupled from the
	/// writer's internal execution order.
	/// </remarks>
	/// <param name="filePath">The file path for the shuttle file.</param>
	private static async Task CreateShuttleFileWithWrongFormatVersionAsync(string filePath)
	{
		string connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = filePath,
			Pooling = false
		}.ConnectionString;

		var connection = new SqliteConnection(connectionString);
		try
		{
			await connection.OpenAsync().ConfigureAwait(false);
			SqliteCommand cmd = connection.CreateCommand();
			try
			{
				cmd.CommandText =
					"""
					CREATE TABLE "__Shuttle_BackupInfo" ("key" TEXT PRIMARY KEY, "value" TEXT NOT NULL);
					INSERT INTO "__Shuttle_BackupInfo" ("key", "value") VALUES ('ExportStatus', 'Completed');
					INSERT INTO "__Shuttle_BackupInfo" ("key", "value") VALUES ('ShuttleFormatVersion', '999');
					""";
				await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
			finally
			{
				await cmd.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a temporary directory and returns a shuttle file path inside it.
	/// The caller owns the <see cref="TemporaryFolder"/> and must dispose it after the test.
	/// </summary>
	/// <param name="prefix">A human-readable prefix for the temporary directory name.</param>
	private static (TemporaryFolder Folder, string FilePath) CreateTempShuttleFilePath(string prefix = "reader-test")
	{
		var folder = new TemporaryFolder(prefix);
		return (folder, folder.GetFilePath("test.shuttle.sqlite"));
	}

	/// <summary>
	/// Creates a fully valid, finalized shuttle file using <see cref="SqliteShuttleWriter"/> and returns
	/// the path. The file contains a <c>Users</c> table with two rows and migration history.
	/// </summary>
	/// <param name="filePath">The file path for the shuttle file.</param>
	private static async Task CreateValidShuttleFileAsync(string filePath)
	{
		var writer = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
		try
		{
			await writer.InitializeAsync().ConfigureAwait(false);

			var table = new TableSnapshot
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

			await writer.WriteTableAsync(table, logger: null, progress: null, 1, 1).ConfigureAwait(false);

			await writer.WriteMigrationHistoryAsync(
				[
					new MigrationInfo("20260101000000_Initial", "10.0.0"),
					new MigrationInfo("20260201000000_AddMessages", "10.0.0")
				])
				.ConfigureAwait(false);

			await writer.WriteMetadataAsync(
					new Dictionary<string, string>
					{
						["source_provider"] = "sqlite",
						["export_time"] = "2026-01-01T00:00:00Z"
					})
				.ConfigureAwait(false);

			await writer.FinalizeAsync().ConfigureAwait(false);
		}
		finally
		{
			await writer.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asserts that <paramref name="metadata"/> contains all four system markers written by
	/// <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/> with syntactically valid values:
	/// <see cref="SqliteShuttleSchema.ExportStatusKey"/> equals <c>"Completed"</c>,
	/// <see cref="SqliteShuttleSchema.ShuttleFormatVersionKey"/> equals <c>"1"</c>,
	/// <see cref="SqliteShuttleSchema.ShuttleIdKey"/> is a valid <see cref="Guid"/>, and
	/// <see cref="SqliteShuttleSchema.CreatedUtcKey"/> is a valid ISO 8601 round-trip
	/// <see cref="DateTimeOffset"/>.
	/// </summary>
	/// <param name="metadata">The metadata dictionary returned by <see cref="SqliteShuttleReader.GetMetadataAsync"/>.</param>
	private static void AssertSystemMarkers(Dictionary<string, string> metadata)
	{
		Assert.Equal("Completed", metadata["ExportStatus"]);
		Assert.Equal("1", metadata["ShuttleFormatVersion"]);

		Assert.True(
			metadata.ContainsKey("ShuttleId") && Guid.TryParse(metadata["ShuttleId"], out Guid _),
			$"Expected ShuttleId to be a valid GUID but was: '{(metadata.TryGetValue("ShuttleId", out string? sid) ? sid : "<missing>")}'.");

		Assert.True(
			metadata.ContainsKey("CreatedUtc")
			&& DateTimeOffset.TryParseExact(
				metadata["CreatedUtc"],
				"O",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out DateTimeOffset _),
			$"Expected CreatedUtc to be a valid ISO 8601 round-trip timestamp but was: '{(metadata.TryGetValue("CreatedUtc", out string? ts) ? ts : "<missing>")}'.");
	}

	/// <summary>
	/// Generates two test rows for the <c>Users</c> table: Alice (with email) and Bob (without email).
	/// </summary>
	private static async IAsyncEnumerable<object?[]> GenerateUserRows()
	{
		await Task.CompletedTask;
		yield return [1L, "Alice", "alice@test.com"];
		yield return [2L, "Bob", null];
	}
}
