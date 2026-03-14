// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

// Restore checkpoint operations: TryReadRestoreCheckpointAsync, WriteRestoreCheckpointAsync,
// UpdateRestoreCheckpointPhaseAsync, DropRestoreCheckpointTableAsync.
public sealed partial class DatabaseInitializerTests
{
	#region TryReadRestoreCheckpointAsync()

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.TryReadRestoreCheckpointAsync"/> returns
	/// <see langword="null"/> when the checkpoint table does not exist in the database.
	/// </summary>
	[Fact]
	public async Task TryReadRestoreCheckpointAsync_WhenTableDoesNotExist_ReturnsNull()
	{
		// Arrange — initialize the DB (migrations applied) so the schema is valid,
		// but no checkpoint has been written.
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				// Act
				RestoreCheckpointData? result = await harness.Sut
					                                .TryReadRestoreCheckpointAsync(dbContext, CancellationToken.None);

				// Assert
				Assert.Null(result);
			}
			finally
			{
				await scope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.TryReadRestoreCheckpointAsync"/> returns
	/// <see langword="null"/> when the checkpoint table exists but contains zero rows.
	/// This exercises the <c>reader.ReadAsync() == false</c> branch.
	/// </summary>
	[Fact]
	public async Task TryReadRestoreCheckpointAsync_WhenTableExistsButEmpty_ReturnsNull()
	{
		// Arrange — write a checkpoint, then delete all rows (leaving the empty table).
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				// Write checkpoint to create the table.
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					"00000000-0000-0000-0000-000000000001",
					"InitialCreate",
					CancellationToken.None);

				// Delete all rows, leaving an empty table.
				await harness.DeleteAllRowsAsync(dbContext, DatabaseInitializer.RestoreCheckpointTableName);

				// Act
				RestoreCheckpointData? result = await harness.Sut
					                                .TryReadRestoreCheckpointAsync(dbContext, CancellationToken.None);

				// Assert
				Assert.Null(result);
			}
			finally
			{
				await scope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion

	#region WriteRestoreCheckpointAsync()

	/// <summary>
	/// Verifies the full Write → Read round-trip: writing a checkpoint creates the table and stores the
	/// expected values, which are then readable via <see cref="DatabaseInitializer.TryReadRestoreCheckpointAsync"/>.
	/// </summary>
	[Fact]
	public async Task WriteRestoreCheckpointAsync_WhenCalled_CreatesReadableCheckpoint()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			const string shuttleId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				// Act
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					shuttleId,
					FirstMigrationId,
					CancellationToken.None);

				// Assert — read back and verify all fields
				RestoreCheckpointData? checkpoint = await harness.Sut
					                                    .TryReadRestoreCheckpointAsync(
						                                    dbContext,
						                                    CancellationToken.None);

