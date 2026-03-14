// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.DataPort;

/// <summary>
/// Central tuning parameters for DataPort export/import throughput and progress reporting.
/// </summary>
/// <remarks>
/// These values are intentionally centralized to avoid scattered magic numbers.
/// They are not currently user-configurable; if runtime configurability is needed later,
/// introduce options (e.g., via <c>IOptions&lt;DataPortOptions&gt;</c>) and migrate callers.
/// </remarks>
static class DataPortTuning
{
	/// <summary>
	/// Number of rows to write per transaction when exporting into a shuttle file.
	/// </summary>
	public const int ShuttleCommitBatchSizeRows = 1000;

	/// <summary>
	/// Number of rows between progress reports while exporting a table.
	/// </summary>
	public const int ExportProgressReportIntervalRows = 100;

	/// <summary>
	/// Number of rows between progress reports while importing a table.
	/// </summary>
	public const int ImportProgressReportIntervalRows = 5000;

	/// <summary>
	/// Number of rows per batch / notification interval for SQL Server bulk copy operations.
	/// </summary>
	public const int SqlServerBulkCopyBatchSizeRows = 5000;

	/// <summary>
	/// Number of rows to include per multi-row INSERT statement when importing into MySQL.
	/// </summary>
	public const int MySqlInsertBatchSizeRows = 500;

	/// <summary>
	/// Number of rows per chunk during data import. Each chunk is committed in its own
	/// transaction alongside a checkpoint update for crash-safe resume.
	/// </summary>
	public const int ImportChunkSizeRows = 5000;
}
