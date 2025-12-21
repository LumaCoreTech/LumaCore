// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.Auth;

/// <summary>
/// Response returned after successful authentication.
/// </summary>
/// <param name="AccessToken">The issued access token (JWT).</param>
public sealed record LoginResponse(string AccessToken);
