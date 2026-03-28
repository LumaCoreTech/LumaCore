// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.ObjectModel;
using System.Security.Claims;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.UserManagement;

using Microsoft.Extensions.Options;

using V1 = LumaCore.Api.Contracts.V1.Auth;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Provides extension methods for mapping authentication endpoints to the application's routing pipeline.
/// </summary>
/// <remarks>
/// This class exposes endpoints for login, logout (with token revocation), identity introspection, and token
/// diagnostics.
/// </remarks>
static class EndpointMapping
{
	/// <summary>
	/// Maps authentication-related endpoints.
	/// </summary>
	/// <param name="endpoints">
	/// The <see cref="IEndpointRouteBuilder"/> to map endpoints to. This is typically the <c>/api</c> route group
	/// from <c>Program.Pipeline.cs</c>, not the root application.
	/// </param>
	/// <returns>The <paramref name="endpoints"/> builder for method chaining.</returns>
	/// <remarks>
	///     <para>Currently this feature exposes:</para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <c>POST /api/v1/auth/login</c> – accepts a <see cref="V1.LoginRequest"/> and returns a
	///             <see cref="V1.LoginResponse"/> containing a short-lived access token. When cookie transport is
	///             enabled, an <c>HttpOnly</c> cookie is set in addition to the JSON response body.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>POST /api/v1/auth/logout</c> – revokes the current access token and clears the <c>HttpOnly</c>
	///             authentication cookie. Both browser and API clients should call this endpoint to invalidate their
	///             token immediately.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>GET /api/v1/auth/whoami</c> – returns information about the currently authenticated principal
	///             (name, roles, and raw claims). This endpoint is available to any authenticated user.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>GET /api/v1/auth/introspect</c> – returns information about the current authenticated principal
	///             and token, including expiry information.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     The <c>/auth/login</c> endpoint delegates credential validation to <see cref="IUserAuthenticationService"/>.
	///     The current default implementation uses a single built-in bootstrap account; this will be replaced by a
	///     database-backed user store once persistent user management is available.
	///     </para>
	///     <para>
	///     This feature is designed to be mounted on a route group (typically <c>/api/v{version}</c>) and maps its
	///     endpoints relative to that group. The versioned prefix is added by the central route group in
	///     <c>Program.Pipeline.cs</c>.
	///     </para>
	/// </remarks>
	public static IEndpointRouteBuilder MapAuthFeature(this IEndpointRouteBuilder endpoints)
	{
		// Note: This feature maps relative paths. The /api/v{version} prefix is provided by the
		// central route group in Program.Pipeline.cs which also applies global filters
		// like validation. Features should NOT include /api in their paths.
		RouteGroupBuilder group = endpoints
			.MapGroup("/auth")
			.WithTags("Auth");

		// -----------------------------------------------------------------------------
		// AUTH FEATURE – CURRENT STATE
		//
		// This endpoint group hosts all authentication-related routes of the LumaCore
		// API. At the current stage of development the feature provides:
		//
		//   - POST /api/v1/auth/login
		//     Issues a short-lived JWT access token after validating credentials
		//     against the registered IUserAuthenticationService. The current
		//     default implementation uses a built-in bootstrap account; this
		//     will be replaced by a database-backed user store once persistent
		//     user management is available.
		//     When cookie transport is enabled, an HttpOnly cookie is set in
		//     addition to returning the token in the JSON response body.
		//
		//   - POST /api/v1/auth/logout
		//     Revokes the current access token by recording its jti in the
		//     RevokedJwts blacklist table, then clears the HttpOnly authentication
		//     cookie. Both browser and API clients should call this endpoint to
		//     invalidate their token immediately.
		//
		//   - GET /api/v1/auth/whoami
		//     Returns basic information about the currently authenticated principal,
		//     including effective name, roles, and raw claims. This endpoint is useful
		//     for debugging authentication and authorization behavior and is available
		//     to any authenticated user, not only administrators.
		//
		//   - GET /api/v1/auth/introspect
		//     Returns diagnostic information about the current principal and token
		//     (subject, roles, issuer, audience, expiry, remaining lifetime, etc.).
		//     This endpoint is primarily intended for development and debugging of
		//     the authentication flow.
		//
		// The /auth feature currently does NOT provide:
		//
		//   - real user accounts or registration flows
		//   - password hashing or persistent credential storage
		//   - refresh tokens or long-lived sessions
		//   - multi-tenant or fine-grained permission modelling
		//   - integration with external identity providers (OIDC/OAuth2)
		//
		// These capabilities are expected to be added once LumaCore introduces
		// persistent storage and a dedicated user-/identity-management subsystem.
		// -----------------------------------------------------------------------------

		group.MapPost(
				"/login",
				async (
					V1.LoginRequest             request,
					IUserAuthenticationService  userAuthService,
					IJwtTokenFactory            tokenFactory,
					TimeProvider                timeProvider,
					IOptions<JwtOptions>        jwtOptionsAccessor,
					IOptions<AuthCookieOptions> cookieOptionsAccessor,
					HttpContext                 httpContext,
					ILoggerFactory              loggerFactory) =>
				{
					// Create a logger for this feature.
					ILogger logger = loggerFactory.CreateLogger("LumaCore.Auth");

					// Delegate credential validation to the registered user authentication service.
					AuthenticatedUser? user = await userAuthService
						                          .AuthenticateAsync(
							                          request.Username,
							                          request.Password,
							                          httpContext.RequestAborted)
						                          .ConfigureAwait(false);

					if (user is null)
					{
						// Log failed authentication attempt for diagnostics. Do not log the password.
						logger.LogWarning(
							"Authentication failed for login attempt with username '{Username}'",
							request.Username);

						// Deliberately return a generic 401 without details to avoid
						// leaking information about valid user names.
						return Results.Unauthorized();
					}

					// Build identity claims from the authenticated user's properties.
					List<Claim> claims = [new(ClaimTypes.Name, user.Username)];

					foreach (string role in user.Roles)
					{
						claims.Add(new Claim(ClaimTypes.Role, role));
					}

					// Issue a signed JWT that the client can present in subsequent API calls.
					string token = tokenFactory.CreateToken(user.Username, claims);

					// Set an HttpOnly cookie for browser clients so the token is not accessible
					// to JavaScript (XSS mitigation). API clients ignore the cookie and use the
					// token from the JSON response body with the Authorization: Bearer header.
					AuthCookieOptions cookieOptions = cookieOptionsAccessor.Value;
					if (cookieOptions.Enabled)
					{
						JwtOptions jwtOptions = jwtOptionsAccessor.Value;
						httpContext.Response.Cookies.Append(
							cookieOptions.Name,
							token,
							new CookieOptions
							{
								HttpOnly = true,
								Secure = cookieOptions.SecureOnly,
								SameSite = SameSiteMode.Strict, // Hardcoded — CSRF protection for SPA
								Path = cookieOptions.Path,
								Domain = cookieOptions.Domain,
								// RememberMe: persistent cookie with explicit expiry vs session cookie
								// that the browser clears when closed.
								Expires = request.RememberMe
									          ? timeProvider.GetUtcNow()
										          .AddMinutes(jwtOptions.AccessTokenLifetimeMinutes)
									          : null
							});
					}

					// Log successful authentication for auditing purposes.
					logger.LogInformation(
						"User '{Username}' successfully authenticated and issued a JWT access token",
						user.Username);

					// Return the access token to the caller.
					// API clients use this value; browser clients can ignore it (cookie is set above).
					return Results.Ok(new V1.LoginResponse(token));
				})
			.MapToApiVersion(ApiVersions.V1)
			.AllowAnonymous()
			.WithSummary("Authenticates user credentials and issues a JWT access token.")
			.WithDescription(
				"Validates the supplied credentials against the user authentication service " +
				"and, on success, returns a short-lived JWT access token. The current default " +
				"implementation uses a built-in bootstrap account; this will be replaced by a " +
				"database-backed authentication flow once persistent user management is available.")
			.WithName("Login")
			.WithMetadata(
				new SetCookieHeaderMetadata(
					"200",
					"Sets an HttpOnly authentication cookie containing the JWT access token " +
					"when cookie transport is enabled. The cookie uses `SameSite=Strict` for " +
					"CSRF protection and is scoped to the configured API path. Browser clients " +
					"send this cookie automatically on subsequent requests. When `RememberMe` " +
					"is `true`, the cookie persists across browser sessions; otherwise it is a " +
					"session cookie."));

		// Logout endpoint: revokes the current access token and clears the authentication cookie.
		// Token revocation ensures the JWT is immediately rejected even before its natural expiry.
		// API clients using Bearer tokens should also call this endpoint to invalidate their token.
		group.MapPost(
				"/logout",
				async (
					ITokenRevocationService     revocationService,
					IOptions<AuthCookieOptions> cookieOptionsAccessor,
					IOptions<JwtOptions>        jwtOptionsAccessor,
					TimeProvider                timeProvider,
					HttpContext                 httpContext,
					ILoggerFactory              loggerFactory,
					CancellationToken           cancellationToken) =>
				{
					ILogger logger = loggerFactory.CreateLogger("LumaCore.Auth");

					string subject = httpContext.User.FindFirst("sub")?.Value
					                 ?? httpContext.User.Identity?.Name
					                 ?? "(unknown)";

					// Revoke the current access token so it is rejected on subsequent requests.
					string? jti = httpContext.User.FindFirst("jti")?.Value;

					if (jti is not null)
					{
						// Extract the token's natural expiry from the exp claim.
						// If the claim is missing or unparseable, fall back to the configured lifetime
						// from now — this ensures the revocation entry has a reasonable cleanup boundary.
						DateTime expiresAtUtc;
						string? expClaim = httpContext.User.FindFirst("exp")?.Value;

						if (expClaim is not null && long.TryParse(expClaim, out long expUnix))
						{
							expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
						}
						else
						{
							// A missing or unparseable exp claim indicates a bug in the token factory —
							// all JWTs issued by LumaCore must have an expiry. Log a warning so the
							// issue is visible, then fall back to the configured lifetime from now.
							logger.LogWarning(
								"JWT for subject '{Subject}' (jti: {Jti}) has no parseable exp claim; " +
								"using configured lifetime as revocation expiry fallback",
								subject,
								jti);

							JwtOptions jwtOptions = jwtOptionsAccessor.Value;
							expiresAtUtc = timeProvider
								.GetUtcNow()
								.UtcDateTime
								.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes);
						}

						await revocationService
							.RevokeAsync(jti, expiresAtUtc, subject, "Logout", cancellationToken)
							.ConfigureAwait(false);
					}

					AuthCookieOptions cookieOptions = cookieOptionsAccessor.Value;
					if (cookieOptions.Enabled)
					{
						// Delete the cookie by setting matching Path and Domain.
						// The browser removes the cookie when it receives a Set-Cookie
						// with a past expiry date and matching attributes.
						httpContext.Response.Cookies.Delete(
							cookieOptions.Name,
							new CookieOptions
							{
								Path = cookieOptions.Path,
								Domain = cookieOptions.Domain
							});
					}

					logger.LogInformation("User '{Subject}' logged out", subject);

					return Results.NoContent();
				})
			.MapToApiVersion(ApiVersions.V1)
			.RequireAuthorization()
			.Produces(StatusCodes.Status204NoContent)
			.Produces(StatusCodes.Status401Unauthorized)
			.WithSummary("Logs the user out by revoking the access token and clearing the authentication cookie.")
			.WithDescription(
				"Revokes the current JWT access token so it is immediately rejected on subsequent requests, " +
				"and clears the HttpOnly authentication cookie for browser clients. API clients using Bearer " +
				"tokens should also call this endpoint to invalidate their token. Returns 204 No Content on success.")
			.WithName("Logout")
			.WithMetadata(
				new SetCookieHeaderMetadata(
					"204",
					"Clears the authentication cookie by setting it to an expired date with " +
					"matching path and domain attributes."));

