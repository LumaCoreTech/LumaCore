// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Admin;

/// <summary>
/// Provides admin endpoints for inspecting and monitoring the running LumaCore instance.
/// </summary>
/// <remarks>
///     <para>
///     All endpoints mapped by this feature require authentication and are intended for
///     administrative or operational use only. The feature does not perform authentication
///     itself; it relies on the JWT-based authentication configured by <c>AuthFeature</c>.
///     </para>
///     <para>
///     The <c>/admin/status</c> endpoint returns a small set of non-sensitive runtime and
///     configuration details that are useful for diagnostics.
///     </para>
/// </remarks>
public static class AdminFeature
{
	/// <summary>
	/// Maps the admin endpoint group (for example <c>/admin/*</c>) into the application's
	/// endpoint routing table.
	/// </summary>
	/// <param name="app">The application used to define the HTTP pipeline.</param>
	/// <returns>
	/// The modified <see cref="WebApplication"/> instance to enable fluent configuration.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method groups all admin endpoints under the <c>/admin</c> path prefix and
	///     enforces that a valid, authenticated user is present by applying the application's
	///     default authorization policy via <c>RequireAuthorization()</c>.
	///     </para>
	///     <para>
	///     The method is intended to be called once during startup, typically from the central
	///     pipeline configuration in <c>Program.Pipeline.cs</c>.
	///     </para>
	/// </remarks>
	public static WebApplication MapAdminFeature(this WebApplication app)
	{
		// Group for admin / internal endpoints that always require authentication.
		// The RequireAuthorization() call applies the default authorization policy
		// (which, in this application, is backed by JWT bearer authentication).
		RouteGroupBuilder admin = app.MapGroup("/admin")
			.RequireAuthorization()
			.WithTags("Admin");

		// -----------------------------------------------------------------------------
		// ADMIN FEATURE – CURRENT STATE
		//
		// This endpoint group exposes administrative and operational endpoints for a
		// running LumaCore instance. All routes require a valid, authenticated user
		// and are intended to be restricted to administrative roles only once role
		// policies are in place.
		//
		// At the current stage of development the feature provides:
		//
		//   - GET /admin/status
		//     Returns a small, non-sensitive snapshot of the API status, including
		//     environment, API version, machine name, server time, and high-level JWT
		//     configuration information. Secrets such as the signing key are never
		//     exposed; only a masked representation and basic flags are returned.
		//
		// The /admin feature currently does NOT perform any write operations or
		// destructive actions. It does not yet include:
		//
		//   - model or configuration changes
		//   - user or role management
		//   - system restart, shutdown, or maintenance operations
		//
		// These capabilities can be added here in the future, keeping all operational
		// and potentially high-impact actions clearly grouped under the /admin prefix.
		// -----------------------------------------------------------------------------

		// ---------------------------------------------------------------------
		// /admin/status
		// ---------------------------------------------------------------------
		// Exposes a small set of non-sensitive status information about the
		// running LumaCore instance. Sensitive values such as the JWT signing
		// key are intentionally masked to avoid leaking secrets via HTTP.
		admin.MapGet(
				"/status",
				(IConfiguration config) =>
				{
					string? environment = config["LumaCore:Environment"];
					string? apiVersion = config["LumaCore:ApiVersion"];

					string? jwtIssuer = config["Jwt:Issuer"];
					string? jwtAudience = config["Jwt:Audience"];
					string? jwtSigningKey = config["Jwt:SigningKey"];
					string? jwtLifetime = config["Jwt:AccessTokenLifetimeMinutes"];

					bool jwtConfigured =
						!string.IsNullOrWhiteSpace(jwtIssuer) &&
						!string.IsNullOrWhiteSpace(jwtAudience) &&
						!string.IsNullOrWhiteSpace(jwtSigningKey);

					// Never expose the raw signing key. Show only a masked indicator
					// that a key is present and its length, which is enough for
					// diagnostics without leaking the secret.
					string? jwtSigningKeyMasked = jwtSigningKey is null
						                              ? null
						                              : $"*** (length {jwtSigningKey.Length})";

					int? lifetimeMinutes = null;
					if (!string.IsNullOrWhiteSpace(jwtLifetime) &&
					    int.TryParse(jwtLifetime, out int parsedLifetime))
					{
						lifetimeMinutes = parsedLifetime;
					}

					var jwtStatus = new AdminJwtStatusInfo(
						Configured: jwtConfigured,
						Issuer: jwtIssuer,
						Audience: jwtAudience,
						SigningKey: jwtSigningKeyMasked,
						AccessTokenLifetimeMinutes: lifetimeMinutes);

					var response = new AdminStatusResponse(
						Environment: environment,
						ApiVersion: apiVersion,
						MachineName: Environment.MachineName,
						UtcNow: DateTime.UtcNow,
						Jwt: jwtStatus);

					return Results.Ok(response);
				})
			.Produces<AdminStatusResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.WithSummary("Returns high-level status information about the API.")
			.WithDescription(
				"Returns a small, non-sensitive snapshot of the running LumaCore instance, " +
				"including environment, API version, machine name, server time and JWT " +
				"configuration status. Secrets such as the signing key are never exposed; " +
				"only a masked representation of the key and basic configuration flags are returned.")
			.WithName("AdminStatus");

		return app;
	}
}
