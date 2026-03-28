// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Models;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

// RunExportAsync() coverage: progress reporting, parameter validation, and error behavior.
//
//   1. Progress reporting — non-null IProgress<T> handler receives all stages
//      (WithProgressAndTwoTables).
//
//   2. Parameter validation — null guards for sourceReader and shuttleWriter
//      (WhenSourceReaderIsNull, WhenShuttleWriterIsNull).
//
//   3. Cancellation — OperationCanceledException catch block
//      (WhenCancelled).
//
//   4. General failure — non-cancellation exception catch block
//      (WhenReaderFails).
//
// For constructor and SourceProviderKey, see the anchor file.
// For RunImportAsync() coverage, see RunImportAsync.
// For shared stubs and helpers, see Helpers.
public sealed partial class DataPortServiceTests
{
	// --- 1. Progress reporting ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunExportAsync"/> sends all expected progress reports
	/// (initialization, per-table, finalization, completion) when a non-<see langword="null"/>
	/// <see cref="IProgress{T}"/> handler is provided and two tables are exported. Using two tables
	/// validates that the step arithmetic (<c>totalSteps = tableCount + 2</c>) and per-table numbering
	/// (<c>(1/N)</c>, <c>(2/N)</c>) scale correctly beyond the trivial single-table case.
	/// </summary>
	[Fact]
	public async Task RunExportAsync_WithProgressAndTwoTables_ReportsAllStages()
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
		var reader = new StubExportReader
		{
			OnGetTableNames = _ => Task.FromResult(new List<string> { "Alpha", "Beta" }),
			OnReadTable = (name, _) => Task.FromResult(snapshots[name])
		};

		// Act
		await sut.RunExportAsync(reader, new StubShuttleWriter(), progress);

		// Assert — 5 progress reports: init → table1 → table2 → finalize → complete.
		List<DataPortProgressReport> reports = progress.Reports;
		Assert.Equal(5, reports.Count);

		Assert.Equal("Initializing export...", reports[0].OverallMessage);
		Assert.Equal(0, reports[0].OverallTotalSteps);
		Assert.Equal(0, reports[0].OverallCurrentStep);

		Assert.Equal("Exporting table Alpha (1/2)...", reports[1].OverallMessage);
		Assert.Equal(4, reports[1].OverallTotalSteps);
		Assert.Equal(1, reports[1].OverallCurrentStep);

		Assert.Equal("Exporting table Beta (2/2)...", reports[2].OverallMessage);
		Assert.Equal(4, reports[2].OverallTotalSteps);
		Assert.Equal(2, reports[2].OverallCurrentStep);

		Assert.Equal("Finalizing export...", reports[3].OverallMessage);
		Assert.Equal(4, reports[3].OverallTotalSteps);
		Assert.Equal(3, reports[3].OverallCurrentStep);

		Assert.Equal("Export completed", reports[4].OverallMessage);
		Assert.Equal(4, reports[4].OverallTotalSteps);
		Assert.Equal(4, reports[4].OverallCurrentStep);
	}

	// --- 2. Parameter validation ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunExportAsync"/> throws <see cref="ArgumentNullException"/>
	/// when <c>sourceReader</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task RunExportAsync_WhenSourceReaderIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RunExportAsync(
			         sourceReader: null!,
			         shuttleWriter: new StubShuttleWriter()));
		Assert.Equal("sourceReader", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunExportAsync"/> throws <see cref="ArgumentNullException"/>
	/// when <c>shuttleWriter</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task RunExportAsync_WhenShuttleWriterIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RunExportAsync(
			         sourceReader: new StubExportReader(),
			         shuttleWriter: null!));
		Assert.Equal("shuttleWriter", ex.ParamName);
	}

	// --- 3. Cancellation ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunExportAsync"/> catches and re-throws
	/// <see cref="OperationCanceledException"/> when the operation is cancelled during the pipeline.
	/// </summary>
	[Fact]
	public async Task RunExportAsync_WhenCancelled_ThrowsOperationCanceledException()
	{
		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// InitializeAsync is the first awaited call — injecting a token check here triggers the OCE catch.
		var reader = new StubExportReader
		{
			OnInitialize = ct =>
			{
				ct.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			}
		};

		// Act + Assert
		var ex = await Assert.ThrowsAsync<OperationCanceledException>(() => sut.RunExportAsync(
			         reader,
			         new StubShuttleWriter(),
			         cancellationToken: cts.Token));
		Assert.Equal(cts.Token, ex.CancellationToken);
	}

	// --- 4. General failure ---

	/// <summary>
	/// Verifies that <see cref="DataPortService.RunExportAsync"/> logs and re-throws non-cancellation
	/// exceptions that occur during the export pipeline.
	/// </summary>
	[Fact]
	public async Task RunExportAsync_WhenReaderFails_ThrowsOriginalException()
	{
		// Arrange
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);
		var expected = new IOException("Simulated disk failure");

		// InitializeAsync throws a non-OCE exception — exercises the general catch block.
		var reader = new StubExportReader
		{
			OnInitialize = _ => throw expected
		};

		// Act + Assert
		var ex = await Assert.ThrowsAsync<IOException>(() => sut.RunExportAsync(reader, new StubShuttleWriter()));
		Assert.Same(expected, ex);
	}
}
