// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Physical host machine memory metrics.
/// </summary>
/// <param name="TotalPhysicalBytes">
/// Total physical RAM on the host machine. Returns <see langword="null"/> if detection fails.
/// </param>
/// <param name="AvailablePhysicalBytes">
/// Currently available physical RAM on the host machine. Returns <see langword="null"/> if detection fails.
/// </param>
/// <remarks>
/// In containers, these values reflect the host machine, not the container limit.
/// Use <see cref="MemoryMetrics.Effective"/> for the actual ceiling.
/// </remarks>
public sealed record SystemMemoryMetrics(
	long? TotalPhysicalBytes,
	long? AvailablePhysicalBytes);
