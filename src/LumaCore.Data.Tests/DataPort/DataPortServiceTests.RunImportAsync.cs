// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Models;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

// RunImportAsync() orchestration: progress reporting, guard clauses, and catch blocks that
// cannot be fully exercised via the happy-path integration tests (DataPortRoundtripTests).
//
// The tests start with parameter validation, then a valid scenario (progress reporting),
// followed by the import pipeline's validation order and cross-cutting error behavior:
//
//   1. Progress reporting — non-null IProgress<T> handler receives all stages
//      (WithProgressAndTwoTables).
//
//   2. Parameter validation — null guards for shuttleReader and targetImporter
//      (WhenShuttleReaderIsNull, WhenTargetImporterIsNull).
//
//   3. Empty migration history — shuttle or target returns zero migrations
//      (WhenMigrationHistoryIsEmpty: both empty, only shuttle, only target).
//
//   4. Schema mismatch — migration IDs differ between shuttle and target
//      (WhenSchemaMismatch: index mismatch, different-length histories).
//
//   5. Missing ShuttleId — metadata lacks the required identity key
//      (WhenShuttleIdMissing).
//
//   6. Cancellation — OperationCanceledException catch block
//      (WhenCancelled).
//
//   7. General failure — non-cancellation exception catch block
//      (WhenImporterFails).
//
// For constructor and SourceProviderKey, see the anchor file.
// For RunExportAsync() coverage, see RunExportAsync.
// For shared stubs and helpers, see Helpers.
public sealed partial class DataPortServiceTests
{
	// --- 1. Progress reporting ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunImportAsync"/> sends all expected progress reports
	/// (initialization, per-table, finalization, completion) when a non-<see langword="null"/>
	/// <see cref="IProgress{T}"/> handler is provided and two tables are imported. Using two tables
	/// validates that the step arithmetic (<c>totalSteps = tableCount + 2</c>) and per-table numbering
	/// (<c>(1/N)</c>, <c>(2/N)</c>) scale correctly beyond the trivial single-table case.
	/// </summary>
	[Fact]
	public async Task RunImportAsync_WithProgressAndTwoTables_ReportsAllStages()
	{
		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);
		var progress = new CapturingProgress();

		var snapshots = new Dictionary<string, TableSnapshot>
		{
			["Alpha"] = new()
			{
				Name = "Alpha",
				Columns = [new ColumnDefinition { Name = "Id", DbType = "INTEGER" }],
				Rows = EmptyRowStream()
			},
			["Beta"] = new()
			{
				Name = "Beta",
				Columns = [new ColumnDefinition { Name = "Id", DbType = "INTEGER" }],
				Rows = EmptyRowStream()
			}
		};
		var reader = new StubShuttleReader
		{
			OnGetMigrationHistory = _ => Task.FromResult<List<MigrationInfo>>(
				[new MigrationInfo("20260101_Initial", "10.0.0")]),
			OnGetMetadata = _ => Task.FromResult(new Dictionary<string, string> { { "ShuttleId", "test-shuttle-id" } }),
			OnGetTableNames = _ => Task.FromResult(new List<string> { "Alpha", "Beta" }),
			OnReadTable = (name, _) => Task.FromResult(snapshots[name])
		};
		var writer = new StubImportWriter
		{
			OnGetMigrationHistory = _ => Task.FromResult<List<MigrationInfo>>(
				[new MigrationInfo("20260101_Initial", "10.0.0")])
		};

		// Act
		await sut.RunImportAsync(reader, writer, progress);

		// Assert — 5 progress reports: init → table1 → table2 → finalize → complete.
		List<DataPortProgressReport> reports = progress.Reports;
		Assert.Equal(5, reports.Count);

		Assert.Equal("Initializing import...", reports[0].OverallMessage);
		Assert.Equal(0, reports[0].OverallTotalSteps);
		Assert.Equal(0, reports[0].OverallCurrentStep);

		Assert.Equal("Importing table Alpha (1/2)...", reports[1].OverallMessage);
		Assert.Equal(4, reports[1].OverallTotalSteps);
		Assert.Equal(1, reports[1].OverallCurrentStep);

		Assert.Equal("Importing table Beta (2/2)...", reports[2].OverallMessage);
		Assert.Equal(4, reports[2].OverallTotalSteps);
		Assert.Equal(2, reports[2].OverallCurrentStep);

		Assert.Equal("Finalizing import...", reports[3].OverallMessage);
		Assert.Equal(4, reports[3].OverallTotalSteps);
		Assert.Equal(3, reports[3].OverallCurrentStep);

