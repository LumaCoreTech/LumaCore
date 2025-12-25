// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Factory for creating <see cref="ThreadPoolMetrics"/> snapshots.
/// </summary>
/// <remarks>
/// See <see cref="ThreadPoolMetrics"/> for detailed documentation on each metric and diagnostic tips.
/// </remarks>
public static class ThreadPoolMetricsFactory
{
	/// <summary>
	/// Creates a snapshot of current thread pool metrics.
	/// </summary>
	/// <returns>
	/// A <see cref="ThreadPoolMetrics"/> instance containing available threads, configured
	/// limits, and pending work item count.
	/// </returns>
	public static ThreadPoolMetrics Create()
	{
		// Get available threads (remaining capacity = Max - busy)
		ThreadPool.GetAvailableThreads(out int availableWorker, out int availableIo);

		// Get minimum thread counts (instant creation threshold, no throttling below this)
		ThreadPool.GetMinThreads(out int minWorker, out int minIo);

		// Get maximum thread counts (ceiling)
		ThreadPool.GetMaxThreads(out int maxWorker, out int maxIo);

		// Pending work items indicate backpressure
		long pendingWorkItems = ThreadPool.PendingWorkItemCount;

		return new ThreadPoolMetrics(
			AvailableWorkerThreads: availableWorker,
			AvailableCompletionPortThreads: availableIo,
			MinWorkerThreads: minWorker,
			MinCompletionPortThreads: minIo,
			MaxWorkerThreads: maxWorker,
			MaxCompletionPortThreads: maxIo,
			PendingWorkItemCount: pendingWorkItems);
	}
}
