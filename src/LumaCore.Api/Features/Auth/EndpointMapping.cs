// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.ObjectModel;
using System.Security.Claims;

using LumaCore.Api.Features.Auth.Contracts;

using Microsoft.Extensions.Options;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Provides extension methods for mapping authentication endpoints to the application's routing pipeline.
/// </summary>
/// <remarks>
///     <para>
///     This class is part of the Auth feature and exposes endpoints for login,
///     identity introspection, and token diagnostics.
///     </para>
/// </remarks>
public static class EndpointMapping
{
	/// <summary>
	/// Maps authentication-related endpoints.
	/// </summary>
	/// <param name="app">The <see cref="IEndpointRouteBuilder"/> used to define HTTP endpoints.</param>
	/// <returns>The modified application.</returns>
	/// <remarks>
	///     <para>
	///     Currently this feature exposes:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <c>POST /api/auth/login</c> – accepts a <see cref="LoginRequest"/> and returns a
	///             <see cref="LoginResponse"/> containing a short-lived access token.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>GET /api/auth/whoami</c> – returns information about the currently authenticated
	///             principal (name, roles, and raw claims). This endpoint is available to any
	///             authenticated user.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>GET /api/auth/introspect</c> – returns information about the current authenticated
	///             principal and token, including expiry information.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     The current implementation of <c>/auth/login</c> uses a single built-in administrator
	///     account. This is intended purely as a bootstrap mechanism until a persistent user
	///     store is available.
	///     </para>
	/// </remarks>
	public static IEndpointRouteBuilder MapAuthFeature(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app
			.MapGroup("/api")
			.MapGroup("/auth")
			.WithTags("Auth");

		// -----------------------------------------------------------------------------
		// AUTH FEATURE – CURRENT STATE
		//
		// This endpoint group hosts all authentication-related routes of the LumaCore
		// API. At the current stage of development the feature provides:
		//
		//   - POST /api/auth/login
		//     Issues a short-lived JWT access token for the built-in administrator
		//     account. This is a temporary bootstrap mechanism until LumaCore has a
		//     persistent user store (for example a database-backed authentication
		//     system with proper password hashing and user management).
		//
		//   - GET /api/auth/whoami
		//     Returns basic information about the currently authenticated principal,
		//     including effective name, roles, and raw claims. This endpoint is useful
		//     for debugging authentication and authorization behavior and is available
		//     to any authenticated user, not only administrators.
		//
		//   - GET /api/auth/introspect
		//     Returns diagnostic information about the current principal and token
		//     (subject, roles, issuer, audience, expiry, remaining lifetime, etc.).
		//     This endpoint is primarily intended for development and debugging of
		//     the authentication flow.
		//
		// The /auth feature currently does NOT provide:
		//
		//   - real user accounts or registration flows
		//   - password hashing or credential storage beyond the built-in admin account
		//   - refresh tokens or long-lived sessions
		//   - multi-tenant or fine-grained permission modelling
		//   - integration with external identity providers (OIDC/OAuth2)
		//
		// These capabilities are expected to be added once LumaCore introduces
		// persistent storage and a dedicated user-/identity-management subsystem.
		// -----------------------------------------------------------------------------

		group.MapPost(
				"/login",
				(
					LoginRequest     request,
					IJwtTokenFactory tokenFactory,
					ILoggerFactory   loggerFactory) =>
				{
					// Create a logger for this feature.
					ILogger logger = loggerFactory.CreateLogger("LumaCore.Auth");

					// Temporary bootstrap authentication: single hard-coded administrator account.
					if (!IsValidAdmin(request.Username, request.Password))
					{
						// Log failed authentication attempt for diagnostics. Do not log the password.
						logger.LogWarning(
							"Authentication failed for administrator login attempt with username '{Username}'.",
							request.Username);

						// Deliberately return a generic 401 without details to avoid
						// leaking information about valid user names.
						return Results.Unauthorized();
					}

					// Build identity claims for the authenticated administrator.
					var claims = new[]
					{
						// Rely on 'sub' => NameIdentifier mapping; no need to add it explicitly.
						new Claim(ClaimTypes.Name, request.Username),
						new Claim(ClaimTypes.Role, "admin")
					};

					// Issue a signed JWT that the client can present in subsequent API calls.
					string token = tokenFactory.CreateToken(request.Username, claims);

					// Log successful authentication for auditing purposes.
					logger.LogInformation(
						"Administrator '{Username}' successfully authenticated and issued a JWT access token.",
						request.Username);

					// Return the access token to the caller.
					return Results.Ok(new LoginResponse(token));
				})
			.AllowAnonymous()
			.WithSummary("Authenticates the built-in admin and issues a JWT access token.")
			.WithDescription(
				"Authenticates the temporary, built-in administrator account and returns a " +
				"short-lived JWT access token. This endpoint is intended for development and " +
				"bootstrap scenarios only and will be replaced by a proper, database-backed " +
				"authentication flow once persistent user management is available.")
			.WithName("Login");

		// Exposes basic information about the current authenticated principal. This is
		// useful as a simple "who am I according to the API?" endpoint and is available
		// to any authenticated user, not only administrators.
		group.MapGet(
				"/whoami",
				(ClaimsPrincipal user) =>
				{
					// Extract the effective user name from the identity.
					// If no name is available, return "(anonymous)".
					string name = user.Identity?.Name ?? "(anonymous)";

					// Extract roles from claims.
					// The 'role' claim is the standard JWT claim for user roles.
					ReadOnlyCollection<string> roles = user
						.FindAll(ClaimTypes.Role)
						.Select(r => r.Value)
						.ToList()
						.AsReadOnly();

					// Extract all claims as raw type/value pairs.
					// This allows clients to inspect the full set of claims.
					// The claims are returned in no particular order.
					ReadOnlyCollection<AuthClaimItem> claims = user.Claims
						.Select(c => new AuthClaimItem(c.Type, c.Value))
						.ToList()
						.AsReadOnly();

					// Build and return the whoami response.
					var response = new AuthWhoAmIResponse(
						Name: name,
						Roles: roles,
						Claims: claims);

					return Results.Ok(response);
				})
			.RequireAuthorization()
			.Produces<AuthWhoAmIResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.WithSummary("Returns the current authenticated user.")
			.WithDescription(
				"Returns basic identity information for the current authenticated principal, " +
				"including effective name, roles, and raw claims. Intended for any authenticated " +
				"user and typically used by client applications to show “who am I?” within the UI.")
			.WithName("AuthWhoAmI");

		// Introspection endpoint that returns information about the current authenticated
		// principal and token (e.g. expiry and remaining lifetime). This is primarily
		// intended for diagnostics and development.
		group.MapGet(
				"/introspect",
				(
					ClaimsPrincipal      user,
					IOptions<JwtOptions> jwtOptionsAccessor,
					ILoggerFactory       loggerFactory) =>
				{
					// Create a logger for this feature.
					ILogger logger = loggerFactory.CreateLogger("LumaCore.Auth");

					// Capture the current time in UTC for lifetime calculations.
					DateTime utcNow = DateTime.UtcNow;

					// Retrieve JWT configuration options.
					JwtOptions jwtOptions = jwtOptionsAccessor.Value;

					// Extract the subject claim
					// The 'sub' claim is the standard JWT subject claim.
					// If it's not present, we fall back to the Identity Name.
					// If that's also not available, we use "(unknown)".
					string subject = user.FindFirst("sub")?.Value
					                 ?? user.Identity?.Name
					                 ?? "(unknown)";

					// Extract the name claim (if present).
					// The 'name' claim is a standard JWT claim for the user's full name.
					string? name = user.Identity?.Name;

					// Extract roles from claims.
					// The 'role' claim is the standard JWT claim for user roles.
					ReadOnlyCollection<string> roles = user.FindAll(ClaimTypes.Role)
						.Select(r => r.Value)
						.ToList()
						.AsReadOnly();

					// Extract token timing claims (if present).
					// The 'nbf' (not before) and 'exp' (expiry) claims are standard JWT claims.
					// They are represented as Unix timestamps in seconds.
					string? nbfClaim = user.FindFirst("nbf")?.Value;
					string? expClaim = user.FindFirst("exp")?.Value;

					// Parse timing claims into UTC DateTime values where possible.
					// If parsing fails or the claim is missing, the result is null.
					// This allows us to represent optional timing information.
					DateTime? notBeforeUtc = TryParseUnixTimeSeconds(nbfClaim);
					DateTime? expiresUtc = TryParseUnixTimeSeconds(expClaim);

					// Calculate remaining lifetime (ExpiresIn) if expiry is known.
					// If the token has already expired, ExpiresIn is set to zero.
					// If expiry is unknown, ExpiresIn remains null.
					TimeSpan? expiresIn = null;
					if (expiresUtc.HasValue)
					{
						expiresIn = expiresUtc.Value - utcNow;
						if (expiresIn < TimeSpan.Zero)
							expiresIn = TimeSpan.Zero;
					}

					// Extract other standard JWT claims.
					// The 'jti' (JWT ID), 'iss' (issuer), and 'aud' (audience) claims
					// are standard JWT claims that may be present.
					// They are optional and may be null.
					string? jwtId = user.FindFirst("jti")?.Value;
					string? issuer = user.FindFirst("iss")?.Value;
					string? audience = user.FindFirst("aud")?.Value;

					// Build and return the introspection response.
					// This includes all extracted information about the token
					// and the configured access token lifetime for reference.
					var response = new AuthIntrospectResponse(
						Subject: subject,
						Name: name,
						Roles: roles,
						NotBeforeUtc: notBeforeUtc,
						ExpiresUtc: expiresUtc,
						ExpiresIn: expiresIn,
						JwtId: jwtId,
						Issuer: issuer,
						Audience: audience,
						ConfiguredAccessTokenLifetimeMinutes: jwtOptions.AccessTokenLifetimeMinutes);

					// Log the introspection request for diagnostics.
					logger.LogDebug(
						"Introspection requested for subject '{Subject}'.",
						subject);

					// Return the introspection response.
					return Results.Ok(response);
				})
			.RequireAuthorization()
			.Produces<AuthIntrospectResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.WithSummary("Introspects the current JWT and returns details about the token.")
			.WithDescription(
				"Provides diagnostic information about the currently used JWT access token, " +
				"including subject, roles, expiry and configured lifetime. This endpoint is " +
				"intended primarily for debugging and support scenarios.")
			.WithName("Introspect");

		return app;
	}

