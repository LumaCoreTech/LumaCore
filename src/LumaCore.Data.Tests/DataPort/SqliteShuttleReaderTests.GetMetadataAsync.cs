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
	/// Verifies that <see cref="SqliteShuttleReader.GetMetadataAsync"/> returns all metadata entries
	/// including both user-supplied and system markers.
	/// </summary>
	[Fact]
	public async Task GetMetadataAsync_WhenMetadataExists_ReturnsDictionary()
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
				Dictionary<string, string> metadata = await sut.GetMetadataAsync();

				// Assert — user-supplied metadata
				Assert.Equal("sqlite", metadata["source_provider"]);
				Assert.Equal("2026-01-01T00:00:00Z", metadata["export_time"]);

				// Assert — system markers from FinalizeAsync() with syntactic validation
				AssertSystemMarkers(metadata);

				// Assert — no unexpected keys beyond the 2 user-supplied + 4 system markers
				Assert.Equal(6, metadata.Count);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetMetadataAsync"/> returns the system markers
	/// written by <see cref="SqliteShuttleWriter.FinalizeAsync(CancellationToken)"/> even when no
	/// custom metadata was written by the caller.
	/// </summary>
	/// <remarks>
	/// The <c>exists is null</c> branch (no <c>__Shuttle_BackupInfo</c> table) cannot be exercised
	/// in isolation because the reader's serializable transaction requires the table to exist at
	/// initialization time. This test instead verifies the complementary scenario: the table exists
	/// with only system-generated entries.
	/// </remarks>
	[Fact]
	public async Task GetMetadataAsync_WhenNoCustomMetadataWritten_ReturnsOnlySystemMarkers()
	{
		// Arrange — even an empty shuttle file has metadata from FinalizeAsync()
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateEmptyShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act
				Dictionary<string, string> metadata = await sut.GetMetadataAsync();

				// Assert — all 4 system markers with syntactic validation
				AssertSystemMarkers(metadata);

				// Assert — no keys beyond the 4 system markers
				Assert.Equal(4, metadata.Count);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetMetadataAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the reader has not been initialized.
	/// </summary>
	[Fact]
	public async Task GetMetadataAsync_WhenNotInitialized_ThrowsInvalidOperationException()
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
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetMetadataAsync());
				Assert.Equal("Reader is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetMetadataAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the reader has been disposed.
	/// </summary>
	[Fact]
	public async Task GetMetadataAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.GetMetadataAsync());
			Assert.Contains(nameof(SqliteShuttleReader), ex.ObjectName);
		}
	}
}
