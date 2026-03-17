// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleReaderTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetMigrationHistoryAsync"/> returns the migration
	/// entries written to the shuttle file, ordered by migration ID.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenMigrationsExist_ReturnsMigrations()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act
				List<MigrationInfo> migrations = await sut.GetMigrationHistoryAsync();

				// Assert
				Assert.Equal(2, migrations.Count);
				Assert.Equal("20260101000000_Initial", migrations[0].MigrationId);
				Assert.Equal("10.0.0", migrations[0].ProductVersion);
				Assert.Equal("20260201000000_AddMessages", migrations[1].MigrationId);
				Assert.Equal("10.0.0", migrations[1].ProductVersion);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetMigrationHistoryAsync"/> returns an empty list
	/// when the shuttle file has no <c>__EFMigrationsHistory</c> table.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenNoMigrationsTable_ReturnsEmptyList()
	{
		// Arrange — empty shuttle file has no migrations table
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateEmptyShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act
				List<MigrationInfo> migrations = await sut.GetMigrationHistoryAsync();

				// Assert
				Assert.Empty(migrations);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetMigrationHistoryAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the reader has not been initialized.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			var sut = new SqliteShuttleReader(filePath, NullLogger.Instance);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetMigrationHistoryAsync());
				Assert.Equal("Reader is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetMigrationHistoryAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the reader has been disposed.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.GetMigrationHistoryAsync());
			Assert.Contains(nameof(SqliteShuttleReader), ex.ObjectName);
		}
	}
}
