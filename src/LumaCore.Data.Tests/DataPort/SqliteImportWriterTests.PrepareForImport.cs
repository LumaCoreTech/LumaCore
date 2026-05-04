// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Import.Implementations;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteImportWriterTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.PrepareForImportAsync"/> creates the checkpoint table
	/// in the target database on a fresh import (no previous checkpoint exists).
	/// </summary>
	[Fact]
	public async Task PrepareForImportAsync_WhenFreshImport_CreatesCheckpointTable()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("prepare-fresh");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();

			// Act
			await writer.PrepareForImportAsync(TestShuttleId);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert
		bool exists = await CheckpointTableExistsAsync(dbPath);
		Assert.True(exists);
	}

	/// <summary>
	/// Verifies that when a checkpoint exists with the same shuttle ID, the writer preserves it
	/// for resume (does not drop/recreate).
	/// </summary>
	[Fact]
	public async Task PrepareForImportAsync_WhenCheckpointMatchesShuttleId_PreservesCheckpoint()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("prepare-match");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);
		await InsertCheckpointAsync(dbPath, TestShuttleId, "Items", chunksCompleted: 2, totalRowsImported: 10000);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();

			// Act
			await writer.PrepareForImportAsync(TestShuttleId);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert — checkpoint should still exist with original data.
		List<(string ShuttleId, string TableName, int ChunksCompleted, long TotalRows)> checkpoints =
			await ReadCheckpointsAsync(dbPath);
		Assert.Single(checkpoints);
		Assert.Equal(TestShuttleId, checkpoints[0].ShuttleId);
		Assert.Equal("Items", checkpoints[0].TableName);
		Assert.Equal(2, checkpoints[0].ChunksCompleted);
		Assert.Equal(10000, checkpoints[0].TotalRows);
	}

	/// <summary>
	/// Verifies that when a checkpoint exists with a different shuttle ID, the writer discards
	/// the old checkpoint and starts fresh (mismatch scenario).
	/// </summary>
	[Fact]
	public async Task PrepareForImportAsync_WhenShuttleIdMismatch_DiscardsOldCheckpoint()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("prepare-mismatch");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);
		await InsertCheckpointAsync(dbPath, OtherShuttleId, "Items", chunksCompleted: 5, totalRowsImported: 25000);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();

			// Act — prepare with a DIFFERENT shuttle ID.
			await writer.PrepareForImportAsync(TestShuttleId);
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert — old checkpoint should be gone, table should exist but be empty.
		bool exists = await CheckpointTableExistsAsync(dbPath);
		Assert.True(exists);

		List<(string ShuttleId, string TableName, int ChunksCompleted, long TotalRows)> checkpoints =
			await ReadCheckpointsAsync(dbPath);
		Assert.Empty(checkpoints);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.PrepareForImportAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the writer has not been initialized.
	/// </summary>
	[Fact]
	public async Task PrepareForImportAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var writer = new SqliteImportWriter("Data Source=dummy.db", TimeProvider.System);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.PrepareForImportAsync(TestShuttleId));
		Assert.Equal("Importer is not initialized", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.PrepareForImportAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the writer has been disposed.
	/// </summary>
	[Fact]
	public async Task PrepareForImportAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var writer = new SqliteImportWriter("Data Source=dummy.db", TimeProvider.System);
		await writer.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => writer.PrepareForImportAsync(TestShuttleId));
		Assert.Equal(typeof(SqliteImportWriter).FullName, ex.ObjectName);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.PrepareForImportAsync"/> throws
	/// <see cref="ArgumentNullException"/> when the shuttle ID is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task PrepareForImportAsync_WhenShuttleIdIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("prepare-null");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => writer.PrepareForImportAsync(null!));
			Assert.Equal("shuttleId", ex.ParamName);
		}
		finally
		{
			await writer.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.PrepareForImportAsync"/> throws
	/// <see cref="ArgumentException"/> when the shuttle ID is empty.
	/// </summary>
	[Fact]
	public async Task PrepareForImportAsync_WhenShuttleIdIsEmpty_ThrowsArgumentException()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("prepare-empty");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() => writer.PrepareForImportAsync(""));
			Assert.Equal("shuttleId", ex.ParamName);
		}
		finally
		{
			await writer.DisposeAsync();
		}
	}
}
