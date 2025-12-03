// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Auth.Contracts;

/// <summary>
/// Represents login credentials submitted by a client in order to obtain a JWT.
/// </summary>
/// <remarks>
/// This record is consumed by the <c>POST /api/auth/login</c> endpoint in <see cref="EndpointMapping"/>.
/// </remarks>
/// <param name="Username">The username.</param>
/// <param name="Password">The password.</param>
public sealed record LoginRequest(string Username, string Password);
