// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleWriterTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMigrationHistoryAsync"/> creates the
	/// <c>__EFMigrationsHistory</c> table and inserts all migration entries correctly.
	/// </summary>
	[Fact]
	public async Task WriteMigrationHistoryAsync_WhenCalled_WritesMigrations()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			try
			{
				// Act
				await sut.WriteMigrationHistoryAsync(
				[
					new MigrationInfo("20260101000000_Initial", "10.0.0"),
					new MigrationInfo("20260201000000_AddMessages", "10.0.0")
				]);
			}
			finally
			{
				await sut.DisposeAsync();
			}

			// Assert — verify via raw SQLite
			SqliteConnection connection = await OpenRawReadConnectionAsync(filePath);
			try
			{
				Assert.True(await TableExistsAsync(connection, "__EFMigrationsHistory"));

				List<object?[]> rows = await ReadAllRowsAsync(connection, "__EFMigrationsHistory");
				Assert.Equal(2, rows.Count);

				Assert.Equal("20260101000000_Initial", rows[0][0]);
				Assert.Equal("10.0.0", rows[0][1]);
				Assert.Equal("20260201000000_AddMessages", rows[1][0]);
				Assert.Equal("10.0.0", rows[1][1]);
			}
			finally
			{
				await connection.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMigrationHistoryAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the writer has not been initialized.
	/// </summary>
	[Fact]
	public async Task WriteMigrationHistoryAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			var sut = new SqliteShuttleWriter(filePath, NullLogger.Instance, TimeProvider.System);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.WriteMigrationHistoryAsync([]));
				Assert.Equal("Writer is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMigrationHistoryAsync"/> throws
	/// <see cref="ArgumentNullException"/> when the <c>migrations</c> parameter is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task WriteMigrationHistoryAsync_WhenMigrationsIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.WriteMigrationHistoryAsync(null!));
				Assert.Equal("migrations", ex.ParamName);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMigrationHistoryAsync"/> throws
	/// <see cref="InvalidOperationException"/> when called after
	/// <see cref="SqliteShuttleWriter.FinalizeAsync"/> has been called.
	/// </summary>
	[Fact]
	public async Task WriteMigrationHistoryAsync_WhenFinalized_ThrowsInvalidOperationException()
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
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.WriteMigrationHistoryAsync([]));
				Assert.Equal(
					"Cannot write migration history after FinalizeAsync() has been called.",
					ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteMigrationHistoryAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the writer has been disposed.
	/// </summary>
	[Fact]
	public async Task WriteMigrationHistoryAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.WriteMigrationHistoryAsync([]));
			Assert.Equal(typeof(SqliteShuttleWriter).FullName!, ex.ObjectName);
		}
	}
}
