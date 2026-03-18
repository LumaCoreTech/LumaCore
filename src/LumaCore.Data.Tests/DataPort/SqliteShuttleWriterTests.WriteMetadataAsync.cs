// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleWriterTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMetadataAsync"/> creates the
	/// <c>__Shuttle_BackupInfo</c> table and inserts all key/value pairs correctly.
	/// </summary>
	[Fact]
	public async Task WriteMetadataAsync_WhenCalled_WritesMetadata()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			try
			{
				// Act
				await sut.WriteMetadataAsync(
					new Dictionary<string, string>
					{
						["source_provider"] = "sqlite",
						["export_time"] = "2026-01-15T12:00:00Z"
					});
			}
			finally
			{
				await sut.DisposeAsync();
			}

			// Assert — verify via raw SQLite
			SqliteConnection connection = await OpenRawReadConnectionAsync(filePath);
			try
			{
				Assert.True(await TableExistsAsync(connection, "__Shuttle_BackupInfo"));
				Assert.Equal("sqlite", await ReadMetadataValueAsync(connection, "source_provider"));
				Assert.Equal("2026-01-15T12:00:00Z", await ReadMetadataValueAsync(connection, "export_time"));

				// Assert — key that was never written is not present
				Assert.Null(await ReadMetadataValueAsync(connection, "NonExistentKey"));
			}
			finally
			{
				await connection.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMetadataAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the writer has not been initialized.
	/// </summary>
	[Fact]
	public async Task WriteMetadataAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
					         sut.WriteMetadataAsync(new Dictionary<string, string>()));
				Assert.Equal("Writer is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMetadataAsync"/> throws
	/// <see cref="ArgumentNullException"/> when the <c>metadata</c> parameter is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task WriteMetadataAsync_WhenMetadataIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.WriteMetadataAsync(null!));
				Assert.Equal("metadata", ex.ParamName);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMetadataAsync"/> throws
	/// <see cref="InvalidOperationException"/> when called after
	/// <see cref="SqliteShuttleWriter.FinalizeAsync"/> has been called.
	/// </summary>
	[Fact]
	public async Task WriteMetadataAsync_WhenFinalized_ThrowsInvalidOperationException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			try
			{
				await sut.FinalizeAsync();

				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
					         sut.WriteMetadataAsync(new Dictionary<string, string> { ["key"] = "value" }));
				Assert.Equal("Cannot write metadata after FinalizeAsync() has been called.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMetadataAsync"/> throws
	/// <see cref="ArgumentException"/> when the metadata dictionary contains a reserved key.
	/// </summary>
	[Fact]
	public async Task WriteMetadataAsync_WhenReservedKeyUsed_ThrowsArgumentException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			try
			{
				var metadata = new Dictionary<string, string>
				{
					[SqliteShuttleSchema.ExportStatusKey] = "hacked"
				};

				// Act + Assert
				var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.WriteMetadataAsync(metadata));
				Assert.Equal("metadata", ex.ParamName);
				Assert.StartsWith(
					"Metadata contains one or more keys that are reserved by the shuttle writer.",
					ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMetadataAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the writer has been disposed.
	/// </summary>
	[Fact]
	public async Task WriteMetadataAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
				         sut.WriteMetadataAsync(new Dictionary<string, string>()));
			Assert.Equal(typeof(SqliteShuttleWriter).FullName!, ex.ObjectName);
		}
	}
}
