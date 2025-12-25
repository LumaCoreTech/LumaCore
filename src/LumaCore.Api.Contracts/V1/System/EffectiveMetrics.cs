// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Computed effective memory limits and usage for utilization calculations.
/// </summary>
/// <param name="LimitBytes">
/// Actual memory ceiling: min of container limit and system RAM. <see langword="null"/> if undetermined.
/// </param>
/// <param name="UsageBytes">
/// Memory usage for utilization: container usage if available, otherwise process working set.
/// </param>
public sealed record EffectiveMetrics(
	long? LimitBytes,
	long  UsageBytes);
