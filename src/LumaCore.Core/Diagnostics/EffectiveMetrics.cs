// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Computed effective memory limits and usage for utilization calculations.
/// </summary>
/// <param name="LimitBytes">
/// The actual memory ceiling: the lower of container limit and system physical RAM.
/// Returns <see langword="null"/> if neither could be determined.
/// </param>
/// <param name="UsageBytes">
/// The memory usage to compare against <see cref="LimitBytes"/>: container usage if available,
/// otherwise process working set.
/// </param>
public sealed record EffectiveMetrics(
	long? LimitBytes,
	long  UsageBytes);
