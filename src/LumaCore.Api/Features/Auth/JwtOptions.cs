// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Represents configuration settings required to issue and validate JSON Web Tokens (JWT)
/// used by the LumaCore API.
/// </summary>
/// <remarks>
///     <para>
///     All values in this options class are used both when issuing new tokens and when validating
///     incoming tokens. Changing any of these values in a running system will typically invalidate
///     all existing tokens.
///     </para>
///     <para>
///     In production, <see cref="SigningKey"/> should be provided via environment variables or
///     a dedicated secret store and must never be committed to source control.
///     </para>
/// </remarks>
public sealed class JwtOptions
{
	/// <summary>
	/// Gets the configuration section name used to bind <see cref="JwtOptions"/>.
	/// </summary>
	public const string SectionName = "Jwt";

	/// <summary>
	/// Gets or sets the logical issuer for generated tokens.
	/// </summary>
	[Required(ErrorMessage = "Jwt:Issuer must be configured. Use appsettings (\"Jwt\": { \"Issuer\": \"...\" }) or env var 'Jwt__Issuer'.")]
	public string Issuer { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the audience that issued tokens are intended for.
	/// </summary>
	[Required(ErrorMessage = "Jwt:Audience must be configured. Use appsettings or env var 'Jwt__Audience'.")]
	public string Audience { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the symmetric signing key used to sign and validate JWTs.
	/// Must be sufficiently long and must not be stored in source control.
	/// </summary>
	/// <remarks>
	/// This key is the only secret required to mint and validate tokens.
	/// Anyone in possession of this key can generate valid tokens for the API.
	/// </remarks>
	[Required(ErrorMessage = "Jwt:SigningKey must be configured. Use appsettings or env var 'Jwt__SigningKey'."), MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters long.")]
	public string SigningKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the lifetime of access tokens in minutes.
	/// </summary>
	[Range(1, 24 * 60)]
	public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
