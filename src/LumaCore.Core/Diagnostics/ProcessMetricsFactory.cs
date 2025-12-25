// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Factory for creating <see cref="ProcessMetrics"/> snapshots.
/// </summary>
/// <remarks>
/// See <see cref="ProcessMetrics"/> for detailed documentation on each metric and diagnostic tips.
/// </remarks>
public static class ProcessMetricsFactory
{
	/// <summary>
	/// Creates a snapshot of current process metrics.
	/// </summary>
	/// <returns>
	/// A <see cref="ProcessMetrics"/> instance containing thread count, handle count, start time, and uptime.
	/// </returns>
	public static ProcessMetrics Create()
	{
		using var process = Process.GetCurrentProcess();

		DateTime startTimeUtc = process.StartTime.ToUniversalTime();
		DateTime now = DateTime.UtcNow;
		TimeSpan uptime = now - startTimeUtc;

		return new ProcessMetrics(
			ThreadCount: process.Threads.Count,
			HandleCount: process.HandleCount,
			StartTimeUtc: startTimeUtc,
			Uptime: uptime);
	}
}
