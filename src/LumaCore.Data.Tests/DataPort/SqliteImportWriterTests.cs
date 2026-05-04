// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Import.Implementations;
using LumaCore.Data.DataPort.Models;

using Microsoft.Data.Sqlite;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Tests for <see cref="SqliteImportWriter"/> covering construction, checkpoint CRUD, chunked import,
/// resume, and shuttle-ID mismatch handling.
/// </summary>
/// <remarks>
///     <para>
///     All tests use file-based SQLite databases in temporary directories so that per-chunk transactions
///     and checkpoint persistence are exercised against real storage (in-memory SQLite does not support
///     multiple concurrent connections or <c>BEGIN</c>/<c>COMMIT</c> across readers and writers).
///     </para>
///     <list type="bullet">
///         <item><c>SqliteImportWriterTests.Construction.cs</c> — Constructor validation</item>
///         <item><c>SqliteImportWriterTests.PrepareForImport.cs</c> — Checkpoint creation, validation, mismatch</item>
///         <item><c>SqliteImportWriterTests.ImportTable.cs</c> — Fresh import, chunking, resume, edge cases</item>
///         <item>
///         <c>SqliteImportWriterTests.CleanupAfterImport.cs</c> — FK validation, PK preservation, composite keys,
///         sequence reset
///         </item>
///         <item><c>SqliteImportWriterTests.Helpers.cs</c> — Shared test infrastructure</item>
///     </list>
/// </remarks>
[Trait("Category", "DataPort")]
public sealed partial class SqliteImportWriterTests
{
	#region InitializeAsync()

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.InitializeAsync"/> opens a connection and allows
	/// subsequent operations.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenCalled_Succeeds()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("init-valid");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			// Act
			await writer.InitializeAsync();

