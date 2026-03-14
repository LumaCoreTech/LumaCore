// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

// Checkpoint lifecycle: from first read on an empty database through the full
// write → read → update → drop progression.
//
// These tests exercise the provider-specific SQL for the restore checkpoint table
// (CREATE TABLE, INSERT, SELECT, UPDATE, DROP TABLE) against the configured database
// engine, validating identifier quoting and schema qualification.
//
//   1. Read when nothing exists → null (establishes baseline).
//   2. Write + Read roundtrip → data correctly persisted (tests CREATE TABLE + INSERT + SELECT).
//   3. Write twice → idempotent overwrite via DELETE + INSERT (no duplicates).
//   4. Update phase → targeted UPDATE of phase + updated_utc columns.
//   5. Drop existing table → DROP TABLE removes the checkpoint.
//   6. Drop non-existent table → idempotent (DROP TABLE IF EXISTS, no error).
public sealed partial class ProviderOperationsIntegrationTests
{
	// --- 1. Read when nothing exists ---

	#region ReadCheckpointAsync

	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations.ReadCheckpointAsync"/> returns
	/// <see langword="null"/> when the checkpoint table does not exist in the database.
	/// </summary>
	[Fact]
	public async Task ReadCheckpointAsync_WhenTableDoesNotExist_ReturnsNull()
	{
		// Arrange
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			DbConnection connection = await harness.GetOpenConnectionAsync();

			// Act
			RestoreCheckpointData? result = await harness.Sut.ReadCheckpointAsync(
				                                connection,
				                                "__TestCheckpoint",
				                                CancellationToken.None);

			// Assert
			Assert.Null(result);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion

	// --- 2. Write + Read roundtrip ---

	#region WriteCheckpointAsync

	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations.WriteCheckpointAsync"/> creates the checkpoint
	/// table and writes a row that can be read back via
	/// <see cref="IDatabaseProviderOperations.ReadCheckpointAsync"/>.
	/// </summary>
	[Fact]
	public async Task WriteCheckpointAsync_WhenCalled_CreatesTableAndWritesCheckpoint()
	{
		// Arrange
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			// Act
			await harness.Sut.WriteCheckpointAsync(
				harness.DbContext,
				"__TestCheckpoint",
				shuttleId: "shuttle-001",
				baselineMigrationId: "20260101_Init",
				startedUtc: "2026-01-15T10:00:00Z",
				CancellationToken.None);

			// Assert — table was created and data can be read back
			DbConnection connection = await harness.GetOpenConnectionAsync();
			RestoreCheckpointData? result = await harness.Sut.ReadCheckpointAsync(
				                                connection,
				                                "__TestCheckpoint",
				                                CancellationToken.None);

			Assert.NotNull(result);
			Assert.Equal("shuttle-001", result.ShuttleId);
			Assert.Equal("20260101_Init", result.BaselineMigrationId);
			Assert.Equal(RestoreCheckpointData.PhaseSchemaCleanup, result.Phase);
			Assert.Equal("2026-01-15T10:00:00Z", result.StartedUtc);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Write twice → idempotent overwrite ---

	/// <summary>
	/// Verifies that calling <see cref="IDatabaseProviderOperations.WriteCheckpointAsync"/> a second time
	/// replaces the previous checkpoint row rather than adding a duplicate.
	/// </summary>
	[Fact]
	public async Task WriteCheckpointAsync_WhenCalledTwice_OverwritesPreviousCheckpoint()
	{
		// Arrange
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			await harness.Sut.WriteCheckpointAsync(
				harness.DbContext,
				"__TestCheckpoint",
				shuttleId: "shuttle-001",
				baselineMigrationId: "20260101_Init",
				startedUtc: "2026-01-15T10:00:00Z",
				CancellationToken.None);

			// Act — write again with different data
			await harness.Sut.WriteCheckpointAsync(
				harness.DbContext,
				"__TestCheckpoint",
				shuttleId: "shuttle-002",
				baselineMigrationId: "20260201_Second",
				startedUtc: "2026-02-01T12:00:00Z",
				CancellationToken.None);

			// Assert — only the second checkpoint data is present
			DbConnection connection = await harness.GetOpenConnectionAsync();
			RestoreCheckpointData? result = await harness.Sut.ReadCheckpointAsync(
				                                connection,
				                                "__TestCheckpoint",
				                                CancellationToken.None);

			Assert.NotNull(result);
			Assert.Equal("shuttle-002", result.ShuttleId);
			Assert.Equal("20260201_Second", result.BaselineMigrationId);
			Assert.Equal("2026-02-01T12:00:00Z", result.StartedUtc);

			// Verify exactly one row exists (DELETE + INSERT, not a duplicate INSERT).
			long rowCount = await harness.CountRowsAsync("__TestCheckpoint");
			Assert.Equal(1L, rowCount);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion

	// --- 4. Update phase ---

	#region UpdateCheckpointPhaseAsync

	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations.UpdateCheckpointPhaseAsync"/> updates the phase
	/// and <c>updated_utc</c> timestamp while preserving all other checkpoint fields.
	/// </summary>
	[Fact]
	public async Task UpdateCheckpointPhaseAsync_WhenCalled_UpdatesPhaseAndTimestamp()
	{
		// Arrange — same setup as the roundtrip test, but this time we update the phase.
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			await harness.Sut.WriteCheckpointAsync(
				harness.DbContext,
				"__TestCheckpoint",
				shuttleId: "shuttle-001",
				baselineMigrationId: "20260101_Init",
				startedUtc: "2026-01-15T10:00:00Z",
				CancellationToken.None);

			// Act — update phase from schema_cleanup to import
			await harness.Sut.UpdateCheckpointPhaseAsync(
				harness.DbContext,
				"__TestCheckpoint",
				phase: RestoreCheckpointData.PhaseImport,
				updatedUtc: "2026-01-15T10:05:00Z",
				CancellationToken.None);

			// Assert — phase is updated, other fields unchanged
			DbConnection connection = await harness.GetOpenConnectionAsync();
			RestoreCheckpointData? result = await harness.Sut.ReadCheckpointAsync(
				                                connection,
				                                "__TestCheckpoint",
				                                CancellationToken.None);

			Assert.NotNull(result);
			Assert.Equal(RestoreCheckpointData.PhaseImport, result.Phase);
			Assert.Equal("shuttle-001", result.ShuttleId);
			Assert.Equal("20260101_Init", result.BaselineMigrationId);
			Assert.Equal("2026-01-15T10:00:00Z", result.StartedUtc);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion

	// --- 5–6. Drop table (existing + idempotent) ---

	#region DropCheckpointTableAsync

	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations.DropCheckpointTableAsync"/> removes the
	/// checkpoint table when it exists.
	/// </summary>
	[Fact]
	public async Task DropCheckpointTableAsync_WhenTableExists_DropsTable()
	{
		// Arrange
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			await harness.Sut.WriteCheckpointAsync(
				harness.DbContext,
				"__TestCheckpoint",
				shuttleId: "shuttle-001",
				baselineMigrationId: "20260101_Init",
				startedUtc: "2026-01-15T10:00:00Z",
				CancellationToken.None);

			DbConnection connection = await harness.GetOpenConnectionAsync();
			Assert.True(await harness.Sut.TableExistsAsync(connection, "__TestCheckpoint", CancellationToken.None));

			// Act
			await harness.Sut.DropCheckpointTableAsync(harness.DbContext, "__TestCheckpoint", CancellationToken.None);

			// Assert — table no longer exists
			Assert.False(await harness.Sut.TableExistsAsync(connection, "__TestCheckpoint", CancellationToken.None));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations.DropCheckpointTableAsync"/> does not throw
	/// when the checkpoint table does not exist (idempotent via <c>DROP TABLE IF EXISTS</c>).
	/// </summary>
	[Fact]
	public async Task DropCheckpointTableAsync_WhenTableDoesNotExist_DoesNotThrow()
	{
		// Arrange
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			DbConnection connection = await harness.GetOpenConnectionAsync();

			// Act — should not throw even though the table doesn't exist
			await harness.Sut.DropCheckpointTableAsync(harness.DbContext, "__TestCheckpoint", CancellationToken.None);

			// Assert — table still doesn't exist (no side effects)
			Assert.False(await harness.Sut.TableExistsAsync(connection, "__TestCheckpoint", CancellationToken.None));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion
}
