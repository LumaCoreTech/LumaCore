// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleWriterTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.FinalizeAsync"/> writes all four completion markers
	/// (<see cref="SqliteShuttleSchema.ExportStatusKey"/>, <see cref="SqliteShuttleSchema.ShuttleFormatVersionKey"/>,
	/// <see cref="SqliteShuttleSchema.ShuttleIdKey"/>, and <see cref="SqliteShuttleSchema.CreatedUtcKey"/>)
	/// to the backup info table.
	/// </summary>
	[Fact]
	public async Task FinalizeAsync_WhenCalled_WritesAllCompletionMarkers()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var expectedUtc = new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
			var fakeTime = new FakeTimeProvider(expectedUtc);

			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, fakeTime);
			try
			{
				await sut.InitializeAsync();

				// Act
				await sut.FinalizeAsync();
			}
			finally
			{
				await sut.DisposeAsync();
			}

			// Assert — verify all four markers via raw SQLite
			SqliteConnection connection = await OpenRawReadConnectionAsync(filePath);
			try
			{
				// ExportStatus
				string? status = await ReadMetadataValueAsync(connection, "ExportStatus");
				Assert.Equal("Completed", status);

				// ShuttleFormatVersion
				string? version = await ReadMetadataValueAsync(connection, "ShuttleFormatVersion");
				Assert.Equal(
					SqliteShuttleSchema.CurrentShuttleFormatVersion.ToString(CultureInfo.InvariantCulture),
					version);

				// ShuttleId — must be a valid GUID
				string? shuttleId = await ReadMetadataValueAsync(connection, "ShuttleId");
				Assert.NotNull(shuttleId);
				Assert.True(Guid.TryParse(shuttleId, out Guid _), $"ShuttleId '{shuttleId}' is not a valid GUID.");

				// CreatedUtc — must match the deterministic timestamp
				string? createdUtc = await ReadMetadataValueAsync(connection, "CreatedUtc");
				Assert.Equal(expectedUtc.ToString("o"), createdUtc);
			}
			finally
			{
				await connection.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.FinalizeAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the writer has not been initialized.
	/// </summary>
	[Fact]
	public async Task FinalizeAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.FinalizeAsync());
				Assert.Equal("Writer is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.FinalizeAsync"/> throws
	/// <see cref="InvalidOperationException"/> when called twice.
	/// </summary>
	[Fact]
	public async Task FinalizeAsync_WhenCalledTwice_ThrowsInvalidOperationException()
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
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.FinalizeAsync());
				Assert.Equal(
					"FinalizeAsync() has already been called. This method must be called exactly once.",
					ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.FinalizeAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the writer has been disposed.
	/// </summary>
	[Fact]
	public async Task FinalizeAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.FinalizeAsync());
			Assert.Equal(typeof(SqliteShuttleWriter).FullName!, ex.ObjectName);
		}
	}
}
