// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleReaderTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetCreatedUtcAsync"/> returns the correct
	/// <see cref="DateTimeOffset"/> when a valid ISO 8601 timestamp is stored in the shuttle metadata.
	/// </summary>
	[Fact]
	public async Task GetCreatedUtcAsync_WhenTimestampPresent_ReturnsParsedValue()
	{
		// Arrange — use FakeTimeProvider so the exact timestamp is deterministic.
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			var expectedUtc = new DateTimeOffset(2026, 3, 15, 8, 30, 0, TimeSpan.Zero);
			var fakeTime = new FakeTimeProvider(expectedUtc);

			// FinalizeAsync stamps the CreatedUtc metadata key from the time provider.
			var writer = new SqliteShuttleWriter(filePath, NullLogger.Instance, fakeTime);
			try
			{
				await writer.InitializeAsync();
				await writer.FinalizeAsync();
			}
			finally
			{
				await writer.DisposeAsync();
			}

			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act
				DateTimeOffset? result = await sut.GetCreatedUtcAsync();

				// Assert
				Assert.NotNull(result);
				Assert.Equal(expectedUtc, result.Value);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetCreatedUtcAsync"/> returns <see langword="null"/>
	/// when the <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata key is missing from the
	/// shuttle file.
	/// </summary>
	[Fact]
	public async Task GetCreatedUtcAsync_WhenKeyMissing_ReturnsNull()
	{
		// Arrange — create a shuttle file, then remove the CreatedUtc key.
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			// The writer always stamps CreatedUtc during FinalizeAsync, so we must tamper
			// with the file afterwards to simulate a missing key.
			await CreateEmptyShuttleFileAsync(filePath);
			string connectionString = new SqliteConnectionStringBuilder
			{
				DataSource = filePath, Pooling = false
			}.ConnectionString;

			// Directly manipulate the SQLite file to remove the CreatedUtc key from the metadata table.
			var connection = new SqliteConnection(connectionString);
			try
			{
				await connection.OpenAsync();
				SqliteCommand cmd = connection.CreateCommand();
				try
				{
					cmd.CommandText = """DELETE FROM "__Shuttle_BackupInfo" WHERE "key" = 'CreatedUtc'""";
					await cmd.ExecuteNonQueryAsync();
				}
				finally
				{
					await cmd.DisposeAsync();
				}
			}
			finally
			{
				await connection.DisposeAsync();
			}

			// Now read the file with the shuttle reader.
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act
				DateTimeOffset? result = await sut.GetCreatedUtcAsync();

				// Assert
				Assert.Null(result);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetCreatedUtcAsync"/> returns <see langword="null"/>
	/// when the <see cref="SqliteShuttleSchema.CreatedUtcKey"/> value cannot be parsed as a
	/// <see cref="DateTimeOffset"/>.
	/// </summary>
	[Fact]
	public async Task GetCreatedUtcAsync_WhenValueInvalid_ReturnsNull()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			// Deliberately unparseable — not valid ISO 8601.
			await CreateShuttleFileWithCustomCreatedUtcAsync(filePath, "not-a-date");
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act
				DateTimeOffset? result = await sut.GetCreatedUtcAsync();

				// Assert
				Assert.Null(result);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetCreatedUtcAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the reader has not been initialized.
	/// </summary>
	[Fact]
	public async Task GetCreatedUtcAsync_WhenNotInitialized_ThrowsInvalidOperationException()
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
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetCreatedUtcAsync());
				Assert.Equal("Reader is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.GetCreatedUtcAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the reader has been disposed.
	/// </summary>
	[Fact]
	public async Task GetCreatedUtcAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.GetCreatedUtcAsync());
			Assert.Contains(nameof(SqliteShuttleReader), ex.ObjectName);
		}
	}
}
