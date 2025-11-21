// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Represents the response returned by the <c>/auth/introspect</c> endpoint.
/// </summary>
/// <param name="Subject">The logical subject of the token (usually the user identifier).</param>
/// <param name="Name">The display name associated with the principal, if available.</param>
/// <param name="Roles">The roles associated with the principal.</param>
/// <param name="NotBeforeUtc">The UTC time before which the token is not considered valid, if present.</param>
/// <param name="ExpiresUtc">The UTC time at which the token expires, if present.</param>
/// <param name="ExpiresIn">
/// The remaining lifetime of the token at the time of the request, or <see langword="null"/>
/// if the expiry could not be determined.
/// </param>
/// <param name="JwtId">The unique token identifier (jti claim), if present.</param>
/// <param name="Issuer">The token issuer as read from the claims, if present.</param>
/// <param name="Audience">The token audience as read from the claims, if present.</param>
/// <param name="ConfiguredAccessTokenLifetimeMinutes">
/// The configured access token lifetime in minutes as specified in <see cref="JwtOptions"/>.
/// </param>
public sealed record AuthIntrospectResponse(
	string                Subject,
	string?               Name,
	IReadOnlyList<string> Roles,
	DateTime?             NotBeforeUtc,
	DateTime?             ExpiresUtc,
	TimeSpan?             ExpiresIn,
	string?               JwtId,
	string?               Issuer,
	string?               Audience,
	int                   ConfiguredAccessTokenLifetimeMinutes);
