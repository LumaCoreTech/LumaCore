// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Models;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Providers;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.DataPort;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.Services;

// Integration test for CreateShuttleBackupAsync(): exercises the complete export pipeline from a
// populated in-memory SQLite source to a shuttle file on disk, then reads back the shuttle file
// to verify data integrity.
//
// Uses real implementations throughout (DataPortService, SqliteProviderOperations, SqliteShuttleWriter)
// because the SUT constructs the shuttle writer internally — no seam for test substitution.
public sealed partial class DatabaseMaintenanceServiceTests
{
	/// <summary>
	/// Verifies that <see cref="DatabaseMaintenanceService.CreateShuttleBackupAsync"/> creates a valid
	/// shuttle file containing the exported tables, migration history, and metadata from the source database.
	/// </summary>
	/// <remarks>
	/// This is an integration test that exercises the full pipeline: source database →
	/// <see cref="DataPortService.RunExportAsync"/> → shuttle file. The shuttle file is then read back via
	/// <see cref="SqliteShuttleReader"/> to verify data integrity.
	/// </remarks>
	[Fact]
	[Trait("Category", "DataPort")]
	public async Task CreateShuttleBackupAsync_WhenSourceHasData_CreatesValidShuttleFile()
	{
		// Arrange — shared in-memory SQLite with test data acts as the source database.
		string databaseName = $"maintenance_{Guid.NewGuid():N}";
		string connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
		SqliteConnection keeper = await CreatePopulatedDatabaseAsync(connectionString);
		try
		{
			using var tempDir = new TemporaryFolder("shuttle-backup");

			// Use milliseconds (123) to verify the filename includes the full -fff timestamp segment.
			var expectedUtc = new DateTimeOffset(2026, 6, 15, 10, 30, 0, 123, TimeSpan.Zero);
			var fakeTime = new FakeTimeProvider(expectedUtc);

			var options = new DatabaseOptions
			{
				ConnectionString = connectionString,
				AutoMigration = { BackupDirectory = tempDir.Path }
			};

			var sut = new DatabaseMaintenanceService(
				NullLogger<DatabaseMaintenanceService>.Instance,
				Options.Create(options),
				new DataPortService(NullLogger<DataPortService>.Instance),
				new SqliteProviderOperations(),
				fakeTime);

			// Act
			string backupPath = await sut.CreateShuttleBackupAsync();

			// Assert — file location and naming.
			Assert.True(File.Exists(backupPath), $"Backup file should exist at: {backupPath}");
			Assert.StartsWith(tempDir.Path, backupPath);
			Assert.EndsWith(SqliteShuttleSchema.FileExtension, backupPath);

			// Filename contains the deterministic timestamp from FakeTimeProvider (including milliseconds).
			string fileName = Path.GetFileName(backupPath);
			Assert.StartsWith("lumacore-", fileName);
			Assert.Contains("20260615-103000-123", fileName);

			// Assert — shuttle file content via SqliteShuttleReader.
			var reader = new SqliteShuttleReader(backupPath, NullLogger.Instance);
			try
			{
				await reader.InitializeAsync();

				// Tables exported from source (excluding __EFMigrationsHistory which is stored separately).
				List<string> tableNames = await reader.GetTableNamesAsync();
				Assert.Equal(2, tableNames.Count);
				Assert.Contains("Users", tableNames);
				Assert.Contains("Messages", tableNames);

				// Migration history preserved.
				List<MigrationInfo> migrations = await reader.GetMigrationHistoryAsync();
				Assert.Equal(2, migrations.Count);
				Assert.Equal("20260101000000_Initial", migrations[0].MigrationId);
				Assert.Equal("10.0.0", migrations[0].ProductVersion);
				Assert.Equal("20260201000000_AddMessages", migrations[1].MigrationId);
				Assert.Equal("10.0.0", migrations[1].ProductVersion);

				// Table data preserved — spot-check Users table.
				TableSnapshot usersTable = await reader.ReadTableAsync("Users");
				Assert.Equal("Users", usersTable.Name);
				Assert.Equal(3, usersTable.Columns.Count);

				var rows = new List<object?[]>();
				await foreach (object?[] row in usersTable.Rows)
				{
					rows.Add(row);
				}

				Assert.Equal(2, rows.Count);
				Assert.Equal(1L, rows[0][0]);
				Assert.Equal("Alice", rows[0][1]);
				Assert.Equal("alice@test.com", rows[0][2]);
				Assert.Equal(2L, rows[1][0]);
				Assert.Equal("Bob", rows[1][1]);
				Assert.Null(rows[1][2]); // NULL value preserved

				// Metadata contains source provider and completion markers.
				Dictionary<string, string> metadata = await reader.GetMetadataAsync();
				Assert.Equal("SqliteExportReader", metadata[DataPortService.SourceProviderKey]);
				Assert.Equal(
					SqliteShuttleSchema.CompletedValue,
					metadata[SqliteShuttleSchema.ExportStatusKey]);

				// CreatedUtc matches FakeTimeProvider.
				DateTimeOffset? createdUtc = await reader.GetCreatedUtcAsync();
				Assert.Equal(expectedUtc, createdUtc);
			}
			finally
			{
				await reader.DisposeAsync();
			}
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	/// <summary>
	/// Creates a shared in-memory SQLite database populated with test tables and data,
	/// and returns the keeper connection that must stay open for the database to persist.
	/// </summary>
	/// <param name="connectionString">The shared in-memory connection string.</param>
	/// <returns>An open keeper connection. Dispose it after the test completes.</returns>
	/// <remarks>
	/// The schema mirrors the one in <see cref="SqliteExportReaderTests"/> — a simplified synthetic schema
	/// (<c>Users</c>, <c>Messages</c>) plus <c>__EFMigrationsHistory</c> to exercise the full export pipeline.
	/// </remarks>
	private static async Task<SqliteConnection> CreatePopulatedDatabaseAsync(string connectionString)
	{
		var keeper = new SqliteConnection(connectionString);
		await keeper.OpenAsync().ConfigureAwait(false);

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
			await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		return keeper;
	}
}
