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
	/// Verifies that <see cref="SqliteShuttleReader.ValidateIntegrityAsync"/> completes successfully
	/// for a valid shuttle file without throwing.
	/// </summary>
	[Fact]
	public async Task ValidateIntegrityAsync_WhenFileIsValid_Succeeds()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act — should not throw
				await sut.ValidateIntegrityAsync();
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.ValidateIntegrityAsync"/> throws
	/// <see cref="InvalidDataException"/> when the underlying SQLite database is corrupted.
	/// The exception message contains the error summary from <c>PRAGMA integrity_check</c>.
	/// </summary>
	/// <remarks>
	/// The corruption is introduced at the schema level via <c>PRAGMA writable_schema</c>: the
	/// <c>Users</c> table's root page is redirected to the same page as <c>__Shuttle_BackupInfo</c>,
	/// creating a double-referenced page. This leaves BackupInfo readable for
	/// <see cref="SqliteShuttleReader.InitializeAsync"/> while producing
	/// <c>PRAGMA integrity_check</c> error rows that exercise the error-summary branch.
	/// </remarks>
	[Fact]
	public async Task ValidateIntegrityAsync_WhenFileIsCorrupted_ThrowsInvalidDataException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateCorruptedShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidDataException>(() => sut.ValidateIntegrityAsync());
				Assert.StartsWith("Shuttle file integrity check failed with", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.ValidateIntegrityAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the reader has not been initialized.
	/// </summary>
	[Fact]
	public async Task ValidateIntegrityAsync_WhenNotInitialized_ThrowsInvalidOperationException()
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
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ValidateIntegrityAsync());
				Assert.Equal("Reader is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.ValidateIntegrityAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the reader has been disposed.
	/// </summary>
	[Fact]
	public async Task ValidateIntegrityAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.ValidateIntegrityAsync());
			Assert.Contains(nameof(SqliteShuttleReader), ex.ObjectName);
		}
	}
}
