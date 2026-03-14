// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Export.Implementations;
using LumaCore.Data.DataPort.Models;

using Microsoft.Data.Sqlite;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteExportReaderTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.ReadTableAsync"/> returns a <see cref="TableSnapshot"/>
	/// with correct column metadata and row data.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenTableHasData_ReturnsSchemaAndRows()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreatePopulatedDatabaseAsync(cs);
		try
		{
			SqliteExportReader sut = await CreateInitializedReaderAsync(cs);
			try
			{
				// Act
				TableSnapshot snapshot = await sut.ReadTableAsync("Users");

				// Assert — schema
				Assert.Equal("Users", snapshot.Name);
				Assert.Equal(3, snapshot.Columns.Count);
				Assert.Equal("Id", snapshot.Columns[0].Name);
				Assert.Equal("Name", snapshot.Columns[1].Name);
				Assert.Equal("Email", snapshot.Columns[2].Name);
				Assert.True(snapshot.Columns[0].IsPrimaryKey);
				Assert.False(snapshot.Columns[1].IsNullable);
				Assert.True(snapshot.Columns[2].IsNullable);

				// Assert — row count
				Assert.Equal(2, snapshot.EstimatedRowCount);

				// Assert — row data
				var rows = new List<object?[]>();
				await foreach (object?[] row in snapshot.Rows)
				{
					rows.Add(row);
				}

				Assert.Equal(2, rows.Count);
				Assert.Equal(1L, rows[0][0]);               // Id
				Assert.Equal("Alice", rows[0][1]);          // Name
				Assert.Equal("alice@test.com", rows[0][2]); // Email
				Assert.Equal(2L, rows[1][0]);               // Id
				Assert.Equal("Bob", rows[1][1]);            // Name
				Assert.Null(rows[1][2]);                    // Email is NULL → converted to null
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.ReadTableAsync"/> returns a <see cref="TableSnapshot"/>
	/// with an empty row stream and <c>EstimatedRowCount = 0</c> when the table exists but has no rows.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenTableIsEmpty_ReturnsSchemaWithZeroRows()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreateEmptyDatabaseAsync(cs);
		try
		{
			SqliteCommand cmd = keeper.CreateCommand();
			try
			{
				cmd.CommandText = """CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);""";
				await cmd.ExecuteNonQueryAsync();
			}
			finally
			{
				await cmd.DisposeAsync();
			}

			SqliteExportReader sut = await CreateInitializedReaderAsync(cs);
			try
			{
				// Act
				TableSnapshot snapshot = await sut.ReadTableAsync("Items");

				// Assert — schema
				Assert.Equal("Items", snapshot.Name);
				Assert.Equal(2, snapshot.Columns.Count);
				Assert.Equal("Id", snapshot.Columns[0].Name);
				Assert.Equal("Name", snapshot.Columns[1].Name);

				// Assert — no rows
				Assert.Equal(0, snapshot.EstimatedRowCount);
				var rows = new List<object?[]>();
				await foreach (object?[] row in snapshot.Rows)
				{
					rows.Add(row);
				}
				Assert.Empty(rows);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.ReadTableAsync"/> populates
	/// <see cref="ColumnDefinition.ShuttleStorageType"/> when a <c>shuttleTypeMapper</c> is provided.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenShuttleTypeMapperProvided_PopulatesShuttleStorageType()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreatePopulatedDatabaseAsync(cs);
		try
		{
			var sut = new SqliteExportReader(cs, dbType => dbType.ToUpperInvariant());
			try
			{
				await sut.InitializeAsync();

				// Act
				TableSnapshot snapshot = await sut.ReadTableAsync("Users");

				// Assert — ShuttleStorageType is populated by the mapper
				Assert.Equal("INTEGER", snapshot.Columns[0].ShuttleStorageType); // Id: INTEGER
				Assert.Equal("TEXT", snapshot.Columns[1].ShuttleStorageType);    // Name: TEXT
				Assert.Equal("TEXT", snapshot.Columns[2].ShuttleStorageType);    // Email: TEXT
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.ReadTableAsync"/> gracefully degrades when the
	/// row-count query fails with a non-cancellation exception — the snapshot is still returned with
	/// <see cref="TableSnapshot.EstimatedRowCount"/> set to <c>-1</c>.
	/// </summary>
	/// <remarks>
	/// Uses <see cref="ExecutionStageMonitor.ThrowAt"/> to inject an <see cref="IOException"/> at the
	/// <c>ReadTable.CountRows</c> stage, exercising the <c>catch (Exception)</c> fallback path in
	/// <see cref="SqliteReaderBase"/>.
	/// </remarks>
	[Fact]
	public async Task ReadTableAsync_WhenCountRowsFails_ReturnsSnapshotWithUnknownRowCount()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreatePopulatedDatabaseAsync(cs);
		try
		{
			SqliteExportReader sut = await CreateInitializedReaderAsync(cs);
			try
			{
				using ExecutionStageMonitor monitor = ExecutionStageMonitor
					.Configure()
					.ThrowAt("ReadTable.CountRows", new IOException("Simulated disk failure"));

				// Act
				TableSnapshot snapshot = await sut.ReadTableAsync("Users");

				// Assert — schema is intact, but row count fell back to -1.
				Assert.Equal("Users", snapshot.Name);
				Assert.Equal(3, snapshot.Columns.Count);
				Assert.Equal("Id", snapshot.Columns[0].Name);
				Assert.Equal("Name", snapshot.Columns[1].Name);
				Assert.Equal("Email", snapshot.Columns[2].Name);
				Assert.Equal(-1, snapshot.EstimatedRowCount);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.ReadTableAsync"/> throws
	/// <see cref="InvalidOperationException"/> for a table that does not exist.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenTableDoesNotExist_ThrowsInvalidOperationException()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreateEmptyDatabaseAsync(cs);
		try
		{
			SqliteExportReader sut = await CreateInitializedReaderAsync(cs);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ReadTableAsync("NonExistent"));
				Assert.Equal("Table 'NonExistent' does not exist.", ex.Message);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.ReadTableAsync"/> throws
	/// <see cref="ArgumentNullException"/> when the table name is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenTableNameIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreateEmptyDatabaseAsync(cs);
		try
		{
			SqliteExportReader sut = await CreateInitializedReaderAsync(cs);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReadTableAsync(null!));
				Assert.Equal("tableName", ex.ParamName);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.ReadTableAsync"/> throws
	/// <see cref="ArgumentException"/> when the table name is empty.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenTableNameIsEmpty_ThrowsArgumentException()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreateEmptyDatabaseAsync(cs);
		try
		{
			SqliteExportReader sut = await CreateInitializedReaderAsync(cs);
			try
			{
				// Act + Assert
				var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.ReadTableAsync(""));
				Assert.Equal("tableName", ex.ParamName);
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="SqliteExportReader.ReadTableAsync"/> before initialization
	/// throws <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var sut = new SqliteExportReader("Data Source=:memory:");
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

	/// <summary>
	/// Verifies that calling <see cref="SqliteExportReader.ReadTableAsync"/> after disposal
	/// throws <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public async Task ReadTableAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var sut = new SqliteExportReader("Data Source=:memory:");
		await sut.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.ReadTableAsync("Users"));
		Assert.Equal(typeof(SqliteExportReader).FullName, ex.ObjectName);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.ReadTableAsync"/> propagates
	/// <see cref="OperationCanceledException"/> when the cancellation token is triggered during
	/// the row-count query phase.
	/// </summary>
	/// <remarks>
	/// Uses <see cref="ExecutionStageMonitor"/> to deterministically cancel at the
	/// <c>ReadTable.CountRows</c> stage — after column metadata has been read but before the
	/// <c>SELECT COUNT(*)</c> query executes.
	/// </remarks>
	[Fact]
	public async Task ReadTableAsync_WhenCancelledDuringCountRows_ThrowsOperationCanceledException()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreatePopulatedDatabaseAsync(cs);
		try
		{
			SqliteExportReader sut = await CreateInitializedReaderAsync(cs);
			try
			{
				using ExecutionStageMonitor monitor = ExecutionStageMonitor
					.Configure()
					.CancelAt("ReadTable.CountRows", out CancellationToken token);

				// Act + Assert
				// The operation should be canceled during the row-count phase, resulting in an OperationCanceledException.
				// The cancellation token becomes part of a linked token source that is passed down to the count query,
				// so we cannot check for the token in the exception.
				await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ReadTableAsync("Users", token));
			}
			finally
			{
				await sut.DisposeAsync();
			}
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}
}
