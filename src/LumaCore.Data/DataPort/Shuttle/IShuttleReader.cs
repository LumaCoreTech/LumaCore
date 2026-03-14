// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Models;

namespace LumaCore.Data.DataPort.Shuttle;

/// <summary>
/// Defines the contract for reading the contents of a LumaCore Shuttle file produced by an <see cref="IShuttleWriter"/>.
/// </summary>
/// <remarks>
///     <para>
///     This interface mirrors the <see cref="IDataExportReader"/>, allowing the import process to treat
///     the shuttle file as a readable data source that exposes logical tables, migration history, and
///     metadata.
///     </para>
///     <para>
///     The LumaCore Shuttle format is intentionally data-centric. It exposes logical tables, rows and basic
///     column metadata, but it does not encode relational constraints such as primary keys, foreign
///     keys or indexes; those are provided by the EF Core model and its migrations.
///     </para>
///     <para>
///     Reader implementations are expected to validate that the underlying shuttle represents a
///     completed and consistent export (for example by checking a format-specific completion marker).
///     Incomplete or corrupted exports should result in a well-defined exception from
///     <see cref="InitializeAsync"/> or from the first read operation.
///     </para>
///     <para>
///     Instances of <see cref="IShuttleReader"/> are not required to be thread-safe. Callers must
///     not invoke methods concurrently on the same instance.
///     </para>
/// </remarks>
public interface IShuttleReader : IAsyncDisposable
{
	/// <summary>
	/// Initializes the reader and opens the shuttle file.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The reader has already been initialized.</exception>
	/// <remarks>
	///     <para>
	///     This method should open the underlying shuttle resource, perform any format-specific
	///     validation and integrity checks, and prepare the reader for subsequent table and metadata
	///     queries.
	///     </para>
	///     <para>
	///     It must be called exactly once before any other method on the reader is used. Subsequent
	///     calls should result in an <see cref="InvalidOperationException"/>.
	///     </para>
	/// </remarks>
	Task InitializeAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Validates the structural integrity of the shuttle file.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	///     <para>Reader is not initialized. Call <see cref="InitializeAsync"/> first.</para>
	///     <para>- or -</para>
	///     <para>The integrity check detected corruption in the shuttle file.</para>
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method performs a deep validation of the underlying storage format to detect corruption
	///     that would not be caught by the format-specific completion markers checked during
	///     <see cref="InitializeAsync"/>. For example, a SQLite-based implementation would run
	///     <c>PRAGMA integrity_check</c> to verify B-tree structure and page consistency.
	///     </para>
	///     <para>
	///     This check can be expensive on large files because it reads every data page. Callers should
	///     invoke it only when the result will gate a destructive operation (e.g., before dropping the
	///     target database schema during a restore).
	///     </para>
	/// </remarks>
	Task ValidateIntegrityAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves the logical table names available in the shuttle.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">Reader is not initialized. Call <see cref="InitializeAsync"/> first.</exception>
	/// <returns>
	/// A list of table names that can be read via <see cref="ReadTableAsync"/>, or an empty list if no tables are present.
	/// </returns>
	/// <remarks>
	/// The returned names represent logical tables as understood by the export process. Implementations
	/// may map these to physical tables, views, or other structures in the underlying LumaCore Shuttle format.
	/// </remarks>
	Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads the schema and data of a single table from the shuttle.
	/// </summary>
	/// <param name="tableName">The logical name of the table to read.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ArgumentNullException"><paramref name="tableName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="tableName"/> is empty or consists only of white-space characters.</exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">
	///     <para>Reader is not initialized. Call <see cref="InitializeAsync"/> first.</para>
	///     <para>- or -</para>
	///     <para>The specified table does not exist in the shuttle file.</para>
	/// </exception>
	/// <returns>
	/// A <see cref="TableSnapshot"/> that exposes the table schema, an estimated row count, and a lazily
	/// streamed sequence of rows.
	/// </returns>
	/// <remarks>
	///     <para>
	///     The returned <see cref="TableSnapshot"/> streams rows lazily via
	///     <see cref="TableSnapshot.Rows"/>. Callers must enumerate the sequence to read the data;
	///     see the property documentation for memory and lifetime semantics.
	///     </para>
	/// </remarks>
	Task<TableSnapshot> ReadTableAsync(string tableName, CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves the EF Core migration history stored in the shuttle file.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">Reader is not initialized. Call <see cref="InitializeAsync"/> first.</exception>
	/// <returns>
	/// A list of <see cref="MigrationInfo"/> entries, or an empty list if no
	/// migration history is present in the shuttle.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method is primarily intended for exports that originate from an EF Core backed data
	///     store. Implementations that do not persist migration information should return an empty
	///     list rather than throwing.
	///     </para>
	/// </remarks>
	Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves the metadata stored in the shuttle file.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">Reader is not initialized. Call <see cref="InitializeAsync"/> first.</exception>
	/// <returns>
	/// A dictionary of metadata key/value pairs, or an empty dictionary if no metadata is present in the shuttle.
	/// </returns>
	/// <remarks>
	///     <para>
	///     The returned dictionary may also contain shuttle-internal metadata entries such as completion
	///     status or LumaCore Shuttle format version. Callers that are only interested in user-supplied metadata
	///     may choose to ignore any implementation-specific reserved metadata keys.
	///     </para>
	/// </remarks>
	Task<Dictionary<string, string>> GetMetadataAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves the export creation timestamp from the shuttle metadata.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">Reader is not initialized. Call <see cref="InitializeAsync"/> first.</exception>
	/// <returns>
	/// The parsed <see cref="DateTimeOffset"/> from the <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata
	/// entry, or <see langword="null"/> if the key is missing or the value cannot be parsed.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method is useful for determining shuttle file age without relying on file system metadata
	///     (e.g., <c>FileInfo.CreationTimeUtc</c>), which can be altered by copy or move operations.
	///     </para>
	///     <para>
	///     A <see langword="null"/> return indicates that the metadata key is absent or contains a value that
	///     cannot be parsed as a <see cref="DateTimeOffset"/>. Infrastructure errors (disposed reader, uninitialized
	///     state, I/O failures) are propagated as exceptions.
	///     </para>
	/// </remarks>
	Task<DateTimeOffset?> GetCreatedUtcAsync(CancellationToken cancellationToken = default);
}