	/// <summary>
	/// Performs the current, minimal authentication based on a single hard-coded admin account.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This method is intended as a temporary bootstrap mechanism only. It must be replaced
	///     by a proper authentication flow backed by a persistent user store before the system
	///     is exposed to untrusted networks.
	///     </para>
	/// </remarks>
	/// <param name="username">The supplied user name.</param>
	/// <param name="password">The supplied password.</param>
	/// <returns>
	/// <see langword="true"/> if the credentials match the built-in admin account;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	private static bool IsValidAdmin(string? username, string? password)
	{
		// NOTE:
		// This is intentionally simple bootstrap logic. It is not meant for production
		// and should be replaced with a proper user store (e.g. database, external IdP)
		// once LumaCore has persistent storage.
		const string AdminUserName = "admin";
		const string AdminPassword = "changeme";

		username = username?.Trim() ?? string.Empty;

		return string.Equals(username, AdminUserName, StringComparison.OrdinalIgnoreCase) &&
		       string.Equals(password, AdminPassword, StringComparison.Ordinal);
	}

	/// <summary>
	/// Attempts to parse a Unix timestamp in seconds into a <see cref="DateTime"/> in UTC.
	/// </summary>
	/// <param name="value">The string representation of the Unix timestamp.</param>
	/// <returns>
	/// A <see cref="DateTime"/> in UTC if parsing succeeds; otherwise, <see langword="null"/>.
	/// </returns>
	private static DateTime? TryParseUnixTimeSeconds(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (!long.TryParse(value, out long seconds))
			return null;

		return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
	}
}
