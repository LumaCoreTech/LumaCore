// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Represents a snapshot of memory usage and availability metrics, organized by scope.
/// </summary>
/// <remarks>
///     <para>
///     Memory metrics are grouped into five categories: <see cref="Managed"/> (GC-controlled heap),
///     <see cref="Process"/> (OS-level view), <see cref="System"/> (host machine), <see cref="Container"/>
///     (cgroup limits, if applicable), and <see cref="Effective"/> (computed values for utilization).
///     </para>
///     <para>
///     For utilization calculations:
///     </para>
///     <list type="bullet">
///         <item>
///         System/container level: <c>Effective.UsageBytes / Effective.LimitBytes</c>
///         </item>
///         <item>
///         Managed heap level: <c>Managed.HeapSizeBytes / Managed.TotalAvailableBytes</c>
///         </item>
///     </list>
/// </remarks>
/// <param name="Managed">Managed heap metrics (GC-controlled memory).</param>
/// <param name="Process">Process-level memory metrics (OS view).</param>
/// <param name="System">Host machine memory metrics.</param>
/// <param name="Container">Container memory metrics, or <see langword="null"/> if not in a container.</param>
/// <param name="Effective">Computed effective limits and usage for utilization calculations.</param>
public sealed record MemoryMetrics(
	ManagedHeapMetrics   Managed,
	ProcessMemoryMetrics Process,
	SystemMemoryMetrics  System,
	ContainerMetrics?    Container,
	EffectiveMetrics     Effective);
