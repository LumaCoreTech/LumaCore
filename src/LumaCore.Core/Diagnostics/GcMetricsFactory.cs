// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Runtime;

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Factory for creating <see cref="GcMetrics"/> snapshots.
/// </summary>
/// <remarks>
/// See <see cref="GcMetrics"/> for detailed documentation on each metric and diagnostic tips.
/// </remarks>
public static class GcMetricsFactory
{
	/// <summary>
	/// Creates a snapshot of current garbage collection metrics.
	/// </summary>
	/// <returns>
	/// A <see cref="GcMetrics"/> instance containing GC statistics and configuration.
	/// </returns>
	public static GcMetrics Create()
	{
		return new GcMetrics(
			Gen0Collections: GC.CollectionCount(0),
			Gen1Collections: GC.CollectionCount(1),
			Gen2Collections: GC.CollectionCount(2),
			IsServerGc: GCSettings.IsServerGC,
			TotalAllocatedBytes: GC.GetTotalAllocatedBytes(precise: false));
	}
}
