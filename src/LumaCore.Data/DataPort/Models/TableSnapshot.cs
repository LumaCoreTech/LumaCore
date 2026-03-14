// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.DataPort.Models;

/// <summary>
/// Represents a snapshot of a database table with its schema and data stream.
/// </summary>
/// <remarks>
/// This class provides a database-agnostic representation of a table that can be transferred between different
/// database providers during data porting (export/import) operations. The data stream uses
/// <see cref="IAsyncEnumerable{T}"/> to support efficient memory usage for large tables.
/// </remarks>
public sealed class TableSnapshot
{
	/// <summary>
	/// Gets the name of the table.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the column definitions for this table.
	/// </summary>
	public required List<ColumnDefinition> Columns { get; init; }

	/// <summary>
	/// Gets the estimated number of rows in the table, or <c>-1</c> if the count is unknown.
	/// </summary>
	/// <remarks>
	/// This value is intended for progress reporting and UI feedback. It may be an exact count or a statistical
	/// estimate depending on the source provider. Callers must not rely on it for correctness — the actual number
	/// of rows yielded by <see cref="Rows"/> may differ.
	/// </remarks>
	public long EstimatedRowCount { get; init; } = -1;

	/// <summary>
	/// Gets the asynchronous stream of row data.
	/// </summary>
	/// <remarks>
	/// Each array represents a single row, where elements correspond to <see cref="Columns"/>
	/// in the same order. <c>Null</c> values are represented as <see langword="null"/>
	/// array elements.
	/// </remarks>
	public required IAsyncEnumerable<object?[]> Rows { get; init; }
}
