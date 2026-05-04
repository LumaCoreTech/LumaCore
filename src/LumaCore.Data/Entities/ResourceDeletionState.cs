// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Indicates the lifecycle state of a <see cref="ResourceEntity"/> with respect to garbage collection.
/// </summary>
/// <remarks>
///     <para>
///     The deletion state controls whether a resource is visible to normal operations (<see cref="Active"/>)
///     or has been marked for removal by the garbage collector (<see cref="PendingDeletion"/>).
///     </para>
///     <para>
///     A composite unique index on <c>(ContentHash, DeletionState)</c> allows at most one
///     <see cref="Active"/> and one <see cref="PendingDeletion"/> row per content hash. This separates
///     upload operations (which only interact with <see cref="Active"/> rows) from GC operations
///     (which only interact with <see cref="PendingDeletion"/> rows), eliminating row contention.
///     </para>
/// </remarks>
public enum ResourceDeletionState
{
	/// <summary>
	/// The resource is active and available for use. This is the default state for newly uploaded resources.
	/// </summary>
	Active = 0,

	/// <summary>
	/// The resource has been marked for deletion by the garbage collector.
	/// The associated file will be deleted during the next GC sweep phase.
	/// </summary>
	PendingDeletion = 1
}
