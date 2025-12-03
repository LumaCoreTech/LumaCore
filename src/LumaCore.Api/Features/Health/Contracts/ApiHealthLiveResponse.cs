// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Health.Contracts;

/// <summary>
/// Represents the response returned by the <c>/api/health/live</c> endpoint.
/// </summary>
/// <param name="Status">A short textual indicator of the backend state.</param>
public sealed record ApiHealthLiveResponse(string Status);
