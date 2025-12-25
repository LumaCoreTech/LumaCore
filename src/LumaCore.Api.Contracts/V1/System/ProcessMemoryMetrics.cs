// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Process-level memory metrics as reported by the operating system.
/// </summary>
/// <param name="WorkingSetBytes">Physical memory (RAM) used by the process.</param>
/// <param name="PrivateMemoryBytes">Committed private virtual memory (RAM + paged to disk).</param>
public sealed record ProcessMemoryMetrics(
	long WorkingSetBytes,
	long PrivateMemoryBytes);
