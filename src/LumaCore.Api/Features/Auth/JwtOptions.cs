// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using Microsoft.IdentityModel.Tokens;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Represents configuration settings required to issue and validate JSON Web Tokens (JWT)
/// for the LumaCore HTTP API.
/// </summary>
/// <remarks>
///     <para>
///     All values in this options class are used both when issuing new tokens and when validating
///     incoming tokens. Changing any of these values in a running system will typically invalidate
///     all existing tokens and force clients to re-authenticate.
///     </para>
///     <para>
///     Options are bound from the configuration section identified by <see cref="SectionName"/>.
///     With the current defaults this corresponds to the <c>"Jwt"</c> section in <c>appsettings.json</c>
///     and environment variables prefixed with <c>Jwt__</c> (for example <c>Jwt__Issuer</c>).
///     </para>
///     <para>
///     In development the settings are usually provided via <c>appsettings.Development.json</c>.
///     In production, sensitive values such as <see cref="SigningKey"/> must be supplied via environment
///     variables or a dedicated secret store and must never be committed to source control.
///     </para>
/// </remarks>
public sealed class JwtOptions
{
	/// <summary>
	/// Gets the configuration section name used to bind <see cref="JwtOptions"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     LumaCore binds JWT settings from the configuration section identified by this name.
	///     With the default value <c>"Jwt"</c> the following mappings are used:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description><c>Jwt:Issuer</c> / <c>Jwt__Issuer</c></description>
	///         </item>
	///         <item>
	///             <description><c>Jwt:Audience</c> / <c>Jwt__Audience</c></description>
	///         </item>
	///         <item>
	///             <description><c>Jwt:SigningKey</c> / <c>Jwt__SigningKey</c></description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>Jwt:AccessTokenLifetimeMinutes</c> / <c>Jwt__AccessTokenLifetimeMinutes</c>
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     Container-based deployments should prefer environment variables over static JSON files,
	///     especially for <see cref="SigningKey"/>.
	///     </para>
	/// </remarks>
	public const string SectionName = "Jwt";

	/// <summary>
	/// Gets or sets the logical issuer for generated tokens.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The issuer is written to the <c>iss</c> claim of each token and is validated when tokens
	///     are received by the API. Typical values are a deployment-specific identifier such as
	///     <c>"LumaCore"</c> or a fully-qualified URL like <c>"https://lumacore.local"</c>.
	///     </para>
	///     <para>
	///     This value is bound from configuration key <c>Jwt:Issuer</c> (or environment variable
	///     <c>Jwt__Issuer</c>).
	///     </para>
	/// </remarks>
	[Required(ErrorMessage = "Jwt:Issuer must be configured. Set configuration key 'Jwt:Issuer' or environment variable 'Jwt__Issuer'.")]
	public string Issuer { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the expected audience for generated tokens.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The audience is written to the <c>aud</c> claim of each token and is validated when tokens
	///     are received by the API. It should identify the intended recipients of the token, for example
	///     <c>"LumaCore"</c>.
	///     </para>
	///     <para>
	///     This value is bound from configuration key <c>Jwt:Audience</c> (or environment variable
	///     <c>Jwt__Audience</c>).
	///     </para>
	/// </remarks>
	[Required(ErrorMessage = "Jwt:Audience must be configured. Set configuration key 'Jwt:Audience' or environment variable 'Jwt__Audience'.")]
	public string Audience { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the symmetric signing key used for HMAC-based JWT signing.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This key is the only secret required to mint and validate tokens. Anyone in possession of this key
	///     can generate valid tokens for the API. It must therefore be treated as a secret and must never
	///     be checked into source control.
	///     </para>
	///     <para>
	///     A minimum length of 32 characters is enforced to ensure sufficient entropy when used with
	///     <c>HS256</c> (<see cref="SecurityAlgorithms.HmacSha256"/>).
	///     </para>
	///     <para>
	///     This value is bound from configuration key <c>Jwt:SigningKey</c> (or environment variable
	///     <c>Jwt__SigningKey</c>). In production this should be provided via environment variables or
	///     a dedicated secrets mechanism (for example Docker secrets or a cloud secret store).
	///     </para>
	/// </remarks>
	[Required(ErrorMessage = "Jwt:SigningKey must be configured. Set configuration key 'Jwt:SigningKey' or environment variable 'Jwt__SigningKey'.")]
	[MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters long. Use a long, random secret and do not commit it to source control.")]
	public string SigningKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the lifetime of access tokens in minutes.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Shorter lifetimes reduce the window in which a stolen token can be abused, at the cost of
	///     requiring clients to re-authenticate more frequently. Longer lifetimes increase convenience
	///     but also increase potential impact if a token is leaked.
	///     </para>
	///     <para>
	///     This value is bound from configuration key <c>Jwt:AccessTokenLifetimeMinutes</c> (or environment
	///     variable <c>Jwt__AccessTokenLifetimeMinutes</c>). The allowed range is between 1 and 1440 minutes
	///     (1 day).
	///     </para>
	/// </remarks>
	[Range(1, 24 * 60, ErrorMessage = "Jwt:AccessTokenLifetimeMinutes must be between 1 and 1440 minutes.")]
	public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
