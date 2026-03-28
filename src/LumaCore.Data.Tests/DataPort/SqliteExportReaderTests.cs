// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Export.Implementations;

using Microsoft.Data.Sqlite;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Unit tests for <see cref="SqliteExportReader"/> and its base class <see cref="SqliteReaderBase"/>,
/// using shared in-memory SQLite databases that require no external infrastructure.
/// </summary>
/// <remarks>
///     <para>
///     Tests use a <b>simplified synthetic schema</b> (<c>Users</c>, <c>Messages</c>) that intentionally differs
///     from the production <c>LumaCoreDbContext</c> schema. This is by design:
///     <see cref="SqliteExportReader"/> is schema-agnostic — it discovers tables and columns dynamically via
///     <c>sqlite_master</c> and <c>PRAGMA table_info</c>. A minimal schema that exercises all column variants
///     (primary key, nullable, NOT NULL) is sufficient to cover every code path.
///     </para>
///     <para>
///     Using the real production schema would couple these tests to the current migration state, causing
///     unrelated test failures whenever entity definitions change.
///     </para>
///     <list type="bullet">
///         <item><c>SqliteExportReaderTests.Construction.cs</c> — Constructor validation</item>
///         <item><c>SqliteExportReaderTests.ReadTableAsync.cs</c> — Schema reading, row streaming, edge cases</item>
///         <item><c>SqliteExportReaderTests.Helpers.cs</c> — Shared test infrastructure</item>
///     </list>
/// </remarks>
[Trait("Category", "DataPort")]
public sealed partial class SqliteExportReaderTests
{
	#region InitializeAsync()

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.InitializeAsync"/> opens a connection and allows
	/// subsequent read operations.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenCalled_OpensConnectionSuccessfully()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreateEmptyDatabaseAsync(cs);
		try
		{
			var sut = new SqliteExportReader(cs);
			try
			{
				// Act
				await sut.InitializeAsync();

				// Assert — no exception, and we can call read operations
				List<string> tables = await sut.GetTableNamesAsync();
				Assert.Empty(tables);
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
	/// Verifies that calling <see cref="SqliteExportReader.InitializeAsync"/> twice throws
	/// <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenCalledTwice_ThrowsInvalidOperationException()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreateEmptyDatabaseAsync(cs);
		try
		{
			var sut = new SqliteExportReader(cs);
			try
			{
				await sut.InitializeAsync();

				// Act + Assert
				var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAsync());
				Assert.Equal("Reader has already been initialized.", ex.Message);
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
	/// Verifies that calling <see cref="SqliteExportReader.InitializeAsync"/> after disposal
	/// throws <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var sut = new SqliteExportReader("Data Source=:memory:");
		await sut.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.InitializeAsync());
		Assert.Equal(typeof(SqliteExportReader).FullName, ex.ObjectName);
	}

	#endregion

	#region GetTableNamesAsync()

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.GetTableNamesAsync"/> returns user table names,
	/// excluding <c>__EFMigrationsHistory</c> and internal <c>sqlite_*</c> tables.
	/// </summary>
	[Fact]
	public async Task GetTableNamesAsync_WhenTablesExist_ReturnsUserTablesOnly()
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
				List<string> tables = await sut.GetTableNamesAsync();

				// Assert — only user tables, sorted alphabetically
				Assert.Equal(2, tables.Count);
				Assert.Equal("Messages", tables[0]);
				Assert.Equal("Users", tables[1]);
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
	/// Verifies that <see cref="SqliteExportReader.GetTableNamesAsync"/> returns an empty list
	/// when no user tables exist.
	/// </summary>
	[Fact]
	public async Task GetTableNamesAsync_WhenNoUserTables_ReturnsEmptyList()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreateEmptyDatabaseAsync(cs);
		try
		{
			SqliteExportReader sut = await CreateInitializedReaderAsync(cs);
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
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="SqliteExportReader.GetTableNamesAsync"/> before initialization
	/// throws <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public async Task GetTableNamesAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var sut = new SqliteExportReader("Data Source=:memory:");
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

	/// <summary>
	/// Verifies that calling <see cref="SqliteExportReader.GetTableNamesAsync"/> after disposal
	/// throws <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public async Task GetTableNamesAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var sut = new SqliteExportReader("Data Source=:memory:");
		await sut.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.GetTableNamesAsync());
		Assert.Equal(typeof(SqliteExportReader).FullName, ex.ObjectName);
	}

	#endregion

	#region GetMigrationHistoryAsync()

	/// <summary>
	/// Verifies that <see cref="SqliteExportReader.GetMigrationHistoryAsync"/> returns migration entries
	/// when the <c>__EFMigrationsHistory</c> table exists.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenTableExists_ReturnsMigrations()
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
				List<MigrationInfo> migrations = await sut.GetMigrationHistoryAsync();

				// Assert — ordered by MigrationId
				Assert.Equal(2, migrations.Count);
				Assert.Equal("20260101000000_Initial", migrations[0].MigrationId);
				Assert.Equal("10.0.0", migrations[0].ProductVersion);
				Assert.Equal("20260201000000_AddMessages", migrations[1].MigrationId);
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
	/// Verifies that <see cref="SqliteExportReader.GetMigrationHistoryAsync"/> returns an empty list
	/// when the <c>__EFMigrationsHistory</c> table does not exist.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenTableDoesNotExist_ReturnsEmptyList()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreateEmptyDatabaseAsync(cs);
		try
		{
			SqliteExportReader sut = await CreateInitializedReaderAsync(cs);
			try
			{
				// Act
				List<MigrationInfo> migrations = await sut.GetMigrationHistoryAsync();

				// Assert
				Assert.Empty(migrations);
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
	/// Verifies that calling <see cref="SqliteExportReader.GetMigrationHistoryAsync"/> before initialization
	/// throws <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var sut = new SqliteExportReader("Data Source=:memory:");
		try
		{
			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetMigrationHistoryAsync());
			Assert.Equal("Reader is not initialized. Call InitializeAsync() first.", ex.Message);
		}
		finally
		{
			await sut.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="SqliteExportReader.GetMigrationHistoryAsync"/> after disposal
	/// throws <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var sut = new SqliteExportReader("Data Source=:memory:");
		await sut.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.GetMigrationHistoryAsync());
		Assert.Equal(typeof(SqliteExportReader).FullName, ex.ObjectName);
	}

	#endregion

	#region DisposeAsync()

	/// <summary>
	/// Verifies that <see cref="SqliteReaderBase.DisposeAsync"/> can be called multiple times
	/// without throwing.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_WhenCalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		string cs = SharedMemoryConnectionString(UniqueDbName());
		SqliteConnection keeper = await CreateEmptyDatabaseAsync(cs);
		try
		{
			var sut = new SqliteExportReader(cs);
			await sut.InitializeAsync();

			// Act + Assert
			await sut.DisposeAsync();
			await sut.DisposeAsync(); // Should not throw.
		}
		finally
		{
			await keeper.DisposeAsync();
		}
	}

	#endregion
}
