// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Api.Contracts.V1.Auth;

/// <summary>
/// Represents login credentials submitted by a client in order to obtain a JWT.
/// </summary>
/// <remarks>
///     <para>
///     This record is consumed by the <c>POST /api/v1/auth/login</c> endpoint.
///     </para>
///     <para>
///     Validation is performed automatically by the API validation filter
///     when this record is used as an endpoint parameter. Invalid requests receive a
///     <c>400 Bad Request</c> response with detailed validation errors.
///     </para>
/// </remarks>
/// <param name="Username">
/// The username for authentication. Must be between 1 and 100 characters.
/// </param>
/// <param name="Password">
/// The password for authentication. Must be at least 8 characters.
/// </param>
public sealed record LoginRequest(
	[Required(ErrorMessage = "Username is required.")]
	[StringLength(100, ErrorMessage = "Username must not exceed 100 characters.")]
	string Username,
	[Required(ErrorMessage = "Password is required.")]
	[MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
	string Password);
