// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Represents a complete snapshot of system diagnostics metrics.
/// </summary>
/// <param name="Timestamp">
/// UTC time when the snapshot was captured.
/// </param>
/// <param name="Memory">
/// Memory usage metrics.
/// </param>
/// <param name="Gc">
/// Garbage collection metrics.
/// </param>
/// <param name="Process">
/// Process-level metrics.
/// </param>
/// <param name="ThreadPool">
/// Thread pool metrics.
/// </param>
public sealed record SystemMetrics(
	DateTime          Timestamp,
	MemoryMetrics     Memory,
	GcMetrics         Gc,
	ProcessMetrics    Process,
	ThreadPoolMetrics ThreadPool);
