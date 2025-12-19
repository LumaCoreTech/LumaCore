// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Admin.Contracts.V1;

/// <summary>
/// Represents the high-level status of the running LumaCore instance as
/// returned by the <c>/api/v1/admin/status</c> endpoint.
/// </summary>
/// <param name="Environment">The logical environment of the application (for example 'Development' or 'Production').</param>
/// <param name="ApiVersion">The advertised API version.</param>
/// <param name="MachineName">The operating system machine name of the host.</param>
/// <param name="UtcNow">The current UTC time on the server.</param>
/// <param name="Jwt">Information about the JWT configuration status.</param>
public sealed record AdminStatusResponse(
	string?            Environment,
	string?            ApiVersion,
	string             MachineName,
	DateTime           UtcNow,
	AdminJwtStatusInfo Jwt);

/// <summary>
/// Represents diagnostic information about the JWT configuration used by the LumaCore API.
/// </summary>
/// <param name="Configured">
/// <see langword="true"/> if a JWT issuer, audience, and signing key are all configured;
/// otherwise, <see langword="false"/>.
/// </param>
/// <param name="Issuer">The configured JWT issuer, if any.</param>
/// <param name="Audience">The configured JWT audience, if any.</param>
/// <param name="SigningKey">
/// A masked representation of the configured signing key, or <see langword="null"/> if no key is configured.
/// The raw signing key is never exposed by this endpoint.
/// </param>
/// <param name="AccessTokenLifetimeMinutes">
/// The configured access token lifetime in minutes, if available; otherwise <see langword="null"/>.
/// </param>
public sealed record AdminJwtStatusInfo(
	bool    Configured,
	string? Issuer,
	string? Audience,
	string? SigningKey,
	int?    AccessTokenLifetimeMinutes);
