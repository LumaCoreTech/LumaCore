// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Container memory metrics from cgroup.
/// </summary>
/// <remarks>
/// <see cref="LimitBytes"/> can exceed physical host RAM if the container is misconfigured.
/// </remarks>
/// <param name="LimitBytes">Container memory limit (cgroup hard limit).</param>
/// <param name="UsageBytes">Current container memory usage (what the OOM killer monitors).</param>
public sealed record ContainerMetrics(
	long LimitBytes,
	long UsageBytes);
