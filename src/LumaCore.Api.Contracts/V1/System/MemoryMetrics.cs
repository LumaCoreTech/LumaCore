// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Memory usage and availability metrics, organized by scope.
/// </summary>
/// <param name="Managed">Managed heap metrics (GC-controlled memory).</param>
/// <param name="Process">Process-level memory metrics (OS view).</param>
/// <param name="System">Host machine memory metrics.</param>
/// <param name="Container">Container memory metrics, or <see langword="null"/> if not in a container.</param>
/// <param name="Effective">Computed effective limits and usage for utilization calculations.</param>
/// <remarks>
///     <para>
///     For utilization calculations: <c>Effective.UsageBytes / Effective.LimitBytes</c> (system level)
///     or <c>Managed.HeapSizeBytes / Managed.TotalAvailableBytes</c> (managed heap level).
///     </para>
/// </remarks>
public sealed record MemoryMetrics(
	ManagedHeapMetrics   Managed,
	ProcessMemoryMetrics Process,
	SystemMemoryMetrics  System,
	ContainerMetrics?    Container,
	EffectiveMetrics     Effective);
