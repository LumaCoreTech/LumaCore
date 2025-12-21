// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.Health;

/// <summary>
/// Simple liveness indicator for the backend.
/// </summary>
/// <param name="Status">A short textual indicator of the backend state.</param>
public sealed record ApiHealthLiveResponse(string Status);
