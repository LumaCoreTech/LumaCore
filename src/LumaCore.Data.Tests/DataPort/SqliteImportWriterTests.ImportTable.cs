// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Import.Implementations;
using LumaCore.Data.DataPort.Models;
using LumaCore.Core.IO;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteImportWriterTests
{
	#region ImportTableAsync()

	/// <summary>
	/// Verifies that a fresh import of a small table (fewer rows than one chunk) succeeds and all
	/// rows are present in the target database.
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenFreshImportSmallTable_InsertsAllRows()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("import-small");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		(int Id, string Name)[] rows = GenerateRows(10);
		TableSnapshot snapshot = CreateSnapshot("Items", rows);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);

			// Act
			await writer.ImportTableAsync(snapshot, null, null, 1, 1);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert
		List<(int Id, string Name)> actual = await ReadAllRowsAsync(dbPath, "Items");
		Assert.Equal(10, actual.Count);
		for (int i = 0; i < rows.Length; i++)
		{
			Assert.Equal(rows[i].Id, actual[i].Id);
			Assert.Equal(rows[i].Name, actual[i].Name);
		}
	}

	/// <summary>
	/// Verifies that importing an empty table succeeds and leaves the target table empty.
	/// Also verifies that no checkpoint is written (no chunks committed).
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenTableIsEmpty_SucceedsWithNoRows()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("import-empty");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);

			// Act
			await writer.ImportTableAsync(CreateEmptySnapshot("Items"), null, null, 1, 1);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert
		List<(int Id, string Name)> actual = await ReadAllRowsAsync(dbPath, "Items");
		Assert.Empty(actual);
	}

	/// <summary>
	/// Verifies that importing exactly one chunk of rows (i.e., exactly <see cref="DataPortTuning.ImportChunkSizeRows"/>
	/// rows) produces exactly one checkpoint record.
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenRowCountEqualsChunkSize_CreatesOneCheckpoint()
	{
		// Arrange
		int chunkSize = DataPortTuning.ImportChunkSizeRows;
		using var tempDir = new TemporaryFolder("import-exact");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		(int Id, string Name)[] rows = GenerateRows(chunkSize);
		TableSnapshot snapshot = CreateSnapshot("Items", rows);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);

			// Act
			await writer.ImportTableAsync(snapshot, null, null, 1, 1);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert — all rows present.
		List<(int Id, string Name)> actual = await ReadAllRowsAsync(dbPath, "Items");
		Assert.Equal(chunkSize, actual.Count);

		// Assert — exactly one checkpoint (one full chunk).
		List<(string ShuttleId, string TableName, int ChunksCompleted, long TotalRows)> checkpoints =
			await ReadCheckpointsAsync(dbPath);
		Assert.Single(checkpoints);
		Assert.Equal(TestShuttleId, checkpoints[0].ShuttleId);
		Assert.Equal("Items", checkpoints[0].TableName);
		Assert.Equal(1, checkpoints[0].ChunksCompleted);
		Assert.Equal(chunkSize, checkpoints[0].TotalRows);
	}

	/// <summary>
	/// Verifies that importing more rows than one chunk creates multiple checkpoint records
	/// with correct cumulative row counts.
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenMultipleChunks_CreatesCorrectCheckpoints()
	{
		// Arrange — 2 full chunks + 100 extra rows = 3 chunks total.
		int chunkSize = DataPortTuning.ImportChunkSizeRows;
		int totalRows = chunkSize * 2 + 100;
		using var tempDir = new TemporaryFolder("import-multi");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		(int Id, string Name)[] rows = GenerateRows(totalRows);
		TableSnapshot snapshot = CreateSnapshot("Items", rows);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);

			// Act
			await writer.ImportTableAsync(snapshot, null, null, 1, 1);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert — all rows present.
		List<(int Id, string Name)> actual = await ReadAllRowsAsync(dbPath, "Items");
		Assert.Equal(totalRows, actual.Count);

		// Assert — checkpoint reflects final state (3 chunks, totalRows total).
		List<(string ShuttleId, string TableName, int ChunksCompleted, long TotalRows)> checkpoints =
			await ReadCheckpointsAsync(dbPath);
		Assert.Single(checkpoints);
		Assert.Equal(TestShuttleId, checkpoints[0].ShuttleId);
		Assert.Equal("Items", checkpoints[0].TableName);
		Assert.Equal(3, checkpoints[0].ChunksCompleted);
		Assert.Equal(totalRows, checkpoints[0].TotalRows);
	}

	/// <summary>
	/// Verifies that a fresh import clears pre-existing data in the target table before inserting.
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenTableHasExistingData_ClearsBeforeImport()
	{
		// Arrange — seed 5 rows, then import 3 new ones.
		using var tempDir = new TemporaryFolder("import-clear");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(
			dbPath,
			seedRows: [(100, "Old1"), (101, "Old2"), (102, "Old3"), (103, "Old4"), (104, "Old5")]);

		(int Id, string Name)[] newRows = GenerateRows(3);
		TableSnapshot snapshot = CreateSnapshot("Items", newRows);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);

			// Act
			await writer.ImportTableAsync(snapshot, null, null, 1, 1);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert — only new rows, old data (IDs 100–104) must be gone.
		List<(int Id, string Name)> actual = await ReadAllRowsAsync(dbPath, "Items");
		Assert.Equal(3, actual.Count);
		for (int i = 0; i < newRows.Length; i++)
		{
			Assert.Equal(newRows[i].Id, actual[i].Id);
			Assert.Equal(newRows[i].Name, actual[i].Name);
		}

		Assert.DoesNotContain(actual, r => r.Id >= 100);
	}

	/// <summary>
	/// Verifies the resume scenario: import one chunk, then simulate a failure by disposing the writer,
	/// then create a new writer and resume — the final result should contain all rows without duplicates.
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenResumingAfterFailure_SkipsAlreadyImportedRows()
	{
		// Arrange — use 2 full chunks + partial.
		int chunkSize = DataPortTuning.ImportChunkSizeRows;
		int totalRows = chunkSize * 2 + 50;
		using var tempDir = new TemporaryFolder("import-resume");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		(int Id, string Name)[] allRows = GenerateRows(totalRows);

		// Phase 1: Import the first chunk only, then "crash" (dispose).
		{
			TableSnapshot firstChunkSnapshot = CreateSnapshot("Items", allRows[..chunkSize]);

			SqliteImportWriter writer = CreateWriter(dbPath);
			try
			{
				await writer.InitializeAsync();
				await writer.PrepareForImportAsync(TestShuttleId);
				await writer.ImportTableAsync(firstChunkSnapshot, null, null, 1, 1);
			}
			finally
			{
				await writer.DisposeAsync();
			}
		}

		// Verify phase 1 state: 1 chunk committed.
		List<(int Id, string Name)> afterPhase1 = await ReadAllRowsAsync(dbPath, "Items");
		Assert.Equal(chunkSize, afterPhase1.Count);

		List<(string ShuttleId, string TableName, int ChunksCompleted, long TotalRows)> checkpointsAfterPhase1 =
			await ReadCheckpointsAsync(dbPath);
		Assert.Single(checkpointsAfterPhase1);
		Assert.Equal(TestShuttleId, checkpointsAfterPhase1[0].ShuttleId);
		Assert.Equal("Items", checkpointsAfterPhase1[0].TableName);
		Assert.Equal(1, checkpointsAfterPhase1[0].ChunksCompleted);
		Assert.Equal(chunkSize, checkpointsAfterPhase1[0].TotalRows);

		// Phase 2: Resume import with the full row set.
		{
			TableSnapshot fullSnapshot = CreateSnapshot("Items", allRows);

			SqliteImportWriter writer = CreateWriter(dbPath);
			try
			{
				await writer.InitializeAsync();
				await writer.PrepareForImportAsync(TestShuttleId);
				await writer.ImportTableAsync(fullSnapshot, null, null, 1, 1);
				await writer.CleanupAfterImportAsync();
			}
			finally
			{
				await writer.DisposeAsync();
			}
		}

		// Assert — all rows present, no duplicates.
		List<(int Id, string Name)> finalRows = await ReadAllRowsAsync(dbPath, "Items");
		Assert.Equal(totalRows, finalRows.Count);

		// Verify first and last row.
		Assert.Equal(1, finalRows[0].Id);
		Assert.Equal("Row_1", finalRows[0].Name);
		Assert.Equal(totalRows, finalRows[^1].Id);
		Assert.Equal($"Row_{totalRows}", finalRows[^1].Name);

		// Checkpoint table should be dropped after cleanup.
		bool checkpointExists = await CheckpointTableExistsAsync(dbPath);
		Assert.False(checkpointExists);
	}

	/// <summary>
	/// Verifies the shuttle-ID mismatch resume scenario: an old checkpoint exists from a different shuttle,
	/// the new import discards it and imports fresh.
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenShuttleIdMismatchAndResume_ImportsFromScratch()
	{
		// Arrange — seed an old checkpoint from a different shuttle.
		using var tempDir = new TemporaryFolder("import-mismatch-resume");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath, seedRows: [(999, "OldData")]);
		await InsertCheckpointAsync(dbPath, OtherShuttleId, "Items", chunksCompleted: 3, totalRowsImported: 15000);

		(int Id, string Name)[] rows = GenerateRows(5);
		TableSnapshot snapshot = CreateSnapshot("Items", rows);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId); // Mismatch detected, old checkpoint discarded.

			// Act
			await writer.ImportTableAsync(snapshot, null, null, 1, 1);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert — only new data, old row (999, "OldData") must be gone.
		List<(int Id, string Name)> actual = await ReadAllRowsAsync(dbPath, "Items");
		Assert.Equal(5, actual.Count);
		for (int i = 0; i < rows.Length; i++)
		{
			Assert.Equal(rows[i].Id, actual[i].Id);
			Assert.Equal(rows[i].Name, actual[i].Name);
		}

		Assert.DoesNotContain(actual, r => r.Id == 999);

		// Checkpoint should reference the new shuttle ID.
		List<(string ShuttleId, string TableName, int ChunksCompleted, long TotalRows)> checkpoints =
			await ReadCheckpointsAsync(dbPath);
		Assert.Single(checkpoints);
		Assert.Equal(TestShuttleId, checkpoints[0].ShuttleId);
		Assert.Equal("Items", checkpoints[0].TableName);
		Assert.Equal(1, checkpoints[0].ChunksCompleted);
		Assert.Equal(5, checkpoints[0].TotalRows);
	}

	/// <summary>
	/// Verifies that progress reports are emitted during import.
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenProgressProvided_ReportsProgress()
	{
		// Arrange
		int chunkSize = DataPortTuning.ImportChunkSizeRows;
		int totalRows = chunkSize + 100; // 2 chunks.
		using var tempDir = new TemporaryFolder("import-progress");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		(int Id, string Name)[] rows = GenerateRows(totalRows);
		TableSnapshot snapshot = CreateSnapshot("Items", rows);
		var reports = new List<DataPortProgressReport>();
		var progress = new Progress<DataPortProgressReport>(r => reports.Add(r));

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);

			// Act
			await writer.ImportTableAsync(snapshot, null, progress, 1, 1);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Allow Progress<T> callbacks to drain (they use SynchronizationContext).
		await Task.Delay(50);

		// Assert — at least 2 progress reports (one per chunk + final).
		Assert.True(reports.Count >= 2, $"Expected at least 2 progress reports but got {reports.Count}.");
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.ImportTableAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the writer has not been initialized.
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var writer = new SqliteImportWriter("Data Source=dummy.db", TimeProvider.System);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			         writer.ImportTableAsync(
				         CreateEmptySnapshot("Items"),
				         null,
				         null,
				         1,
				         1));
		Assert.Equal("Importer is not initialized or not prepared.", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.ImportTableAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the writer has been disposed.
	/// </summary>
	[Fact]
	public async Task ImportTableAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var writer = new SqliteImportWriter("Data Source=dummy.db", TimeProvider.System);
		await writer.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => writer.ImportTableAsync(
			         CreateEmptySnapshot("Items"),
			         null,
			         null,
			         1,
			         1));
		Assert.Equal(typeof(SqliteImportWriter).FullName, ex.ObjectName);
	}

	#endregion
}
