// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Process-level resource metrics.
/// </summary>
/// <remarks>
///     <para>
///     Process metrics reflect resource consumption at the operating system level, including both managed and
///     unmanaged resources.
///     </para>
/// </remarks>
/// <param name="ThreadCount">Number of OS threads currently active in the process.</param>
/// <param name="HandleCount">Number of OS handles (files, sockets, registry keys, etc.) held by the process.</param>
/// <param name="StartTimeUtc">UTC timestamp when the process started.</param>
/// <param name="Uptime">
/// Duration the process has been running. Serializes as <c>d.hh:mm:ss.fffffff</c> (e.g., <c>1.02:30:45</c>).
/// </param>
public sealed record ProcessMetrics(
	int      ThreadCount,
	int      HandleCount,
	DateTime StartTimeUtc,
	TimeSpan Uptime);
