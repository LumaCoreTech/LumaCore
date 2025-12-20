// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.Auth;

/// <summary>
/// Represents the response returned after successful authentication.
/// </summary>
/// <remarks>
/// This record is produced by the <c>POST /api/v1/auth/login</c> endpoint.
/// </remarks>
/// <param name="AccessToken">The issued access token (JWT).</param>
public sealed record LoginResponse(string AccessToken);
