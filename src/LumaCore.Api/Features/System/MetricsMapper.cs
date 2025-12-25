// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using CoreDiag = LumaCore.Core.Diagnostics;
using V1 = LumaCore.Api.Contracts.V1.System;

namespace LumaCore.Api.Features.System;

/// <summary>
/// Maps Core diagnostics types to API Contracts types.
/// </summary>
/// <remarks>
///     <para>
///     This mapper provides explicit, compile-time safe conversion between <c>LumaCore.Core.Diagnostics</c> types
///     and <c>LumaCore.Api.Contracts.V1.System</c> types. If either side changes, the build will fail, preventing
///     accidental drift between Core and Contracts.
///     </para>
///     <para>
///     The separation between Core and Contracts allows API versioning: when <c>V2</c> introduces breaking changes,
///     a new mapper can be created while <c>V1</c> continues to work unchanged.
///     </para>
/// </remarks>
static class MetricsMapper
{
	/// <summary>
	/// Maps <see cref="CoreDiag.GcMetrics"/> to <see cref="V1.GcMetrics"/>.
	/// </summary>
	/// <param name="source">The Core GC metrics to map.</param>
	/// <returns>The mapped Contracts GC metrics.</returns>
	public static V1.GcMetrics ToContract(CoreDiag.GcMetrics source) => new(
		Gen0Collections: source.Gen0Collections,
		Gen1Collections: source.Gen1Collections,
		Gen2Collections: source.Gen2Collections,
		IsServerGc: source.IsServerGc,
		TotalAllocatedBytes: source.TotalAllocatedBytes);

	/// <summary>
	/// Maps <see cref="CoreDiag.MemoryMetrics"/> to <see cref="V1.MemoryMetrics"/>.
	/// </summary>
	/// <param name="source">The Core memory metrics to map.</param>
	/// <returns>The mapped Contracts memory metrics.</returns>
	public static V1.MemoryMetrics ToContract(CoreDiag.MemoryMetrics source) => new(
		Managed: ToContract(source.Managed),
		Process: ToContract(source.Process),
		System: ToContract(source.System),
		Container: source.Container is { } c ? ToContract(c) : null,
		Effective: ToContract(source.Effective));

	/// <summary>
	/// Maps <see cref="CoreDiag.ManagedHeapMetrics"/> to <see cref="V1.ManagedHeapMetrics"/>.
	/// </summary>
	/// <param name="source">The Core managed heap metrics to map.</param>
	/// <returns>The mapped Contracts managed heap metrics.</returns>
	public static V1.ManagedHeapMetrics ToContract(CoreDiag.ManagedHeapMetrics source) => new(
		LiveBytes: source.LiveBytes,
		HeapSizeBytes: source.HeapSizeBytes,
		FragmentedBytes: source.FragmentedBytes,
		PinnedObjectsCount: source.PinnedObjectsCount,
		TotalAvailableBytes: source.TotalAvailableBytes);

	/// <summary>
	/// Maps <see cref="CoreDiag.ProcessMemoryMetrics"/> to <see cref="V1.ProcessMemoryMetrics"/>.
	/// </summary>
	/// <param name="source">The Core process memory metrics to map.</param>
	/// <returns>The mapped Contracts process memory metrics.</returns>
	public static V1.ProcessMemoryMetrics ToContract(CoreDiag.ProcessMemoryMetrics source) => new(
		WorkingSetBytes: source.WorkingSetBytes,
		PrivateMemoryBytes: source.PrivateMemoryBytes);

	/// <summary>
	/// Maps <see cref="CoreDiag.SystemMemoryMetrics"/> to <see cref="V1.SystemMemoryMetrics"/>.
	/// </summary>
	/// <param name="source">The Core system memory metrics to map.</param>
	/// <returns>The mapped Contracts system memory metrics.</returns>
	public static V1.SystemMemoryMetrics ToContract(CoreDiag.SystemMemoryMetrics source) => new(
		TotalPhysicalBytes: source.TotalPhysicalBytes,
		AvailablePhysicalBytes: source.AvailablePhysicalBytes);

	/// <summary>
	/// Maps <see cref="CoreDiag.ContainerMetrics"/> to <see cref="V1.ContainerMetrics"/>.
	/// </summary>
	/// <param name="source">The Core container metrics to map.</param>
	/// <returns>The mapped Contracts container metrics.</returns>
	public static V1.ContainerMetrics ToContract(CoreDiag.ContainerMetrics source) => new(
		LimitBytes: source.LimitBytes,
		UsageBytes: source.UsageBytes);

	/// <summary>
	/// Maps <see cref="CoreDiag.EffectiveMetrics"/> to <see cref="V1.EffectiveMetrics"/>.
	/// </summary>
	/// <param name="source">The Core effective metrics to map.</param>
	/// <returns>The mapped Contracts effective metrics.</returns>
	public static V1.EffectiveMetrics ToContract(CoreDiag.EffectiveMetrics source) => new(
		LimitBytes: source.LimitBytes,
		UsageBytes: source.UsageBytes);

	/// <summary>
	/// Maps <see cref="CoreDiag.ProcessMetrics"/> to <see cref="V1.ProcessMetrics"/>.
	/// </summary>
	/// <param name="source">The Core process metrics to map.</param>
	/// <returns>The mapped Contracts process metrics.</returns>
	public static V1.ProcessMetrics ToContract(CoreDiag.ProcessMetrics source) => new(
		ThreadCount: source.ThreadCount,
		HandleCount: source.HandleCount,
		StartTimeUtc: source.StartTimeUtc,
		Uptime: source.Uptime);

	/// <summary>
	/// Maps <see cref="CoreDiag.ThreadPoolMetrics"/> to <see cref="V1.ThreadPoolMetrics"/>.
	/// </summary>
	/// <param name="source">The Core thread pool metrics to map.</param>
	/// <returns>The mapped Contracts thread pool metrics.</returns>
	public static V1.ThreadPoolMetrics ToContract(CoreDiag.ThreadPoolMetrics source) => new(
		AvailableWorkerThreads: source.AvailableWorkerThreads,
		AvailableCompletionPortThreads: source.AvailableCompletionPortThreads,
		MinWorkerThreads: source.MinWorkerThreads,
		MinCompletionPortThreads: source.MinCompletionPortThreads,
		MaxWorkerThreads: source.MaxWorkerThreads,
		MaxCompletionPortThreads: source.MaxCompletionPortThreads,
		PendingWorkItemCount: source.PendingWorkItemCount);

	/// <summary>
	/// Maps <see cref="CoreDiag.SystemMetrics"/> to <see cref="V1.SystemMetricsResponse"/>.
	/// </summary>
	/// <param name="source">The Core system metrics snapshot to map.</param>
	/// <returns>The mapped Contracts response (without Extensions — those are added by the aggregator).</returns>
	public static V1.SystemMetricsResponse ToContract(CoreDiag.SystemMetrics source) => new(
		Timestamp: source.Timestamp,
		Gc: ToContract(source.Gc),
		Memory: ToContract(source.Memory),
		Process: ToContract(source.Process),
		ThreadPool: ToContract(source.ThreadPool));
}
