// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleReaderTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetTableNamesAsync"/> returns only user tables,
	/// excluding internal tables (<c>__Shuttle_*</c>, <c>sqlite_*</c>, <c>__EFMigrationsHistory</c>).
	/// </summary>
	[Fact]
	public async Task GetTableNamesAsync_WhenTablesExist_ReturnsOnlyUserTables()
	{
		// Arrange — the valid shuttle file contains a "Users" table,
		// plus __Shuttle_BackupInfo and __EFMigrationsHistory (both should be excluded).
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act
				List<string> tables = await sut.GetTableNamesAsync();

				// Assert — only "Users" should be returned
				Assert.Single(tables);
				Assert.Equal("Users", tables[0]);

				// Assert — internal tables are excluded
				Assert.DoesNotContain("__Shuttle_BackupInfo", tables);
				Assert.DoesNotContain("__EFMigrationsHistory", tables);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetTableNamesAsync"/> returns an empty list
	/// when the shuttle file contains no user tables.
	/// </summary>
	[Fact]
	public async Task GetTableNamesAsync_WhenNoUserTablesExist_ReturnsEmptyList()
	{
		// Arrange — empty shuttle file has only __Shuttle_BackupInfo (internal)
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateEmptyShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act
				List<string> tables = await sut.GetTableNamesAsync();

				// Assert
				Assert.Empty(tables);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetTableNamesAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the reader has not been initialized.
	/// </summary>
	[Fact]
	public async Task GetTableNamesAsync_WhenNotInitialized_ThrowsInvalidOperationException()
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
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetTableNamesAsync());
				Assert.Equal("Reader is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetTableNamesAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the reader has been disposed.
	/// </summary>
	[Fact]
	public async Task GetTableNamesAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.GetTableNamesAsync());
			Assert.Contains(nameof(SqliteShuttleReader), ex.ObjectName);
		}
	}
}