			// Assert — no exception, and we can call subsequent operations
			await writer.PrepareForImportAsync(TestShuttleId);
		}
		finally
		{
			await writer.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="SqliteImportWriter.InitializeAsync"/> twice throws
	/// <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenCalledTwice_ThrowsInvalidOperationException()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("init-twice");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();

			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.InitializeAsync());
			Assert.Equal("Importer has already been initialized", ex.Message);
		}
		finally
		{
			await writer.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="SqliteImportWriter.InitializeAsync"/> after disposal
	/// throws <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var writer = new SqliteImportWriter("Data Source=dummy.db", TimeProvider.System);
		await writer.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => writer.InitializeAsync());
		Assert.Equal(typeof(SqliteImportWriter).FullName, ex.ObjectName);
	}

	#endregion

	#region CleanupAfterImportAsync()

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.CleanupAfterImportAsync"/> drops the checkpoint table.
	/// </summary>
	[Fact]
	public async Task CleanupAfterImportAsync_WhenCalled_DropsCheckpointTable()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("cleanup");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		(int Id, string Name)[] rows = GenerateRows(10);
		TableSnapshot snapshot = CreateSnapshot("Items", rows);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);
			await writer.ImportTableAsync(snapshot, null, null, 1, 1);

			// Act
			await writer.CleanupAfterImportAsync();
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert
		bool exists = await CheckpointTableExistsAsync(dbPath);
		Assert.False(exists);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.CleanupAfterImportAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the writer has not been initialized.
	/// </summary>
	[Fact]
	public async Task CleanupAfterImportAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var writer = new SqliteImportWriter("Data Source=dummy.db", TimeProvider.System);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CleanupAfterImportAsync());
		Assert.Equal("Importer is not initialized", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.CleanupAfterImportAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the writer has been disposed.
	/// </summary>
	[Fact]
	public async Task CleanupAfterImportAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var writer = new SqliteImportWriter("Data Source=dummy.db", TimeProvider.System);
		await writer.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => writer.CleanupAfterImportAsync());
		Assert.Equal(typeof(SqliteImportWriter).FullName, ex.ObjectName);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.CleanupAfterImportAsync"/> throws
	/// <see cref="InvalidOperationException"/> when imported data violates foreign key constraints.
	/// </summary>
	/// <remarks>
	/// The import writer disables FK checks during import (<c>PRAGMA foreign_keys = OFF</c>) so that
	/// rows can be inserted in arbitrary order. <see cref="SqliteImportWriter.CleanupAfterImportAsync"/>
	/// runs <c>PRAGMA foreign_key_check</c> to catch any violations before they become persistent.
	/// </remarks>
	[Fact]
	public async Task CleanupAfterImportAsync_WhenForeignKeyViolationExists_ThrowsInvalidOperationException()
	{
		// Arrange — create a database with a foreign key constraint.
		using var tempDir = new TemporaryFolder("cleanup-fk-violation");
		string dbPath = Path.Combine(tempDir.Path, "test.db");

		await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
		{
			await conn.OpenAsync();
			await using SqliteCommand cmd = conn.CreateCommand();
			cmd.CommandText = """
			                  CREATE TABLE "Categories" (
			                      "Id" INTEGER PRIMARY KEY
			                  );
			                  CREATE TABLE "Items" (
			                      "Id"         INTEGER PRIMARY KEY,
			                      "CategoryId" INTEGER NOT NULL REFERENCES "Categories"("Id")
			                  );
			                  """;
			await cmd.ExecuteNonQueryAsync();
		}

		// Import an Item that references a non-existent Category (FK violation).
		// The writer disables FK checks during import, so the INSERT succeeds.
		var snapshot = new TableSnapshot
		{
			Name = "Items",
			Columns =
			[
				new ColumnDefinition { Name = "Id", DbType = "INTEGER" },
				new ColumnDefinition { Name = "CategoryId", DbType = "INTEGER" }
			],
			EstimatedRowCount = 1,
			Rows = ToAsyncEnumerable([[1, 999]]) // CategoryId 999 has no matching Category
		};

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);
			await writer.ImportTableAsync(snapshot, null, null, 1, 1);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CleanupAfterImportAsync());
			Assert.Equal(
				"Foreign key integrity check failed. The imported data violates database constraints.",
				ex.Message);
		}
		finally
		{
			await writer.DisposeAsync();
		}
	}

	#endregion

	#region GetMigrationHistoryAsync()

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.GetMigrationHistoryAsync"/> returns the migration
	/// entries that were seeded during database creation.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenMigrationsExist_ReturnsEntries()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("migrations");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();

			// Act
			List<MigrationInfo> migrations = await writer.GetMigrationHistoryAsync();

			// Assert — ordered by MigrationId
			Assert.Equal(2, migrations.Count);
			Assert.Equal("20260101000000_Init", migrations[0].MigrationId);
			Assert.Equal("10.0.0", migrations[0].ProductVersion);
			Assert.Equal("20260201000000_AddOrders", migrations[1].MigrationId);
			Assert.Equal("10.0.0", migrations[1].ProductVersion);
		}
		finally
		{
			await writer.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.GetMigrationHistoryAsync"/> returns an empty list
	/// when the <c>__EFMigrationsHistory</c> table does not exist.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenTableDoesNotExist_ReturnsEmpty()
	{
		// Arrange — create a database without the migrations table.
		using var tempDir = new TemporaryFolder("migrations-nohistory");
		string dbPath = Path.Combine(tempDir.Path, "test.db");

		// Create DB with only a data table (no __EFMigrationsHistory).
		await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
		{
			await conn.OpenAsync();
			await using SqliteCommand cmd = conn.CreateCommand();
			cmd.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT)";
			await cmd.ExecuteNonQueryAsync();
		}

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();

			// Act
			List<MigrationInfo> migrations = await writer.GetMigrationHistoryAsync();

			// Assert
			Assert.Empty(migrations);
		}
		finally
		{
			await writer.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.GetMigrationHistoryAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the writer has not been initialized.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var writer = new SqliteImportWriter("Data Source=dummy.db", TimeProvider.System);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.GetMigrationHistoryAsync());
		Assert.Equal("Importer is not initialized", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.GetMigrationHistoryAsync"/> throws
	/// <see cref="ObjectDisposedException"/> when the writer has been disposed.
	/// </summary>
	[Fact]
	public async Task GetMigrationHistoryAsync_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var writer = new SqliteImportWriter("Data Source=dummy.db", TimeProvider.System);
		await writer.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => writer.GetMigrationHistoryAsync());
		Assert.Equal(typeof(SqliteImportWriter).FullName, ex.ObjectName);
	}

	#endregion

	#region DisposeAsync()

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.DisposeAsync"/> can be called multiple times
	/// without throwing.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_WhenCalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		using var tempDir = new TemporaryFolder("dispose-double");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		SqliteImportWriter writer = CreateWriter(dbPath);
		await writer.InitializeAsync();

		// Act + Assert
		await writer.DisposeAsync();
		await writer.DisposeAsync(); // Should not throw.
	}

	#endregion
}
