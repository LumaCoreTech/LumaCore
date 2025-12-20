// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Claims;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using V1 = LumaCore.Api.Contracts.V1.Admin;

namespace LumaCore.Api.Features.Admin;

/// <summary>
/// Provides extension methods for mapping administrative endpoints to the application's routing pipeline.
/// </summary>
/// <remarks>
///     <para>
///     This class is part of the Admin feature and exposes endpoints for
///     operational monitoring and system status. All endpoints require
///     the <c>admin</c> role.
///     </para>
/// </remarks>
public static class EndpointMapping
{
	/// <summary>
	/// Maps the admin endpoint group (for example <c>/api/v1/admin/*</c>) into the application's
	/// endpoint routing table.
	/// </summary>
	/// <param name="endpoints">
	/// The <see cref="IEndpointRouteBuilder"/> to map endpoints to. This is typically the
	/// <c>/api/v{version}</c> route group from <c>Program.Pipeline.cs</c>, not the root application.
	/// </param>
	/// <returns>The <paramref name="endpoints"/> builder for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method groups all admin endpoints under the <c>/admin</c> path prefix (relative
	///     to the parent route group) and enforces that a valid, authenticated user in the
	///     <c>admin</c> role is present by applying an authorization policy via
	///     <c>RequireAuthorization()</c>.
	///     </para>
	///     <para>
	///     This feature is designed to be mounted on a route group (typically <c>/api/v{version}</c>) and
	///     maps its endpoints relative to that group. The versioned prefix is added by the
	///     central route group in <c>Program.Pipeline.cs</c>.
	///     </para>
	///     <para>
	///     The method is intended to be called once during startup, typically from the central
	///     pipeline configuration in <c>Program.Pipeline.cs</c>.
	///     </para>
	/// </remarks>
	public static IEndpointRouteBuilder MapAdminFeature(this IEndpointRouteBuilder endpoints)
	{
		// Note: This feature maps relative paths. The /api/v{version} prefix is provided by the
		// central route group in Program.Pipeline.cs which also applies global filters
		// like validation. Features should NOT include /api in their paths.
		//
		// Group for admin / internal endpoints that always require an authenticated user
		// in the 'admin' role. The RequireAuthorization(...) call applies an authorization
		// policy that enforces this role using the JWT-based authentication configured
		// by the AuthFeature.
		RouteGroupBuilder admin = endpoints.MapGroup("/admin")
			.RequireAuthorization(new AuthorizeAttribute { Roles = "admin" })
			.WithTags("Admin");

		// -----------------------------------------------------------------------------
		// ADMIN FEATURE – CURRENT STATE
		//
		// This endpoint group exposes administrative and operational endpoints for a
		// running LumaCore instance. All routes require a valid, authenticated user
		// in the 'admin' role and are intended for operational and maintenance
		// scenarios.
		//
		// At the current stage of development the feature provides:
		//
		//   - GET /api/v1/admin/status
		//     Returns a small, non-sensitive snapshot of the API status, including
		//     environment, API version, machine name, server time, and high-level JWT
		//     configuration information. Secrets such as the signing key are never
		//     exposed; only a masked representation and basic configuration flags are
		//     returned.
		//
		// The Admin feature currently does NOT perform any write operations or
		// destructive actions. It does not yet include:
		//
		//   - model or configuration changes
		//   - user or role management
		//   - system restart, shutdown, or maintenance operations
		//
		// These capabilities can be added here in the future, keeping all operational
		// and potentially high-impact actions clearly grouped under the /api/v{version}/admin
		// prefix.
		// -----------------------------------------------------------------------------

		// ---------------------------------------------------------------------
		// /api/v1/admin/status
		// ---------------------------------------------------------------------
		// Exposes a small set of non-sensitive status information about the
		// running LumaCore instance. Sensitive values such as the JWT signing
		// key are intentionally masked to avoid leaking secrets via HTTP.
		admin.MapGet(
				"/status",
				(
					ClaimsPrincipal      user,
					IConfiguration       config,
					IOptions<JwtOptions> jwtOptionsAccessor,
					ILoggerFactory       loggerFactory) =>
				{
					// Create a logger for this feature.
					ILogger logger = loggerFactory.CreateLogger("LumaCore.Admin");

					// Log who requested the admin status.
					// Use the 'sub' claim if present; otherwise fall back to the Identity name or a generic placeholder.
					string subject = user.FindFirst("sub")?.Value
					                 ?? user.Identity?.Name
					                 ?? "(unknown)";

					// Log the admin status request.
					logger.LogInformation(
						"Admin status requested by subject '{Subject}'.",
						subject);

					// Gather status information.
					string? environment = config["LumaCore:Environment"];
					string? apiVersion = config["LumaCore:ApiVersion"];

					JwtOptions jwtOptions = jwtOptionsAccessor.Value;

					bool jwtConfigured =
						!string.IsNullOrWhiteSpace(jwtOptions.Issuer) &&
						!string.IsNullOrWhiteSpace(jwtOptions.Audience) &&
						!string.IsNullOrWhiteSpace(jwtOptions.SigningKey);

					string signingKey = jwtOptions.SigningKey;

					// Never expose the raw signing key. Show only a masked indicator that a key
					// is present and its length, which is enough for diagnostics without leaking
					// the secret material.
					string? jwtSigningKeyMasked = string.IsNullOrEmpty(signingKey)
						                              ? null
						                              : $"*** (length {signingKey.Length})";

					// Build the JWT status info.
					var jwtStatus = new V1.AdminJwtStatusInfo(
						Configured: jwtConfigured,
						Issuer: jwtOptions.Issuer,
						Audience: jwtOptions.Audience,
						SigningKey: jwtSigningKeyMasked,
						AccessTokenLifetimeMinutes: jwtOptions.AccessTokenLifetimeMinutes);

					// Build the overall admin status response.
					var response = new V1.AdminStatusResponse(
						Environment: environment,
						ApiVersion: apiVersion,
						MachineName: Environment.MachineName,
						UtcNow: DateTime.UtcNow,
						Jwt: jwtStatus);

					return Results.Ok(response);
				})
			.MapToApiVersion(ApiVersions.V1)
			.Produces<V1.AdminStatusResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.WithSummary("Returns high-level status information about the API.")
			.WithDescription(
				"Returns a small, non-sensitive snapshot of the running LumaCore instance, " +
				"including environment, API version, machine name, server time and JWT " +
				"configuration status. Secrets such as the signing key are never exposed; " +
				"only a masked representation of the key and basic configuration flags are returned.")
			.WithName("AdminStatus");

		return endpoints;
	}
}
