// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Models;

using Microsoft.Extensions.Logging;

namespace LumaCore.Data.DataPort.Import;

/// <summary>
/// Defines the contract for importing data into an existing,
/// EF Core-managed target database.
/// </summary>
/// <remarks>
///     <para>
///     Implementations are optimized for a specific database engine (e.g., Postgres,
///     SQL Server) and must handle high-speed import methods (like bulk copy),
///     the correct handling of auto-increment/identity keys, and the temporary
///     disabling of foreign key constraints.
///     </para>
///     <para>
///     <b>Transaction behavior:</b> Data is imported in chunks of <see cref="DataPortTuning.ImportChunkSizeRows"/>
///     rows. Each chunk is committed in its own transaction alongside a checkpoint update, enabling crash-safe
///     resume. This is always active and not configurable.
///     </para>
/// </remarks>
public interface IDataImportWriter : IAsyncDisposable
{
	/// <summary>
	/// Initializes the importer and opens a connection to the target database.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The importer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The importer has already been initialized.</exception>
	Task InitializeAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves the EF Core migration history from the target database.
	/// This is required by the import service to perform the crucial
	/// schema compatibility check before any data is imported.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A list of migration entries ordered by migration ID.</returns>
	/// <exception cref="ObjectDisposedException">The importer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The importer is not initialized.</exception>
	Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Prepares the target database for a high-speed bulk import.
	/// </summary>
	/// <param name="shuttleId">
	/// The unique identity of the shuttle file being imported. Used to match/validate checkpoint records.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The importer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The importer is not initialized.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="shuttleId"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="shuttleId"/> is empty or consists only of white-space characters.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method disables foreign key checks and triggers to ensure data can be imported in any order
	///     and at maximum speed. It also creates the checkpoint table (if it does not exist) and validates
	///     existing checkpoint data against the provided <paramref name="shuttleId"/>.
	///     </para>
	///     <para>
	///     <b>Checkpoint validation:</b> If a checkpoint exists for a different shuttle ID, the checkpoint
	///     is discarded and the import starts from scratch. If the shuttle ID matches, the import resumes
	///     from the last committed chunk.
	///     </para>
	/// </remarks>
	Task PrepareForImportAsync(string shuttleId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Imports a complete table snapshot into the corresponding target table.
	/// </summary>
	/// <param name="table">The table snapshot (schema and data stream) to import.</param>
	/// <param name="logger">A logger for progress reporting, or <see langword="null"/> to disable logging.</param>
	/// <param name="progress">Progress reporter for import progress updates.</param>
	/// <param name="currentTable">The current table index being processed (1-based).</param>
	/// <param name="totalTables">The total number of tables to be processed.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The importer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The importer is not initialized.</exception>
	/// <remarks>
	///     <para>
	///     This method MUST internally handle identity/auto-increment columns.
	///     For example, on SQL Server, it must execute <c>SET IDENTITY_INSERT [Table] ON</c>
	///     before importing and <c>OFF</c> after. For Postgres, it must use a <c>COPY</c>
	///     command that correctly specifies the column list.
	///     </para>
	///     <para>
	///     Data is imported in chunks of <see cref="DataPortTuning.ImportChunkSizeRows"/> rows. Each chunk
	///     is committed in its own transaction alongside a checkpoint update. If a checkpoint exists for
	///     this table (from a previous interrupted import), already-imported rows are skipped automatically.
	///     </para>
	/// </remarks>
	Task ImportTableAsync(
		TableSnapshot                      table,
		ILogger?                           logger,
		IProgress<DataPortProgressReport>? progress,
		int                                currentTable,
		int                                totalTables,
		CancellationToken                  cancellationToken = default);

	/// <summary>
	/// Finalizes the import process.
	/// This method must re-enable foreign key constraints and triggers.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The importer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The importer is not initialized.</exception>
	/// <remarks>
	///     <para>
	///     CRITICAL: This method is also responsible for resetting the
	///     database's auto-increment counters (sequences) to the highest
	///     imported ID for each table. This prevents future primary key
	///     conflicts when the application inserts new rows.
	///     </para>
	///     <para>
	///     This method also drops the <c>_shuttle_import_checkpoint</c> table
	///     after a successful import.
	///     </para>
	/// </remarks>
	Task CleanupAfterImportAsync(CancellationToken cancellationToken = default);
}
