// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Represents a snapshot of garbage collection statistics and configuration.
/// </summary>
/// <remarks>
///     <para>
///     The .NET GC uses a generational model: Gen0 for short-lived objects, Gen1 as a buffer, and Gen2 for
///     long-lived objects. Each generation collection also collects all younger generations (a Gen2 collection
///     includes Gen1 and Gen0).
///     </para>
///     <para>
///     Allocation rate can be calculated from <see cref="TotalAllocatedBytes"/>:
///     <c>(current - previous) / elapsedSeconds</c> = bytes allocated per second.
///     </para>
/// </remarks>
/// <param name="Gen0Collections">
/// Number of Gen0 (ephemeral) collections since process start. Gen0 collects short-lived objects and is the
/// fastest generation to collect.
/// </param>
/// <param name="Gen1Collections">
/// Number of Gen1 collections since process start. Gen1 acts as a buffer between short-lived (Gen0) and
/// long-lived (Gen2) objects. Objects surviving Gen0 are promoted here.
/// </param>
/// <param name="Gen2Collections">
/// Number of Gen2 (full) collections since process start. Gen2 holds long-lived objects and is the most
/// expensive generation to collect.
/// </param>
/// <param name="IsServerGc">
/// <see langword="true"/> if server GC mode is active. Server GC uses one managed heap per logical CPU for
/// parallel collection. Workstation GC (<see langword="false"/>) uses a single managed heap.
/// </param>
/// <param name="TotalAllocatedBytes">
/// Cumulative bytes allocated by managed code since process start. This counter only increases — GC does not
/// decrement it when reclaiming memory.
/// </param>
public sealed record GcMetrics(
	int  Gen0Collections,
	int  Gen1Collections,
	int  Gen2Collections,
	bool IsServerGc,
	long TotalAllocatedBytes);
