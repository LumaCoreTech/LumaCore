// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Models;

namespace LumaCore.Data.DataPort.Export;

/// <summary>
/// Defines the contract for reading database content from a live source for data porting (export) purposes.
/// </summary>
/// <remarks>
/// Implementations of this interface provide database-specific logic to extract table schemas and data in a
/// consistent, streaming manner. Each implementation must ensure transaction isolation to guarantee a consistent
/// snapshot.
/// </remarks>
public interface IDataExportReader : IAsyncDisposable
{
	/// <summary>
	/// Initializes the reader and establishes a transaction for consistent reads.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The reader has already been initialized.</exception>
	/// <remarks>
	/// This method should open a database connection and begin a transaction with an appropriate isolation level
	/// (e.g., Repeatable Read or Snapshot Isolation) to ensure all subsequent reads see a consistent database state.
	/// </remarks>
	Task InitializeAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves the names of all user tables in the database.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A list of table names, excluding system tables and migration history.</returns>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">Reader is not initialized.</exception>
	Task<List<string>> GetTableNamesAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads a complete table snapshot including schema, data stream, and estimated row count.
	/// </summary>
	/// <param name="tableName">The name of the table to read.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A <see cref="TableSnapshot"/> containing the table's schema, data, and row estimate.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="tableName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="tableName"/> is empty or consists only of white-space characters.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">Reader is not initialized.</exception>
	Task<TableSnapshot> ReadTableAsync(string tableName, CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves the Entity Framework Core migration history.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A list of migration history entries.</returns>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="InvalidOperationException">Reader is not initialized.</exception>
	Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default);
}
