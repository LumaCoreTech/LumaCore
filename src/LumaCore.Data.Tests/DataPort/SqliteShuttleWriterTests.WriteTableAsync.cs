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
	/// Verifies that <see cref="SqliteShuttleWriter.WriteTableAsync"/> creates the table with correct
	/// schema and inserts all rows, including <see langword="null"/> values.
	/// </summary>
	[Fact]
	public async Task WriteTableAsync_WhenTableHasData_WritesSuccessfully()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			try
			{
				// Act
				await sut.WriteTableAsync(
					CreateUsersTableSnapshot(),
					null,
					null,
					1,
					1);
			}
			finally
			{
				await sut.DisposeAsync();
			}

			// Assert — verify via raw SQLite
			SqliteConnection connection = await OpenRawReadConnectionAsync(filePath);
			try
			{
				// Verify only the expected table was created — no side-effect tables
				List<string> tables = await GetAllTableNamesAsync(connection);
				Assert.Single(tables);
				Assert.Equal("Users", tables[0]);

				// Verify rows
				List<object?[]> rows = await ReadAllRowsAsync(connection, "Users");
				Assert.Equal(2, rows.Count);

				Assert.Equal(1L, rows[0][0]);
				Assert.Equal("Alice", rows[0][1]);
				Assert.Equal("alice@test.com", rows[0][2]);

				Assert.Equal(2L, rows[1][0]);
				Assert.Equal("Bob", rows[1][1]);
				Assert.Null(rows[1][2]);
			}
			finally
			{
				await connection.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteTableAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the writer has not been initialized.
	/// </summary>
	[Fact]
	public async Task WriteTableAsync_WhenNotInitialized_ThrowsInvalidOperationException()
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
					         sut.WriteTableAsync(
						         CreateUsersTableSnapshot(),
						         null,
						         null,
						         1,
						         1));
				Assert.Equal("Writer is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteTableAsync"/> throws
	/// <see cref="ArgumentNullException"/> when the <c>table</c> parameter is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task WriteTableAsync_WhenTableIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
					         sut.WriteTableAsync(
						         null!,
						         null,
						         null,
						         1,
						         1));
				Assert.Equal("table", ex.ParamName);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteTableAsync"/> throws
	/// <see cref="InvalidOperationException"/> when called after
	/// <see cref="SqliteShuttleWriter.FinalizeAsync"/> has been called.
	/// </summary>
	[Fact]
	public async Task WriteTableAsync_WhenFinalized_ThrowsInvalidOperationException()
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
					         sut.WriteTableAsync(
						         CreateUsersTableSnapshot(),
						         null,
						         null,
						         1,
						         1));
				Assert.Equal("Cannot write new tables after FinalizeAsync() has been called.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleWriter.WriteTableAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the writer has been disposed.
	/// </summary>
	[Fact]
	public async Task WriteTableAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFile();
		using (tempDir)
		{
			SqliteShuttleWriter sut = await CreateInitializedWriterAsync(filePath);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
				         sut.WriteTableAsync(CreateUsersTableSnapshot(), null, null, 1, 1));
			Assert.Equal(typeof(SqliteShuttleWriter).FullName!, ex.ObjectName);
		}
	}
}
