// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Provides registration and endpoint wiring for the authentication subsystem of LumaCore.
/// </summary>
public static class AuthFeature
{
	/// <summary>
	/// Registers JWT authentication, authorization, options binding, and supporting services.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This method wires up the complete authentication stack for the LumaCore HTTP API:
	///     it binds <see cref="JwtOptions"/>, configures the JWT bearer handler, and registers
	///     authorization services and the <see cref="IJwtTokenFactory"/>.
	///     </para>
	///     <para>
	///     The method is intended to be called once during application startup from the main
	///     <c>Program</c> configuration.
	///     </para>
	/// </remarks>
	/// <param name="builder">The application builder.</param>
	/// <returns>The modified application builder.</returns>
	public static WebApplicationBuilder AddAuthFeature(this WebApplicationBuilder builder)
	{
		// Bind and validate JWT options at startup so misconfiguration fails fast.
		builder.Services
			.AddOptions<JwtOptions>()
			.Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Read raw values for token validation configuration. These are used both for
		// issuing tokens and for validating incoming tokens in the JWT bearer middleware.
		IConfigurationSection jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);

		// Read required JWT configuration value 'Issuer' or throw an exception if any are missing.
		string issuer = jwtSection["Issuer"]
		                ?? throw new InvalidOperationException(
			                "Missing configuration value 'Jwt:Issuer'. " +
			                "Configure it via appsettings (\"Jwt\": { \"Issuer\": \"...\" }) " +
			                "or environment variable 'Jwt__Issuer'.");

		// Read required JWT configuration value 'Audience' or throw an exception if any are missing.
		string audience = jwtSection["Audience"]
		                  ?? throw new InvalidOperationException(
			                  "Missing configuration value 'Jwt:Audience'. " +
			                  "Configure it via appsettings (\"Jwt\": { \"Audience\": \"...\" }) " +
			                  "or environment variable 'Jwt__Audience'.");

		// Read required JWT configuration value 'SigningKey' or throw an exception if any are missing.
		string signingKey = jwtSection["SigningKey"]
		                    ?? throw new InvalidOperationException(
			                    "Missing configuration value 'Jwt:SigningKey'. " +
			                    "Configure it via appsettings (\"Jwt\": { \"SigningKey\": \"...\" }) " +
			                    "or environment variable 'Jwt__SigningKey'.");

		byte[] signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);

		// Configure JWT bearer authentication for incoming requests.
		builder.Services
			.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
			.AddJwtBearer(options =>
			{
				options.TokenValidationParameters = new TokenValidationParameters
				{
					// Validate the JWT issuer (iss claim).
					ValidateIssuer = true,
					ValidIssuer = issuer,

					// Validate the JWT audience (aud claim).
					ValidateAudience = true,
					ValidAudience = audience,

					// Validate the token signature.
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),

					// Validate the token expiry (exp claim) and reject expired tokens.
					ValidateLifetime = true,

					// Allow a small clock skew to account for minor time differences
					// between clients and the server.
					ClockSkew = TimeSpan.FromSeconds(30)
				};
			});

		// Register authorization and the token factory used by the login endpoint.
		builder.Services.AddAuthorization();
		builder.Services.AddSingleton<IJwtTokenFactory, JwtTokenFactory>();

		return builder;
	}

	/// <summary>
	/// Maps authentication-related endpoints.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Currently this feature exposes:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <c>POST /auth/login</c> – accepts a <see cref="LoginRequest"/> and returns a
	///             <see cref="LoginResponse"/> containing a short-lived access token.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>GET /auth/whoami</c> – returns information about the currently authenticated
	///             principal (name, roles, and raw claims). This endpoint is available to any
	///             authenticated user.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>GET /auth/introspect</c> – returns information about the current authenticated
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
	/// <param name="app">The application.</param>
	/// <returns>The modified application.</returns>
	public static WebApplication MapAuthFeature(this WebApplication app)
	{
		RouteGroupBuilder group = app.MapGroup("/auth")
			.WithTags("Auth");

		// -----------------------------------------------------------------------------
		// AUTH FEATURE – CURRENT STATE
		//
		// This endpoint group hosts all authentication-related routes of the LumaCore
		// API. At the current stage of development the feature provides:
		//
		//   - POST /auth/login
		//     Issues a short-lived JWT access token for the built-in administrator
		//     account. This is a temporary bootstrap mechanism until LumaCore has a
		//     persistent user store (for example a database-backed authentication
		//     system with proper password hashing and user management).
		//
		//   - GET /auth/whoami
		//     Returns basic information about the currently authenticated principal,
		//     including effective name, roles, and raw claims. This endpoint is useful
		//     for debugging authentication and authorization behavior and is available
		//     to any authenticated user, not only administrators.
		//
		//   - GET /auth/introspect
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
				(LoginRequest request, IJwtTokenFactory tokenFactory) =>
				{
					// Temporary bootstrap authentication: single hard-coded administrator account.
					if (!IsValidAdmin(request.Username, request.Password))
					{
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
					string name = user.Identity?.Name ?? "(anonymous)";

					ReadOnlyCollection<string> roles = user
						.FindAll(ClaimTypes.Role)
						.Select(r => r.Value)
						.ToList()
						.AsReadOnly();

					ReadOnlyCollection<AuthClaimItem> claims = user.Claims
						.Select(c => new AuthClaimItem(c.Type, c.Value))
						.ToList()
						.AsReadOnly();

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
				(ClaimsPrincipal user, IOptions<JwtOptions> jwtOptionsAccessor) =>
				{
					DateTime utcNow = DateTime.UtcNow;

					JwtOptions jwtOptions = jwtOptionsAccessor.Value;

					string subject = user.FindFirst("sub")?.Value
					                 ?? user.Identity?.Name
					                 ?? "(unknown)";

					string? name = user.Identity?.Name;

					// Extract roles from claims.
					ReadOnlyCollection<string> roles = user.FindAll(ClaimTypes.Role)
						.Select(r => r.Value)
						.ToList()
						.AsReadOnly();

					// Extract token timing claims (if present).
					string? nbfClaim = user.FindFirst("nbf")?.Value;
					string? expClaim = user.FindFirst("exp")?.Value;

					// Parse timing claims into UTC DateTime values.
					DateTime? notBeforeUtc = TryParseUnixTimeSeconds(nbfClaim);
					DateTime? expiresUtc = TryParseUnixTimeSeconds(expClaim);

					// Calculate remaining lifetime (if expiry is known).
					TimeSpan? expiresIn =
						expiresUtc is null
							? null
							: expiresUtc.Value - utcNow;

					// Extract other standard JWT claims.
					string? jwtId = user.FindFirst("jti")?.Value;
					string? issuer = user.FindFirst("iss")?.Value;
					string? audience = user.FindFirst("aud")?.Value;

					// Build and return the introspection response.
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

					return Results.Ok(response);
				})
			.RequireAuthorization()
			.Produces<AuthIntrospectResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.WithSummary("Introspects the current JWT and principal.")
			.WithDescription(
				"Returns diagnostic information about the current authenticated principal and " +
				"its JWT token, including issuer, audience, not-before time, expiry and remaining " +
				"lifetime. Primarily intended for development and troubleshooting rather than " +
				"normal client-side UI logic.")
			.WithName("AuthIntrospect");

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
	private static bool IsValidAdmin(string username, string password)
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
