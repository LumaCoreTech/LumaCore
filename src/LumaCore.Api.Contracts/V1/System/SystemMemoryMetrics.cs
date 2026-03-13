// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Physical host machine memory metrics.
/// </summary>
/// <param name="TotalPhysicalBytes">Total physical RAM, or <see langword="null"/> if unavailable.</param>
/// <param name="AvailablePhysicalBytes">Available physical RAM, or <see langword="null"/> if unavailable.</param>
/// <remarks>
/// In containers, these values reflect the host, not the container limit. Use <see cref="MemoryMetrics.Effective"/>
/// for the actual ceiling.
/// </remarks>
public sealed record SystemMemoryMetrics(
	long? TotalPhysicalBytes,
	long? AvailablePhysicalBytes);
