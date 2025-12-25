// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Represents a snapshot of process-level resource metrics.
/// </summary>
/// <remarks>
///     <para>
///     Process metrics provide OS-level visibility into resource usage. These complement managed metrics by showing
///     the "outside view" of the application.
///     </para>
/// </remarks>
/// <param name="ThreadCount">
/// Number of OS threads currently active in the process. Includes both managed threads (thread pool, explicit
/// Thread instances) and native threads.
/// </param>
/// <param name="HandleCount">
/// Number of OS handles (files, sockets, registry keys, etc.) held by the process.
/// </param>
/// <param name="StartTimeUtc">
/// UTC timestamp when the process was started.
/// </param>
/// <param name="Uptime">
/// Duration the process has been running since <see cref="StartTimeUtc"/>. Can be used to normalize cumulative
/// counters into rates: <c>totalAllocatedBytes / uptime.TotalSeconds</c> = average allocation rate.
/// </param>
public sealed record ProcessMetrics(
	int      ThreadCount,
	int      HandleCount,
	DateTime StartTimeUtc,
	TimeSpan Uptime);
