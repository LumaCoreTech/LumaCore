// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Import.Implementations;
using LumaCore.Data.DataPort.Models;

using Microsoft.Data.Sqlite;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

// Data fidelity through the import pipeline: PK preservation, FK integrity, and post-import cleanup.
//
// The import pipeline must guarantee that every row arrives in the target database exactly as it
// left the source. These tests walk through the critical invariants one by one:
//
//   1. Auto-increment PKs must survive with their original values (PreservesExactPkValues).
//      If a PK shifts, every FK that points to it silently breaks.
//
//   2. Composite PKs (join tables without AUTOINCREMENT) must be handled by the same generic
//      INSERT strategy (PreservesAllKeyValues).
//
//   3. FK-dependent tables must import in dependency order — parents before children — and
//      the FK integrity check in CleanupAfterImportAsync must pass (PassesFkCheckOnCleanup).
//
//   4. If an import produces orphaned FK references, CleanupAfterImportAsync must catch it
//      via PRAGMA foreign_key_check (ThrowsInvalidOperationException).
//
//   5. After the import is done, the auto-increment counters must be reset so that the next
//      application-level INSERT gets MAX(imported Id) + 1 (ResetsAutoIncrementCounters).
public sealed partial class SqliteImportWriterTests
{
	#region Import data fidelity & CleanupAfterImportAsync()

	// --- 1. PK preservation: the foundation everything else depends on ---

