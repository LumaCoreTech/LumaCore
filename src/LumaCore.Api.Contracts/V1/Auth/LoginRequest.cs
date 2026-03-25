// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Definitions;

namespace LumaCore.Api.Contracts.V1.Auth;

/// <summary>
/// Login credentials submitted by a client to obtain a JWT.
/// </summary>
/// <remarks>
/// Validation is performed automatically by the API validation filter. Invalid requests receive a
/// <c>400 Bad Request</c> response with detailed validation errors.
/// </remarks>
public sealed record LoginRequest
{
	// Implementation note: validation attributes must live on properties (not positional-record
	// constructor parameters) so that Validator.TryValidateObject() — used by the ValidationFilter —
	// can see them at runtime.
	//
	// Keep this aligned with database constraints (EF Core model) and shared UI validation.

	/// <summary>
	/// The username for authentication.
	/// </summary>
	[Required(ErrorMessage = "Username is required.")]
	[StringLength(EntityLimits.UsernameMaxLength, ErrorMessage = "Username must not exceed {1} characters.")]
	public required string Username { get; init; }

	/// <summary>
	/// The password for authentication. Must be at least 8 characters.
	/// </summary>
	[Required(ErrorMessage = "Password is required.")]
	[MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
	public required string Password { get; init; }

	/// <summary>
	/// Controls cookie persistence for browser clients. When <see langword="true"/>, the server sets a persistent
	/// cookie that survives browser restarts (expires when the token expires). When <see langword="false"/>, a
	/// session cookie is set that is cleared when the browser closes. API clients using
	/// <c>Authorization: Bearer</c> can ignore this field.
	/// </summary>
	public bool RememberMe { get; init; }
}
