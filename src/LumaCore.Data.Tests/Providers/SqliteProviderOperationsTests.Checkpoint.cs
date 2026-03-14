// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class SqliteProviderOperationsTests
{
	#region ReadCheckpointAsync

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.ReadCheckpointAsync"/> returns <see langword="null"/>
	/// when the checkpoint table does not exist in the database.
	/// </summary>
	[Fact]
	public async Task ReadCheckpointAsync_WhenTableDoesNotExist_ReturnsNull()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			await connection.OpenAsync();

			// Act
			RestoreCheckpointData? result = await sut.ReadCheckpointAsync(
				                                connection,
				                                "__RestoreCheckpoint",
				                                CancellationToken.None);

			// Assert
			Assert.Null(result);
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.ReadCheckpointAsync"/> returns <see langword="null"/>
	/// when the checkpoint table exists but contains no rows.
	/// </summary>
	[Fact]
	public async Task ReadCheckpointAsync_WhenTableExistsButEmpty_ReturnsNull()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			await connection.OpenAsync();

			// Create the checkpoint table structure without inserting any rows.
			SqliteCommand cmd = connection.CreateCommand();
			try
			{
				cmd.CommandText =
					"""
					CREATE TABLE "__RestoreCheckpoint" (
					  "shuttle_id"            TEXT NOT NULL,
					  "baseline_migration_id" TEXT NOT NULL,
					  "phase"                 TEXT NOT NULL,
					  "started_utc"           TEXT NOT NULL,
					  "updated_utc"           TEXT NOT NULL
					)
					""";
				await cmd.ExecuteNonQueryAsync();
			}
			finally
			{
				await cmd.DisposeAsync();
			}

			// Act
			RestoreCheckpointData? result = await sut.ReadCheckpointAsync(
				                                connection,
				                                "__RestoreCheckpoint",
				                                CancellationToken.None);

			// Assert
			Assert.Null(result);
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}

	#endregion

	#region WriteCheckpointAsync

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.WriteCheckpointAsync"/> creates the checkpoint table
	/// and writes a row that can be read back via <see cref="SqliteProviderOperations.ReadCheckpointAsync"/>.
	/// </summary>
	[Fact]
	public async Task WriteCheckpointAsync_WhenCalled_CreatesTableAndWritesCheckpoint()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			await connection.OpenAsync();

			DbContextOptions<LumaCoreDbContext> ctxOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseSqlite(connection)
				.Options;
			LumaCoreDbContext dbContext = new(ctxOptions);
			try
			{
				// Act
				await sut.WriteCheckpointAsync(
					dbContext,
					"__RestoreCheckpoint",
					shuttleId: "shuttle-001",
					baselineMigrationId: "20260101_Init",
					startedUtc: "2026-01-15T10:00:00Z",
					CancellationToken.None);

				// Assert — table was created and data can be read back
				RestoreCheckpointData? result = await sut.ReadCheckpointAsync(
					                                connection,
					                                "__RestoreCheckpoint",
					                                CancellationToken.None);

				Assert.NotNull(result);
				Assert.Equal("shuttle-001", result.ShuttleId);
				Assert.Equal("20260101_Init", result.BaselineMigrationId);
				Assert.Equal(RestoreCheckpointData.PhaseSchemaCleanup, result.Phase);
				Assert.Equal("2026-01-15T10:00:00Z", result.StartedUtc);
			}
			finally
			{
				await dbContext.DisposeAsync();
			}
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="SqliteProviderOperations.WriteCheckpointAsync"/> a second time
	/// replaces the previous checkpoint row rather than adding a duplicate.
	/// </summary>
	[Fact]
	public async Task WriteCheckpointAsync_WhenCalledTwice_OverwritesPreviousCheckpoint()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			await connection.OpenAsync();

			DbContextOptions<LumaCoreDbContext> ctxOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseSqlite(connection)
				.Options;
			LumaCoreDbContext dbContext = new(ctxOptions);
			try
			{
				await sut.WriteCheckpointAsync(
					dbContext,
					"__RestoreCheckpoint",
					shuttleId: "shuttle-001",
					baselineMigrationId: "20260101_Init",
					startedUtc: "2026-01-15T10:00:00Z",
					CancellationToken.None);

				// Act — write again with different data
				await sut.WriteCheckpointAsync(
					dbContext,
					"__RestoreCheckpoint",
					shuttleId: "shuttle-002",
					baselineMigrationId: "20260201_Second",
					startedUtc: "2026-02-01T12:00:00Z",
					CancellationToken.None);

				// Assert — only the second checkpoint data is present
				RestoreCheckpointData? result = await sut.ReadCheckpointAsync(
					                                connection,
					                                "__RestoreCheckpoint",
					                                CancellationToken.None);

				Assert.NotNull(result);
				Assert.Equal("shuttle-002", result.ShuttleId);
				Assert.Equal("20260201_Second", result.BaselineMigrationId);
				Assert.Equal("2026-02-01T12:00:00Z", result.StartedUtc);

				// Verify exactly one row exists (no duplicates).
				SqliteCommand countCmd = connection.CreateCommand();
				try
				{
					countCmd.CommandText = """SELECT COUNT(*) FROM "__RestoreCheckpoint" """;
					long rowCount = (long)(await countCmd.ExecuteScalarAsync())!;
					Assert.Equal(1, rowCount);
				}
				finally
				{
					await countCmd.DisposeAsync();
				}
			}
			finally
			{
				await dbContext.DisposeAsync();
			}
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}

	#endregion

	#region UpdateCheckpointPhaseAsync

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.UpdateCheckpointPhaseAsync"/> updates the phase
	/// and <c>updated_utc</c> timestamp of an existing checkpoint.
	/// </summary>
	[Fact]
	public async Task UpdateCheckpointPhaseAsync_WhenCalled_UpdatesPhaseAndTimestamp()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			await connection.OpenAsync();

			DbContextOptions<LumaCoreDbContext> ctxOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseSqlite(connection)
				.Options;
			LumaCoreDbContext dbContext = new(ctxOptions);
			try
			{
				await sut.WriteCheckpointAsync(
					dbContext,
					"__RestoreCheckpoint",
					shuttleId: "shuttle-001",
					baselineMigrationId: "20260101_Init",
					startedUtc: "2026-01-15T10:00:00Z",
					CancellationToken.None);

				// Act — update phase from schema_cleanup to import
				await sut.UpdateCheckpointPhaseAsync(
					dbContext,
					"__RestoreCheckpoint",
					phase: RestoreCheckpointData.PhaseImport,
					updatedUtc: "2026-01-15T10:05:00Z",
					CancellationToken.None);

				// Assert — phase is updated, other fields unchanged
				RestoreCheckpointData? result = await sut.ReadCheckpointAsync(
					                                connection,
					                                "__RestoreCheckpoint",
					                                CancellationToken.None);

				Assert.NotNull(result);
				Assert.Equal(RestoreCheckpointData.PhaseImport, result.Phase);
				Assert.Equal("shuttle-001", result.ShuttleId);
				Assert.Equal("20260101_Init", result.BaselineMigrationId);
				Assert.Equal("2026-01-15T10:00:00Z", result.StartedUtc);
			}
			finally
			{
				await dbContext.DisposeAsync();
			}
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}

	#endregion

	#region DropCheckpointTableAsync

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.DropCheckpointTableAsync"/> removes the checkpoint
	/// table when it exists.
	/// </summary>
	[Fact]
	public async Task DropCheckpointTableAsync_WhenTableExists_DropsTable()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			await connection.OpenAsync();

			DbContextOptions<LumaCoreDbContext> ctxOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseSqlite(connection)
				.Options;
			LumaCoreDbContext dbContext = new(ctxOptions);
			try
			{
				await sut.WriteCheckpointAsync(
					dbContext,
					"__RestoreCheckpoint",
					shuttleId: "shuttle-001",
					baselineMigrationId: "20260101_Init",
					startedUtc: "2026-01-15T10:00:00Z",
					CancellationToken.None);

				// Verify the table exists before dropping.
				Assert.True(await sut.TableExistsAsync(connection, "__RestoreCheckpoint", CancellationToken.None));

				// Act
				await sut.DropCheckpointTableAsync(dbContext, "__RestoreCheckpoint", CancellationToken.None);

				// Assert — table no longer exists
				Assert.False(await sut.TableExistsAsync(connection, "__RestoreCheckpoint", CancellationToken.None));
			}
			finally
			{
				await dbContext.DisposeAsync();
			}
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.DropCheckpointTableAsync"/> does not throw
	/// when the checkpoint table does not exist (idempotent via <c>DROP TABLE IF EXISTS</c>).
	/// </summary>
	[Fact]
	public async Task DropCheckpointTableAsync_WhenTableDoesNotExist_DoesNotThrow()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var connection = new SqliteConnection("Data Source=:memory:");
		try
		{
			await connection.OpenAsync();

			DbContextOptions<LumaCoreDbContext> ctxOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseSqlite(connection)
				.Options;
			LumaCoreDbContext dbContext = new(ctxOptions);
			try
			{
				// Act — should not throw even though the table doesn't exist
				await sut.DropCheckpointTableAsync(dbContext, "__RestoreCheckpoint", CancellationToken.None);

				// Assert — table still doesn't exist (no side effects)
				Assert.False(await sut.TableExistsAsync(connection, "__RestoreCheckpoint", CancellationToken.None));
			}
			finally
			{
				await dbContext.DisposeAsync();
			}
		}
		finally
		{
			await connection.DisposeAsync();
		}
	}

	#endregion
}
