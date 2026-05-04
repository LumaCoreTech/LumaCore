// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;

using LumaCore.Core.IO;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Models;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Integration tests that verify the full write-read roundtrip of the LumaCore Shuttle format
/// using <see cref="SqliteShuttleWriter"/> and <see cref="SqliteShuttleReader"/> with temporary files.
/// </summary>
[Trait("Category", "DataPort")]
public sealed class SqliteShuttleRoundtripTests
{
	/// <summary>
	/// Verifies the full write → finalize → read roundtrip: table schema, row data, migration history,
	/// and metadata are preserved correctly through the shuttle format.
	/// </summary>
	[Fact]
	public async Task Roundtrip_WriteAndRead_PreservesAllData()
	{
		using var tempDir = new TemporaryFolder("shuttle-roundtrip");
		string filePath = Path.Combine(tempDir.Path, "test.shuttle.sqlite");
		var expectedUtc = new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
		var fakeTime = new FakeTimeProvider(expectedUtc);

		// ===== WRITE PHASE =====
		var writer = new SqliteShuttleWriter(filePath, NullLogger.Instance, fakeTime);
		try
		{
			await writer.InitializeAsync();

			// Write a table with columns and rows
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
				Rows = GenerateRows()
			};

			await writer.WriteTableAsync(table, logger: null, progress: null, 1, 1);

			// Write migration history
			await writer.WriteMigrationHistoryAsync(
			[
				new MigrationInfo("20260101000000_Initial", "10.0.0"),
				new MigrationInfo("20260201000000_AddMessages", "10.0.0")
			]);

			// Write metadata
			await writer.WriteMetadataAsync(
				new Dictionary<string, string>
				{
					["source_provider"] = "sqlite",
					["export_time"] = "2026-01-15T12:00:00Z"
				});

			// Finalize — writes completion marker, runs integrity check
			await writer.FinalizeAsync();
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// ===== READ PHASE =====
		var reader = new SqliteShuttleReader(filePath, NullLogger.Instance);
		try
		{
			await reader.InitializeAsync();

			// Verify table names
			List<string> tableNames = await reader.GetTableNamesAsync();
			Assert.Contains("Users", tableNames);

			// Verify table data
			TableSnapshot readTable = await reader.ReadTableAsync("Users");
			Assert.Equal("Users", readTable.Name);
			Assert.Equal(3, readTable.Columns.Count);
			Assert.Equal("Id", readTable.Columns[0].Name);
			Assert.Equal("Name", readTable.Columns[1].Name);
			Assert.Equal("Email", readTable.Columns[2].Name);
			Assert.Equal(2, readTable.EstimatedRowCount);

			var rows = new List<object?[]>();
			await foreach (object?[] row in readTable.Rows)
			{
				rows.Add(row);
			}

			Assert.Equal(2, rows.Count);
			Assert.Equal(1L, rows[0][0]);
			Assert.Equal("Alice", rows[0][1]);
			Assert.Equal("alice@test.com", rows[0][2]);
			Assert.Equal(2L, rows[1][0]);
			Assert.Equal("Bob", rows[1][1]);
			Assert.Null(rows[1][2]); // NULL preserved

			// Verify migrations
			List<MigrationInfo> migrations = await reader.GetMigrationHistoryAsync();
			Assert.Equal(2, migrations.Count);
			Assert.Equal("20260101000000_Initial", migrations[0].MigrationId);
			Assert.Equal("10.0.0", migrations[0].ProductVersion);
			Assert.Equal("20260201000000_AddMessages", migrations[1].MigrationId);
			Assert.Equal("10.0.0", migrations[1].ProductVersion);

			// Verify metadata
			Dictionary<string, string> metadata = await reader.GetMetadataAsync();
			Assert.Equal("sqlite", metadata["source_provider"]);
			Assert.Equal("2026-01-15T12:00:00Z", metadata["export_time"]);

			// Verify system markers were written by FinalizeAsync
			Assert.Equal(
				SqliteShuttleSchema.CompletedValue,
				metadata[SqliteShuttleSchema.ExportStatusKey]);
			Assert.Equal(
				SqliteShuttleSchema.CurrentShuttleFormatVersion.ToString(CultureInfo.InvariantCulture),
				metadata[SqliteShuttleSchema.ShuttleFormatVersionKey]);
			Assert.True(metadata.ContainsKey(SqliteShuttleSchema.ShuttleIdKey));
			Assert.False(string.IsNullOrWhiteSpace(metadata[SqliteShuttleSchema.ShuttleIdKey]));

			// Verify CreatedUtc — FakeTimeProvider ensures deterministic value
			DateTimeOffset? createdUtc = await reader.GetCreatedUtcAsync();
			Assert.Equal(expectedUtc, createdUtc);
		}
		finally
		{
			await reader.DisposeAsync();
		}
	}

	/// <summary>
	/// Generates test row data as an async enumerable for the Users table.
	/// </summary>
	private static async IAsyncEnumerable<object?[]> GenerateRows()
	{
		await Task.CompletedTask;
		yield return [1L, "Alice", "alice@test.com"];
		yield return [2L, "Bob", null];
	}
}
