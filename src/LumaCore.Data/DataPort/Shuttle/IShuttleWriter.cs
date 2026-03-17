// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Models;

using Microsoft.Extensions.Logging;

namespace LumaCore.Data.DataPort.Shuttle;

/// <summary>
/// Defines the contract for writing a logical data export to a target LumaCore Shuttle file.
/// </summary>
/// <remarks>
///     <para>
///     A shuttle writer is responsible for producing a self-contained, read-only export that can later
///     be consumed by an <see cref="IShuttleReader"/>. Implementations are typically single-use:
///     one instance is created for a single export run and then disposed.
///     </para>
///     <para>
///     The LumaCore Shuttle format is intentionally data-centric. It transports row data and simple metadata
///     but does not encode relational constraints such as primary keys, foreign keys or indexes; those
///     are defined by the EF Core model and its migrations.
///     </para>
///     <para>
///     The typical call sequence is:
///     <list type="number">
///         <item>
///             <description>
///             <see cref="InitializeAsync"/> – prepare the target, open connections, allocate resources.
///             </description>
///         </item>
///         <item>
///             <description>
///             One or more calls to <see cref="WriteTableAsync"/> – schema and data for each logical
///             table that should be part of the export.
///             </description>
///         </item>
///         <item>
///             <description>
///             Optional calls to <see cref="WriteMigrationHistoryAsync"/> and
///             <see cref="WriteMetadataAsync"/> – provider-specific migration information and
///             descriptive metadata.
///             </description>
///         </item>
///         <item>
///             <description>
///             A single call to <see cref="FinalizeAsync"/> – establish the durability and completeness
///             boundary of the export.
///             </description>
///         </item>
///         <item>
///             <description>
///             Finally, <see cref="IAsyncDisposable.DisposeAsync"/> is called to release resources.
///             </description>
///         </item>
///     </list>
///     </para>
///     <para>
///     Implementations must <b>not</b> implicitly finalize the export from
///     <see cref="IAsyncDisposable.DisposeAsync"/>. If <see cref="FinalizeAsync"/> is never called or
///     throws, the resulting target must remain in a clearly non-complete state that a corresponding
///     <see cref="IShuttleReader"/> can detect and reject.
///     </para>
///     <para>
///     Implementations are not required to be thread-safe. Callers must not invoke methods
///     concurrently on the same instance.
///     </para>
/// </remarks>
public interface IShuttleWriter : IAsyncDisposable
{
	/// <summary>
	/// Initializes the shuttle writer and prepares the target storage.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The writer has already been initialized or finalized.</exception>
	/// <remarks>
	///     <para>
	///     This method should create or open the underlying target (for example a file, database, or
	///     blob), initialize any format-specific structures, and apply performance optimizations that
	///     are appropriate for write-once export scenarios.
	///     </para>
	///     <para>
	///     It must be called exactly once before any other method on the writer is used. Subsequent
	///     calls should result in an <see cref="InvalidOperationException"/>.
	///     </para>
	///     <para>
	///     If this method throws, the writer instance must be treated as not initialized. Callers
	///     should either dispose the instance or retry initialization according to the concrete
	///     implementation's guidance. Implementations are expected to avoid leaving the writer in a
	///     partially initialized state that appears usable.
	///     </para>
	/// </remarks>
	Task InitializeAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Writes a table snapshot to the shuttle file.
	/// </summary>
	/// <param name="table">The table snapshot containing schema and data.</param>
	/// <param name="logger">The logger for progress reporting (<see langword="null"/> to disable logging progress).</param>
	/// <param name="progress">Progress reporter for data export progress.</param>
	/// <param name="currentTable">The current table index being processed (1-based).</param>
	/// <param name="totalTables">The total number of tables to be processed.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ArgumentNullException"><paramref name="table"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	///     <para>Writer is not initialized. Call <see cref="InitializeAsync"/> first.</para>
	///     <para>- or -</para>
	///     <para>The writer has already been finalized.</para>
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method should create the table structure in the underlying LumaCore Shuttle format and stream
	///     the row data efficiently without loading the entire table into memory at once.
	///     </para>
	///     <para>
	///     Implementations are expected to honor the <paramref name="progress"/> reporter, if provided.
	///     It is recommended to report progress periodically during the row export (e.g., once
	///     per data batch) to provide a responsive user experience for large tables.
	///     </para>
	///     <para>
	///     The <see cref="DataPortProgressReport.DetailedTotalSteps"/> property of the report
	///     <b>should</b> be populated from the <see cref="TableSnapshot.EstimatedRowCount"/>. If
	///     <see cref="TableSnapshot.EstimatedRowCount"/> is -1 (unknown),
	///     <see cref="DataPortProgressReport.DetailedTotalSteps"/> <b>must</b> be set to <see langword="null"/>.
	///     This allows the UI to reliably switch between a determinate and indeterminate progress bar.
	///     </para>
	///     <para>
	///     The <paramref name="currentTable"/> and <paramref name="totalTables"/> parameters allow the
	///     implementation and caller to provide user-friendly progress information.
	///     </para>
	/// </remarks>
	Task WriteTableAsync(
		TableSnapshot                      table,
		ILogger?                           logger,
		IProgress<DataPortProgressReport>? progress,
		int                                currentTable,
		int                                totalTables,
		CancellationToken                  cancellationToken = default);