	/// <summary>
	/// Verifies that auto-increment PK values are preserved exactly through the import roundtrip —
	/// each imported row retains the PK it had in the source database.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Why this matters:</b> The import pipeline must INSERT with explicit <c>Id</c> values.
	///     If the INSERT omits the PK column or uses <c>NULL</c>, SQLite assigns a new auto-increment
	///     value, silently breaking all FK references that depend on the original PK. This is the
	///     foundation: if PKs don't survive, nothing else (FKs, sequences) can be correct.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task ImportTableAsync_WhenAutoIncrementPk_PreservesExactPkValues()
	{
		// Arrange — non-sequential PKs with gaps and high values. If the import pipeline
		// silently re-assigns PKs (e.g., by omitting the Id column), these specific values
		// would be replaced by 1, 2, 3 — and the assertion catches that immediately.
		using var tempDir = new TemporaryFolder("import-pk-preserve");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		(int Id, string Name)[] rows = [(5, "Five"), (42, "FortyTwo"), (1000, "Thousand")];
		TableSnapshot snapshot = CreateSnapshot("Items", rows);

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);
			await writer.ImportTableAsync(snapshot, null, null, 1, 1);
			await writer.CleanupAfterImportAsync();
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert — PKs are exactly as imported, not reassigned.
		List<(int Id, string Name)> actual = await ReadAllRowsAsync(dbPath, "Items");
		Assert.Equal(3, actual.Count);
		Assert.Equal((5, "Five"), actual[0]);
		Assert.Equal((42, "FortyTwo"), actual[1]);
		Assert.Equal((1000, "Thousand"), actual[2]);
	}

	// --- 2. Composite PKs: same INSERT strategy, different key shape ---

	/// <summary>
	/// Verifies that importing a table with a composite primary key (two-column PK, no
	/// <c>AUTOINCREMENT</c>) preserves all key values through the import roundtrip.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Why this matters:</b> The import pipeline constructs <c>INSERT INTO ... (col1, col2, ...)</c>
	///     from <see cref="TableSnapshot.Columns"/>. Composite PKs have no <c>AUTOINCREMENT</c>, so there
	///     is no sequence to reset — but the INSERT must still include both key columns explicitly. This
	///     test proves that the generic column-based INSERT strategy handles composite PKs correctly.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task ImportTableAsync_WhenTableHasCompositePrimaryKey_PreservesAllKeyValues()
	{
		// Arrange — a join table modeled after ConversationParticipantEntity: the PK is
		// (ConversationId, ParticipantId), there's no AUTOINCREMENT, and both columns
		// must appear in the INSERT explicitly.
		using var tempDir = new TemporaryFolder("import-composite-pk");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateCompositePkTestDatabaseAsync(dbPath);

		var snapshot = new TableSnapshot
		{
			Name = "Memberships",
			Columns =
			[
				new ColumnDefinition { Name = "ConversationId", DbType = "INTEGER" },
				new ColumnDefinition { Name = "ParticipantId", DbType = "INTEGER" },
				new ColumnDefinition { Name = "JoinedAtUtc", DbType = "TEXT" }
			],
			EstimatedRowCount = 3,
			Rows = ToAsyncEnumerable(
			[
				[10, 20, "2026-01-01T00:00:00Z"],
				[10, 30, "2026-01-02T00:00:00Z"],
				[11, 20, "2026-01-03T00:00:00Z"]
			])
		};

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);

			// Act
			await writer.ImportTableAsync(snapshot, null, null, 1, 1);
			await writer.CleanupAfterImportAsync();
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert — all 3 rows present with correct composite keys.
		await using var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		await using SqliteCommand cmd = conn.CreateCommand();
		cmd.CommandText = """
		                  SELECT "ConversationId", "ParticipantId", "JoinedAtUtc"
		                  FROM "Memberships"
		                  ORDER BY "ConversationId", "ParticipantId"
		                  """;

		var rows = new List<(long ConvId, long PartId, string Joined)>();
		await using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			rows.Add((reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)));
		}

		Assert.Equal(3, rows.Count);
		Assert.Equal((10, 20, "2026-01-01T00:00:00Z"), rows[0]);
		Assert.Equal((10, 30, "2026-01-02T00:00:00Z"), rows[1]);
		Assert.Equal((11, 20, "2026-01-03T00:00:00Z"), rows[2]);
	}

	// --- 3. FK-dependent tables: import order matters, cleanup validates ---

	/// <summary>
	/// Verifies that importing multiple FK-dependent tables in the correct order succeeds and
	/// passes the <c>PRAGMA foreign_key_check</c> during <see cref="SqliteImportWriter.CleanupAfterImportAsync"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Why this matters:</b> In production, the export reader emits tables in dependency order
	///     (parents before children). The import writer disables FK checks during import and re-validates
	///     during cleanup. This test proves the full happy-path: parent → child import order, FK check
	///     passes, no orphaned rows.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task ImportTableAsync_WhenMultipleFkDependentTables_PassesFkCheckOnCleanup()
	{
		// Arrange — schema: Parents(Id PK) → Children(Id PK, ParentId FK → Parents).
		// FK checks are disabled during import (InitializeAsync sets PRAGMA foreign_keys = OFF),
		// so the import order doesn't cause constraint errors at INSERT time.
		using var tempDir = new TemporaryFolder("import-fk-order");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateFkTestDatabaseAsync(dbPath);

		var parentSnapshot = new TableSnapshot
		{
			Name = "Parents",
			Columns =
			[
				new ColumnDefinition { Name = "Id", DbType = "INTEGER" },
				new ColumnDefinition { Name = "Name", DbType = "TEXT" }
			],
			EstimatedRowCount = 2,
			Rows = ToAsyncEnumerable(
			[
				[1, "Alice"],
				[2, "Bob"]
			])
		};

		var childSnapshot = new TableSnapshot
		{
			Name = "Children",
			Columns =
			[
				new ColumnDefinition { Name = "Id", DbType = "INTEGER" },
				new ColumnDefinition { Name = "ParentId", DbType = "INTEGER" },
				new ColumnDefinition { Name = "Label", DbType = "TEXT" }
			],
			EstimatedRowCount = 3,
			Rows = ToAsyncEnumerable(
			[
				[10, 1, "Child_A1"],
				[11, 1, "Child_A2"],
				[12, 2, "Child_B1"]
			])
		};

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);

			// Act — import parent first, then children (dependency order).
			await writer.ImportTableAsync(parentSnapshot, null, null, 1, 2);
			await writer.ImportTableAsync(childSnapshot, null, null, 2, 2);

			// CleanupAfterImportAsync runs PRAGMA foreign_key_check — if any child row
			// references a nonexistent parent, it throws here. The next test proves that.
			await writer.CleanupAfterImportAsync();
		}
		finally
		{
			await writer.DisposeAsync();
		}

		// Assert — all rows present, FKs intact (CleanupAfterImportAsync would have thrown otherwise).
		await using var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		await using SqliteCommand parentCmd = conn.CreateCommand();
		parentCmd.CommandText = """SELECT COUNT(*) FROM "Parents" """;
		long parentCount = (long)(await parentCmd.ExecuteScalarAsync())!;
		Assert.Equal(2, parentCount);

		await using SqliteCommand childCmd = conn.CreateCommand();
		childCmd.CommandText = """SELECT COUNT(*) FROM "Children" """;
		long childCount = (long)(await childCmd.ExecuteScalarAsync())!;
		Assert.Equal(3, childCount);
	}

	// --- 4. FK violation detection: the safety net when something went wrong ---

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.CleanupAfterImportAsync"/> detects FK constraint
	/// violations introduced by the imported data and throws <see cref="InvalidOperationException"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is the counterpart to the previous test: instead of a valid parent → child import,
	///     we import an orphaned child row (ParentId = 999, no matching parent). The import itself
	///     succeeds because FKs are disabled — but <c>CleanupAfterImportAsync</c> must catch it.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task CleanupAfterImportAsync_WhenFkViolationExists_ThrowsInvalidOperationException()
	{
		// Arrange — same FK schema as the previous test, but this time we skip importing
		// the parent and go straight to a child that references ParentId = 999 (nonexistent).
		// The import succeeds (FKs off), but cleanup must reject the data.
		using var tempDir = new TemporaryFolder("import-fk-violation");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateFkTestDatabaseAsync(dbPath);

		var childSnapshot = new TableSnapshot
		{
			Name = "Children",
			Columns =
			[
				new ColumnDefinition { Name = "Id", DbType = "INTEGER" },
				new ColumnDefinition { Name = "ParentId", DbType = "INTEGER" },
				new ColumnDefinition { Name = "Label", DbType = "TEXT" }
			],
			EstimatedRowCount = 1,
			Rows = ToAsyncEnumerable([[1, 999, "Orphan"]])
		};

		SqliteImportWriter writer = CreateWriter(dbPath);
		try
		{
			await writer.InitializeAsync();
			await writer.PrepareForImportAsync(TestShuttleId);
			await writer.ImportTableAsync(childSnapshot, null, null, 1, 1);

			// Act + Assert — PRAGMA foreign_key_check finds the orphan and rejects the import.
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

	// --- 5. Sequence reset: the last mile after a successful import ---

	/// <summary>
	/// Verifies that <see cref="SqliteImportWriter.CleanupAfterImportAsync"/> resets the
	/// <c>sqlite_sequence</c> auto-increment counter to the maximum imported <c>rowid</c> for each table.
	/// A new row inserted after cleanup must receive <c>MAX(imported Id) + 1</c>, not <c>1</c> or a
	/// value that collides with imported data.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Why this matters:</b> This is the last step in the import pipeline. PKs are correct
	///     (test 1), composite keys work (test 2), FK integrity is validated (tests 3–4) — but if
	///     the auto-increment counter isn't reset, the very first application-level INSERT after
	///     the restore would collide with imported data (<c>UNIQUE constraint failed</c>).
	///     </para>
	/// </remarks>
	[Fact]
	public async Task CleanupAfterImportAsync_WhenCalled_ResetsAutoIncrementCounters()
	{
		// Arrange — import rows with explicit IDs starting at 100. After cleanup, the
		// sqlite_sequence counter for "Items" must be 102 so the next auto-increment
		// INSERT yields 103.
		using var tempDir = new TemporaryFolder("import-autoinc-reset");
		string dbPath = Path.Combine(tempDir.Path, "test.db");
		await CreateTestDatabaseAsync(dbPath);

		(int Id, string Name)[] rows = [(100, "Row_100"), (101, "Row_101"), (102, "Row_102")];
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

		// Assert — insert a new row WITHOUT explicit Id and verify it gets 103, not 1.
		// This simulates the first application-level INSERT after a restore.
		await using var conn = new SqliteConnection($"Data Source={dbPath}");
		await conn.OpenAsync();

		await using SqliteCommand insertCmd = conn.CreateCommand();
		insertCmd.CommandText = """INSERT INTO "Items" ("Name") VALUES ('NewRow')""";
		await insertCmd.ExecuteNonQueryAsync();

		await using SqliteCommand readCmd = conn.CreateCommand();
		readCmd.CommandText = """SELECT MAX("Id") FROM "Items" """;
		long maxId = (long)(await readCmd.ExecuteScalarAsync())!;

		Assert.Equal(103, maxId);
	}

	#endregion
}
