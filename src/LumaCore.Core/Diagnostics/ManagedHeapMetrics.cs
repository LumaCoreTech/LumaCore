// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Metrics for the managed GC heap.
/// </summary>
/// <param name="LiveBytes">Approximate bytes used by live (reachable) managed objects.</param>
/// <param name="HeapSizeBytes">
/// Total size of the managed heap in bytes, including both live objects and free space. This is memory reserved
/// by the GC, not necessarily committed physical memory.
/// </param>
/// <param name="FragmentedBytes">
/// Free bytes scattered between allocated objects. Fragmentation occurs when free space exists but is not
/// contiguous — often due to pinned objects preventing compaction.
/// </param>
/// <param name="PinnedObjectsCount">
/// Number of objects pinned in memory (cannot be moved by GC). Pinning is necessary for interop with native code
/// but prevents heap compaction.
/// </param>
/// <param name="TotalAvailableBytes">
/// GC heap budget. In containers: ~75% of container limit. Without containers: equals system physical memory
/// unless manually configured via <c>DOTNET_GCHeapHardLimit</c> or <c>runtimeconfig.json</c>.
/// </param>
public sealed record ManagedHeapMetrics(
	long LiveBytes,
	long HeapSizeBytes,
	long FragmentedBytes,
	long PinnedObjectsCount,
	long TotalAvailableBytes);
