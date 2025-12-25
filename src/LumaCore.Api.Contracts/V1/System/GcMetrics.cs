// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Garbage collection statistics and configuration.
/// </summary>
/// <remarks>
///     <para>
///     .NET uses a generational GC: Gen0 for short-lived objects, Gen1 as a buffer, Gen2 for long-lived objects.
///     Each generation's collection also collects younger generations.
///     </para>
///     <para>
///     <see cref="TotalAllocatedBytes"/> only increases — GC does not decrement it. Calculate allocation rate as:
///     <c>(current - previous) / elapsedSeconds</c>.
///     </para>
/// </remarks>
/// <param name="Gen0Collections">Number of Gen0 (ephemeral) garbage collections since process start.</param>
/// <param name="Gen1Collections">Number of Gen1 garbage collections since process start.</param>
/// <param name="Gen2Collections">Number of Gen2 (full) garbage collections since process start.</param>
/// <param name="IsServerGc">
/// <see langword="true"/> if Server GC is active (one managed heap per logical CPU);
/// <see langword="false"/> for Workstation GC (single managed heap).
/// </param>
/// <param name="TotalAllocatedBytes">Cumulative bytes allocated by managed code since process start.</param>
public sealed record GcMetrics(
	int  Gen0Collections,
	int  Gen1Collections,
	int  Gen2Collections,
	bool IsServerGc,
	long TotalAllocatedBytes);
