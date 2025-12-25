// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Metrics for the managed GC heap.
/// </summary>
/// <param name="LiveBytes">Approximate bytes used by live (reachable) managed objects.</param>
/// <param name="HeapSizeBytes">Total size of the managed heap, including live objects and free space.</param>
/// <param name="FragmentedBytes">Free bytes scattered between allocated objects.</param>
/// <param name="PinnedObjectsCount">Number of pinned objects (cannot be moved by GC).</param>
/// <param name="TotalAvailableBytes">
/// GC heap budget.
/// In containers: ~75% of container limit.
/// Without containers: typically equals system physical memory.
/// </param>
public sealed record ManagedHeapMetrics(
	long LiveBytes,
	long HeapSizeBytes,
	long FragmentedBytes,
	long PinnedObjectsCount,
	long TotalAvailableBytes);
