// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Process-level memory metrics as reported by the operating system.
/// </summary>
/// <param name="WorkingSetBytes">
/// Physical memory (RAM) currently used by the process. Includes both managed and unmanaged memory.
/// </param>
/// <param name="PrivateMemoryBytes">
/// Committed private virtual memory (RAM + paged to disk). Does not include reserved-only pages.
/// </param>
public sealed record ProcessMemoryMetrics(
	long WorkingSetBytes,
	long PrivateMemoryBytes);
