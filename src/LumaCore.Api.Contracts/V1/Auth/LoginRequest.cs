// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Definitions;

namespace LumaCore.Api.Contracts.V1.Auth;

/// <summary>
/// Login credentials submitted by a client to obtain a JWT.
/// </summary>
/// <param name="Username">The username for authentication.</param>
/// <param name="Password">The password for authentication. Must be at least 8 characters.</param>
/// <remarks>
/// Validation is performed automatically by the API validation filter.
/// Invalid requests receive a <c>400 Bad Request</c> response with detailed validation errors.
/// </remarks>
public sealed record LoginRequest(
	// Note: Keep this aligned with database constraints (EF Core model) and shared UI validation.
	[Required(ErrorMessage = "Username is required.")]
	[StringLength(EntityLimits.UsernameMaxLength, ErrorMessage = "Username must not exceed {1} characters.")]
	string Username,
	[Required(ErrorMessage = "Password is required.")]
	[MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
	string Password);
