// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Models;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleReaderTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.ReadTableAsync"/> returns a <see cref="TableSnapshot"/>
	/// with correct column metadata, estimated row count, and streamed row data.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenTableExists_ReturnsSchemaAndRows()
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
				TableSnapshot snapshot = await sut.ReadTableAsync("Users");

				// Assert — schema
				Assert.Equal("Users", snapshot.Name);
				Assert.Equal(3, snapshot.Columns.Count);

				Assert.Equal("Id", snapshot.Columns[0].Name);
				Assert.Equal("INTEGER", snapshot.Columns[0].DbType);
				Assert.Null(snapshot.Columns[0].ShuttleStorageType);

				Assert.Equal("Name", snapshot.Columns[1].Name);
				Assert.Equal("TEXT", snapshot.Columns[1].DbType);
				Assert.Null(snapshot.Columns[1].ShuttleStorageType);

				Assert.Equal("Email", snapshot.Columns[2].Name);
				Assert.Equal("TEXT", snapshot.Columns[2].DbType);
				Assert.Null(snapshot.Columns[2].ShuttleStorageType);

				// Assert — row count
				Assert.Equal(2, snapshot.EstimatedRowCount);

				// Assert — row data
				var rows = new List<object?[]>();
				await foreach (object?[] row in snapshot.Rows)
				{
					rows.Add(row);
				}

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
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.ReadTableAsync"/> throws
	/// <see cref="KeyNotFoundException"/> when the specified table does not exist in the shuttle file.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenTableDoesNotExist_ThrowsKeyNotFoundException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.ReadTableAsync("NonExistent"));
				Assert.Equal("Table 'NonExistent' does not exist.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.ReadTableAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the reader has not been initialized.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenNotInitialized_ThrowsInvalidOperationException()
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
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ReadTableAsync("Users"));
				Assert.Equal("Reader is not initialized. Call InitializeAsync() first.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.ReadTableAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the reader has been disposed.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			await sut.DisposeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.ReadTableAsync("Users"));
			Assert.Contains(nameof(SqliteShuttleReader), ex.ObjectName);
		}
	}

	/// <summary>
	/// Provides test data for <see cref="ReadTableAsync_WhenTableNameIsInvalid_ThrowsArgumentException"/>.
	/// Each row contains a scenario name, the invalid table name, and the expected exception type.
	/// </summary>
	public static TheoryData<string, string?, Type> InvalidTableNameData => new()
	{
		// null → ArgumentNullException from ArgumentNullException.ThrowIfNull()
		{ "null", null, typeof(ArgumentNullException) },

		// empty → ArgumentException from ArgumentException.ThrowIfNullOrEmpty()
		{ "empty", "", typeof(ArgumentException) }
	};

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReader.ReadTableAsync"/> throws the appropriate
	/// <see cref="ArgumentException"/> (or derived type) when <c>tableName</c> is invalid.
	/// </summary>
	/// <param name="scenario">Human-readable description of the test case.</param>
	/// <param name="tableName">The invalid table name to pass.</param>
	/// <param name="expectedExceptionType">The expected exception type.</param>
	[Theory]
	[MemberData(nameof(InvalidTableNameData))]
	public async Task ReadTableAsync_WhenTableNameIsInvalid_ThrowsArgumentException(
		string  scenario,
		string? tableName,
		Type    expectedExceptionType)
	{
		_ = scenario;

		// Arrange
		(TemporaryFolder tempDir, string filePath) = CreateTempShuttleFilePath();
		using (tempDir)
		{
			await CreateValidShuttleFileAsync(filePath);
			SqliteShuttleReader sut = await CreateInitializedReaderAsync(filePath);
			try
			{
				// Act + Assert
				var ex = (ArgumentException)await Assert.ThrowsAsync(
					                            expectedExceptionType,
					                            () => sut.ReadTableAsync(tableName!));
				Assert.Equal("tableName", ex.ParamName);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
	}
}