		Assert.Equal("Import completed", reports[4].OverallMessage);
		Assert.Equal(4, reports[4].OverallTotalSteps);
		Assert.Equal(4, reports[4].OverallCurrentStep);
	}

	// --- 2. Parameter validation ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunImportAsync"/> throws <see cref="ArgumentNullException"/>
	/// when <c>shuttleReader</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task RunImportAsync_WhenShuttleReaderIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RunImportAsync(
			         shuttleReader: null!,
			         targetImporter: new StubImportWriter()));
		Assert.Equal("shuttleReader", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunImportAsync"/> throws <see cref="ArgumentNullException"/>
	/// when <c>targetImporter</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task RunImportAsync_WhenTargetImporterIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RunImportAsync(
			         shuttleReader: new StubShuttleReader(),
			         targetImporter: null!));
		Assert.Equal("targetImporter", ex.ParamName);
	}

	// --- 3. Empty migration history ---

	/// <summary>
	/// Provides test data for
	/// <see cref="RunImportAsync_WhenMigrationHistoryIsEmpty_ThrowsInvalidOperationException"/>.
	/// Each row represents a different combination of empty shuttle and/or target histories.
	/// </summary>
	public static TheoryData<string, List<MigrationInfo>, List<MigrationInfo>, string>
		EmptyMigrationHistoryData => new()
	{
		// Both histories empty — the most common case (uninitialized databases).
		{
			"Both empty",
			[],
			[],
			"Invalid migration history. Shuttle has 0 migrations, " +
			"target has 0. Both must have at least one migration entry."
		},
		// Only shuttle empty — e.g., shuttle exported from an uninitialized database.
		{
			"Only shuttle empty",
			[],
			[new MigrationInfo("20260101_Initial", "10.0.0")],
			"Invalid migration history. Shuttle has 0 migrations, " +
			"target has 1. Both must have at least one migration entry."
		},
		// Only target empty — e.g., importing into an uninitialized database.
		{
			"Only target empty",
			[new MigrationInfo("20260101_Initial", "10.0.0")],
			[],
			"Invalid migration history. Shuttle has 1 migrations, " +
			"target has 0. Both must have at least one migration entry."
		}
	};

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunImportAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the shuttle or target (or both) report empty
	/// migration histories, indicating uninitialized or invalid databases.
	/// </summary>
	/// <param name="scenario">A description of the test scenario (for test output readability).</param>
	/// <param name="shuttleHistory">The migration history returned by the shuttle reader.</param>
	/// <param name="targetHistory">The migration history returned by the target importer.</param>
	/// <param name="expectedMessage">The expected exception message including the correct counts.</param>
	[Theory]
	[MemberData(nameof(EmptyMigrationHistoryData))]
	public async Task RunImportAsync_WhenMigrationHistoryIsEmpty_ThrowsInvalidOperationException(
		string              scenario,
		List<MigrationInfo> shuttleHistory,
		List<MigrationInfo> targetHistory,
		string              expectedMessage)
	{
		_ = scenario; // Used by xUnit test runner for display purposes.

		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);
		var reader = new StubShuttleReader
		{
			OnGetMigrationHistory = _ => Task.FromResult(shuttleHistory)
		};
		var writer = new StubImportWriter
		{
			OnGetMigrationHistory = _ => Task.FromResult(targetHistory)
		};

		// Act + Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RunImportAsync(reader, writer));
		Assert.Equal(expectedMessage, ex.Message);
	}

	// --- 4. Schema mismatch ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunImportAsync"/> throws
	/// <see cref="DataPortSchemaMismatchException"/> when the shuttle and target have non-empty but
	/// different migration histories, indicating incompatible database schemas.
	/// </summary>
	[Fact]
	public async Task RunImportAsync_WhenSchemaMismatch_ThrowsDataPortSchemaMismatchException()
	{
		// Arrange — shuttle and target return different migration IDs to trigger the mismatch guard.
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);
		List<MigrationInfo> shuttleHistory = [new("20260101_Initial", "10.0.0")];
		List<MigrationInfo> targetHistory = [new("20260102_Different", "10.0.0")];

		var reader = new StubShuttleReader
		{
			OnGetMigrationHistory = _ => Task.FromResult(shuttleHistory)
		};
		var writer = new StubImportWriter
		{
			OnGetMigrationHistory = _ => Task.FromResult(targetHistory)
		};

		// Act + Assert
		var ex = await Assert.ThrowsAsync<DataPortSchemaMismatchException>(() => sut.RunImportAsync(reader, writer));
		Assert.Equal(0, ex.FirstMismatchIndex);
		Assert.Equal(shuttleHistory, ex.ShuttleMigrationHistory);
		Assert.Equal(targetHistory, ex.TargetMigrationHistory);
	}

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunImportAsync"/> throws
	/// <see cref="DataPortSchemaMismatchException"/> when the shuttle has more migrations than the
	/// target. The common prefix matches, so <see cref="DataPortSchemaMismatchException.FirstMismatchIndex"/>
	/// is <see langword="null"/> (different-length branch).
	/// </summary>
	[Fact]
	public async Task
		RunImportAsync_WhenSchemaMismatchDueToDifferentLength_ThrowsDataPortSchemaMismatchException()
	{
		// Arrange — shuttle has an extra migration beyond the shared prefix.
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);
		List<MigrationInfo> shuttleHistory =
		[
			new("20260101_Initial", "10.0.0"),
			new("20260201_AddIndex", "10.0.0")
		];
		List<MigrationInfo> targetHistory = [new("20260101_Initial", "10.0.0")];

		var reader = new StubShuttleReader
		{
			OnGetMigrationHistory = _ => Task.FromResult(shuttleHistory)
		};
		var writer = new StubImportWriter
		{
			OnGetMigrationHistory = _ => Task.FromResult(targetHistory)
		};

		// Act + Assert
		var ex = await Assert.ThrowsAsync<DataPortSchemaMismatchException>(() => sut.RunImportAsync(reader, writer));
		Assert.Null(ex.FirstMismatchIndex);
		Assert.Equal(shuttleHistory, ex.ShuttleMigrationHistory);
		Assert.Equal(targetHistory, ex.TargetMigrationHistory);
	}

	// --- 5. Missing ShuttleId ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunImportAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the shuttle metadata does not contain the
	/// required <c>ShuttleId</c> entry needed for checkpoint-based import resume.
	/// </summary>
	[Fact]
	public async Task RunImportAsync_WhenShuttleIdMissing_ThrowsInvalidOperationException()
	{
		// Arrange — both histories match (passes schema check), but metadata lacks the ShuttleId key.
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);

		var reader = new StubShuttleReader
		{
			OnGetMigrationHistory = _ => Task.FromResult<List<MigrationInfo>>(
				[new MigrationInfo("20260101_Initial", "10.0.0")]),
			OnGetMetadata = _ => Task.FromResult(new Dictionary<string, string> { { "SourceProvider", "Test" } })
		};
		var writer = new StubImportWriter
		{
			OnGetMigrationHistory = _ => Task.FromResult<List<MigrationInfo>>(
				[new MigrationInfo("20260101_Initial", "10.0.0")])
		};

		// Act + Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RunImportAsync(reader, writer));
		Assert.Equal(
			"Shuttle file does not contain the required 'ShuttleId' metadata entry. " +
			"The file may have been created by an older version or is corrupted.",
			ex.Message);
	}

	// --- 6. Cancellation ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunImportAsync"/> catches and re-throws
	/// <see cref="OperationCanceledException"/> when the operation is cancelled during the pipeline.
	/// </summary>
	[Fact]
	public async Task RunImportAsync_WhenCancelled_ThrowsOperationCanceledException()
	{
		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// GetMigrationHistoryAsync is the first configurable call after initialization —
		// injecting a token check here triggers the OCE catch block.
		var reader = new StubShuttleReader
		{
			OnGetMigrationHistory = ct =>
			{
				ct.ThrowIfCancellationRequested();
				return Task.FromResult(new List<MigrationInfo>());
			}
		};

		// Act + Assert
		var ex = await Assert.ThrowsAsync<OperationCanceledException>(() => sut.RunImportAsync(
			         reader,
			         new StubImportWriter(),
			         cancellationToken: cts.Token));
		Assert.Equal(cts.Token, ex.CancellationToken);
	}

	// --- 7. General failure ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunImportAsync"/> logs and re-throws non-cancellation
	/// exceptions that occur during the import pipeline.
	/// </summary>
	[Fact]
	public async Task RunImportAsync_WhenImporterFails_ThrowsOriginalException()
	{
		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);
		var expected = new IOException("Simulated I/O failure");

		// GetMigrationHistoryAsync throws a non-OCE exception — exercises the general catch block.
		var reader = new StubShuttleReader
		{
			OnGetMigrationHistory = _ => throw expected
		};

		// Act + Assert
		var ex = await Assert.ThrowsAsync<IOException>(() => sut.RunImportAsync(reader, new StubImportWriter()));
		Assert.Same(expected, ex);
	}
}