				Assert.NotNull(checkpoint);
				Assert.Equal(shuttleId, checkpoint.ShuttleId);
				Assert.Equal(FirstMigrationId, checkpoint.BaselineMigrationId);
				Assert.Equal("schema_cleanup", checkpoint.Phase);
				Assert.Equal(harness.TimeProvider.GetUtcNow().ToString("O"), checkpoint.StartedUtc);
			}
			finally
			{
				await scope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="DatabaseInitializer.WriteRestoreCheckpointAsync"/> twice replaces
	/// the previous checkpoint instead of creating a duplicate row (idempotent via DELETE + INSERT).
	/// </summary>
	[Fact]
	public async Task WriteRestoreCheckpointAsync_WhenCalledTwice_ReplacesExistingCheckpoint()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					"11111111-1111-1111-1111-111111111111",
					"Migration_A",
					CancellationToken.None);

				// Act — write again with different values
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					"22222222-2222-2222-2222-222222222222",
					"Migration_B",
					CancellationToken.None);

				// Assert — only the second checkpoint should exist
				RestoreCheckpointData? checkpoint = await harness.Sut
					                                    .TryReadRestoreCheckpointAsync(
						                                    dbContext,
						                                    CancellationToken.None);

				Assert.NotNull(checkpoint);
				Assert.Equal("22222222-2222-2222-2222-222222222222", checkpoint.ShuttleId);
				Assert.Equal("Migration_B", checkpoint.BaselineMigrationId);
				Assert.Equal("schema_cleanup", checkpoint.Phase);
				Assert.Equal(harness.TimeProvider.GetUtcNow().ToString("O"), checkpoint.StartedUtc);

				// Verify exactly one row exists (DELETE + INSERT, not a duplicate INSERT).
				long rowCount = await harness.CountRowsAsync(dbContext, DatabaseInitializer.RestoreCheckpointTableName);
				Assert.Equal(1L, rowCount);
			}
			finally
			{
				await scope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion

	#region UpdateRestoreCheckpointPhaseAsync()

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.UpdateRestoreCheckpointPhaseAsync"/> correctly updates the
	/// phase column while preserving all other checkpoint fields.
	/// </summary>
	[Fact]
	public async Task UpdateRestoreCheckpointPhaseAsync_WhenCalled_UpdatesPhaseOnly()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);

			const string shuttleId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					shuttleId,
					FirstMigrationId,
					CancellationToken.None);

				// Capture StartedUtc before the phase update for comparison.
				RestoreCheckpointData? initial = await harness.Sut
					                                 .TryReadRestoreCheckpointAsync(dbContext, CancellationToken.None);
				Assert.NotNull(initial);

				// Act — advance phase from "schema_cleanup" to "migration"
				await harness.Sut.UpdateRestoreCheckpointPhaseAsync(dbContext, "migration", CancellationToken.None);

				// Assert
				RestoreCheckpointData? checkpoint = await harness
					                                    .Sut
					                                    .TryReadRestoreCheckpointAsync(
						                                    dbContext,
						                                    CancellationToken.None);

				Assert.NotNull(checkpoint);
				Assert.Equal("migration", checkpoint.Phase);
				Assert.Equal(shuttleId, checkpoint.ShuttleId);
				Assert.Equal(FirstMigrationId, checkpoint.BaselineMigrationId);
				Assert.Equal(initial.StartedUtc, checkpoint.StartedUtc);
			}
			finally
			{
				await scope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies the full phase progression: <c>schema_cleanup</c> → <c>migration</c> → <c>import</c>,
	/// confirming that each update is correctly persisted and the final state reflects the last update.
	/// </summary>
	[Fact]
	public async Task UpdateRestoreCheckpointPhaseAsync_WhenProgressedThroughAllPhases_ReflectsFinalPhase()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					"00000000-0000-0000-0000-000000000002",
					"InitialCreate",
					CancellationToken.None);
				await harness.Sut.UpdateRestoreCheckpointPhaseAsync(dbContext, "migration", CancellationToken.None);
				await harness.Sut.UpdateRestoreCheckpointPhaseAsync(dbContext, "import", CancellationToken.None);

				// Assert — final phase is "import"
				RestoreCheckpointData? checkpoint = await harness
					                                    .Sut
					                                    .TryReadRestoreCheckpointAsync(
						                                    dbContext,
						                                    CancellationToken.None);

				Assert.NotNull(checkpoint);
				Assert.Equal("import", checkpoint.Phase);
			}
			finally
			{
				await scope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion

	#region DropRestoreCheckpointTableAsync()

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.DropRestoreCheckpointTableAsync"/> removes the checkpoint
	/// table so that a subsequent <see cref="DatabaseInitializer.TryReadRestoreCheckpointAsync"/> returns
	/// <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task DropRestoreCheckpointTableAsync_WhenTableExists_RemovesTable()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					"00000000-0000-0000-0000-000000000003",
					"InitialCreate",
					CancellationToken.None);
				RestoreCheckpointData? before = await harness.Sut
					                                .TryReadRestoreCheckpointAsync(dbContext, CancellationToken.None);
				Assert.NotNull(before);

				// Act
				await harness.Sut.DropRestoreCheckpointTableAsync(dbContext, CancellationToken.None);

				// Assert — checkpoint is gone
				RestoreCheckpointData? after = await harness.Sut
					                               .TryReadRestoreCheckpointAsync(dbContext, CancellationToken.None);
				Assert.Null(after);
			}
			finally
			{
				await scope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.DropRestoreCheckpointTableAsync"/> is safe to call even
	/// when the checkpoint table does not exist (idempotent).
	/// </summary>
	[Fact]
	public async Task DropRestoreCheckpointTableAsync_WhenTableDoesNotExist_DoesNotThrow()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				// Act — drop a non-existent table
				await harness.Sut.DropRestoreCheckpointTableAsync(dbContext, CancellationToken.None);

				// Assert — no exception thrown, TryRead still returns null
				RestoreCheckpointData? result = await harness.Sut
					                                .TryReadRestoreCheckpointAsync(dbContext, CancellationToken.None);
				Assert.Null(result);
			}
			finally
			{
				await scope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion
}
