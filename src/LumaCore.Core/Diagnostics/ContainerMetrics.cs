// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Container memory metrics from cgroup.
/// </summary>
/// <param name="LimitBytes">
/// Container memory limit (cgroup hard limit). This is the ceiling before the OOM killer terminates the
/// container. Can exceed system physical RAM if misconfigured.
/// </param>
/// <param name="UsageBytes">
/// Current memory usage of the container (cgroup <c>memory.current</c>). This is what the OOM killer
/// monitors — includes process RSS, page cache, and kernel structures.
/// </param>
public sealed record ContainerMetrics(
	long LimitBytes,
	long UsageBytes);