	/// <summary>
	/// Writes Entity Framework Core migration history to the shuttle file.
	/// </summary>
	/// <param name="migrations">The list of migration entries.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ArgumentNullException"><paramref name="migrations"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	///     <para>Writer is not initialized. Call <see cref="InitializeAsync"/> first.</para>
	///     <para>- or -</para>
	///     <para>The writer has already been finalized.</para>
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method is optional and is only relevant for exports that originate from an EF Core
	///     backed data store. If the underlying provider does not use EF migrations, callers may simply
	///     pass an empty list or skip this call entirely.
	///     </para>
	///     <para>
	///     Implementations should persist this information in a way that can be queried by
	///     <see cref="IShuttleReader.GetMigrationHistoryAsync"/> during import.
	///     </para>
	/// </remarks>
	Task WriteMigrationHistoryAsync(
		List<MigrationInfo> migrations,
		CancellationToken   cancellationToken = default);

	/// <summary>
	/// Writes export metadata information.
	/// </summary>
	/// <param name="metadata">A dictionary of metadata key/value pairs.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ArgumentNullException"><paramref name="metadata"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="metadata"/> contains reserved keys.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	///     <para>Writer is not initialized. Call <see cref="InitializeAsync"/> first.</para>
	///     <para>- or -</para>
	///     <para>The writer has already been finalized.</para>
	/// </exception>
	/// <remarks>
	///     <para>
	///     Typical metadata entries include the source provider, source database identifier, creation
	///     timestamp, product version, and any additional information that helps consumers understand
	///     the origin and shape of the export.
	///     </para>
	///     <para>
	///     The metadata model is intentionally simple (string keys and values) so that different
	///     implementations and transport formats can share a common contract.
	///     </para>
	/// </remarks>
	Task WriteMetadataAsync(
		Dictionary<string, string> metadata,
		CancellationToken          cancellationToken = default);

	/// <summary>
	/// Finalizes the export by establishing the durability and completeness boundary.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	///     <para>Writer is not initialized. Call <see cref="InitializeAsync"/> first.</para>
	///     <para>- or -</para>
	///     <para><see cref="FinalizeAsync"/> has already been called.</para>
	/// </exception>
	/// <exception cref="InvalidDataException">
	/// The integrity check failed after flushing (the shuttle file may be corrupted).
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method should ensure that all buffered data has been flushed to durable storage, run
	///     any format-specific integrity checks, and record a completion marker that a corresponding
	///     <see cref="IShuttleReader"/> can validate.
	///     </para>
	///     <para>
	///     It is expected to be called exactly once per export run and must <b>not</b> close or dispose
	///     the underlying resources; cleanup is the responsibility of
	///     <see cref="IAsyncDisposable.DisposeAsync"/>. If this method throws at any point, the resulting
	///     shuttle must be treated as invalid and should not be used for import.
	///     </para>
	/// </remarks>
	Task FinalizeAsync(CancellationToken cancellationToken = default);
}
