// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Singleton table row that tracks resource garbage collection state for throttling.
/// </summary>
/// <remarks>
///     <para>
///     This entity has exactly one row (with <see cref="Id"/> = <c>1</c>) that stores the timestamp of
///     the last successful GC run. The resource cleanup service reads this timestamp to decide whether
///     a new GC cycle should execute, preventing excessive runs in horizontally scaled deployments
///     where multiple instances share the same database.
///     </para>
///     <para>
///     The row is upserted (inserted on first use, then updated) — no seed data is required.
///     </para>
///     <para>
///     Database constraints and table mapping are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public sealed class ResourceGcStateEntity
{
	// --- 1. Primary key ---

	/// <summary>
	/// Gets or sets the singleton row identifier. Always <c>1</c>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Index:</b> Primary key. Not auto-incremented — the application always sets this to <c>1</c>.
	///     </para>
	///     <para>
	///     Typed as <see cref="int"/> (not <see cref="long"/> like the other entities' identity columns)
	///     because this column is a fixed slot identifier for a singleton row, not an auto-incrementing
	///     surrogate key. There is no growth dimension that would justify a 64-bit value.
	///     </para>
	/// </remarks>
	public int Id { get; set; }

	// --- 2. Public identifier (none) ---

	// --- 3. Foreign keys + Navigation properties (none) ---

	// --- 4. Timestamps ---

	/// <summary>
	/// Gets or sets the UTC timestamp of the last completed garbage collection run.
	/// </summary>
	/// <remarks>
	/// Used by the cleanup service to throttle GC execution. If the elapsed time since this value is less
	/// than the configured interval, the GC cycle is skipped.
	/// </remarks>
	public DateTime LastRunAtUtc { get; set; }

	// --- 5. Scalar domain fields (none) ---

	// --- 6. Collection navigation properties (none) ---
}
