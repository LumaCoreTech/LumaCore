// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Factory responsible for creating complete <see cref="SystemMetrics"/> snapshots by orchestrating the individual
/// metrics factories.
/// </summary>
/// <remarks>
///     <para>
///     This factory aggregates metrics from <see cref="MemoryMetricsFactory"/>, <see cref="GcMetricsFactory"/>,
///     <see cref="ProcessMetricsFactory"/>, and <see cref="ThreadPoolMetricsFactory"/> into a single coherent
///     snapshot.
///     </para>
///     <para>
///     All metrics are captured as close together as possible to represent a consistent point-in-time view, though
///     minor variations may occur due to the sequential nature of data collection.
///     </para>
/// </remarks>
public static class SystemMetricsFactory
{
	/// <summary>
	/// Creates a complete snapshot of all system metrics.
	/// </summary>
	/// <returns>
	/// A <see cref="SystemMetrics"/> instance containing memory, GC, process, and thread pool metrics captured at
	/// approximately the same point in time.
	/// </returns>
	public static SystemMetrics Create()
	{
		// Capture timestamp first to represent the snapshot time.
		DateTime timestamp = DateTime.UtcNow;

		// Collect metrics from individual factories.
		// Order is intentional: memory/GC first (most volatile), then process/threadpool.
		MemoryMetrics memory = MemoryMetricsFactory.Create();
		GcMetrics gc = GcMetricsFactory.Create();
		ProcessMetrics process = ProcessMetricsFactory.Create();
		ThreadPoolMetrics threadPool = ThreadPoolMetricsFactory.Create();

		return new SystemMetrics(
			Timestamp: timestamp,
			Memory: memory,
			Gc: gc,
			Process: process,
			ThreadPool: threadPool);
	}
}
