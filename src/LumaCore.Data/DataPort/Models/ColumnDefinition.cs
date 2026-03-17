// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

namespace LumaCore.Data.DataPort.Models;

/// <summary>
/// Represents the definition of a database column for data porting (export/import) operations.
/// </summary>
public sealed class ColumnDefinition
{
	/// <summary>
	/// Gets the name of the column.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the database-specific type of the column as reported by the source provider.
	/// </summary>
	/// <remarks>
	/// Examples: <c>varchar</c>, <c>integer</c>, <c>timestamp with time zone</c>, <c>uuid</c>
	/// </remarks>
	public required string DbType { get; init; }

	/// <summary>
	/// Gets the SQLite storage type to use when writing this column into a LumaCore Shuttle file.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This value is set by the export reader using the provider-specific mapping supplied via
	///     <see cref="IDatabaseProviderOperations.MapToShuttleStorageType"/>. When
	///     <see langword="null"/> (e.g., during shuttle-to-database import), the shuttle writer
	///     falls back to <see cref="DbType"/>.
	///     </para>
	///     <para>
	///     Valid SQLite storage types are <c>TEXT</c>, <c>INTEGER</c>, <c>REAL</c>, <c>NUMERIC</c>,
	///     and <c>BLOB</c>.
	///     </para>
	/// </remarks>
	public string? ShuttleStorageType { get; init; }
}