		// Exposes basic information about the current authenticated principal.
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
					ReadOnlyCollection<V1.AuthClaimItem> claims = user.Claims
						.Select(c => new V1.AuthClaimItem(c.Type, c.Value))
						.ToList()
						.AsReadOnly();

					// Build and return the whoami response.
					var response = new V1.AuthWhoAmIResponse(
						Name: name,
						Roles: roles,
						Claims: claims);

					return Results.Ok(response);
				})
			.MapToApiVersion(ApiVersions.V1)
			.RequireAuthorization()
			.Produces<V1.AuthWhoAmIResponse>(StatusCodes.Status200OK)
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
					TimeProvider         timeProvider,
					IOptions<JwtOptions> jwtOptionsAccessor,
					ILoggerFactory       loggerFactory) =>
				{
					// Create a logger for this feature.
					ILogger logger = loggerFactory.CreateLogger("LumaCore.Auth");

					// Capture the current time in UTC for lifetime calculations.
					DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

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
					var response = new V1.AuthIntrospectResponse(
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
						"Introspection requested for subject '{Subject}'",
						subject);

					// Return the introspection response.
					return Results.Ok(response);
				})
			.MapToApiVersion(ApiVersions.V1)
			.RequireAuthorization()
			.Produces<V1.AuthIntrospectResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.WithSummary("Introspects the current JWT and returns details about the token.")
			.WithDescription(
				"Provides diagnostic information about the currently used JWT access token, " +
				"including subject, roles, expiry and configured lifetime. This endpoint is " +
				"intended primarily for debugging and support scenarios.")
			.WithName("Introspect");

		return endpoints;
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
