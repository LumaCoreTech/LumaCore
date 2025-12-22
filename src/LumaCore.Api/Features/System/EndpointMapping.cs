// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Reflection;

using LumaCore.Api.Features.ApiVersioning;

using V1 = LumaCore.Api.Contracts.V1.System;

namespace LumaCore.Api.Features.System;

/// <summary>
/// Provides extension methods for mapping System feature endpoints.
/// </summary>
/// <remarks>
///     <para>
///     The System feature exposes diagnostic endpoints for monitoring and troubleshooting:
///     <list type="bullet">
///         <item><c>GET /api/v1/system/info</c> – Runtime information (environment, version, machine)</item>
///         <item><c>GET /api/v1/system/configuration</c> – All configuration sections with secrets masked</item>
///         <item><c>GET /api/v1/system/configuration/{section}</c> – A specific configuration section</item>
///         <item><c>GET /api/v1/system/configuration/{section}/{key}</c> – A specific configuration value</item>
///     </list>
///     </para>
///     <para>
///     All endpoints require authorization to prevent information disclosure to unauthenticated users.
///     </para>
/// </remarks>
static class EndpointMapping
{
	/// <summary>
	/// Maps the System feature endpoints to the versioned API group.
	/// </summary>
	/// <param name="endpoints">The <see cref="RouteGroupBuilder"/> for the versioned API.</param>
	/// <returns>The <paramref name="endpoints"/> for method chaining.</returns>
	public static RouteGroupBuilder MapSystemFeature(this RouteGroupBuilder endpoints)
	{
		RouteGroupBuilder group = endpoints
			.MapGroup("/system")
			.WithTags("System");

		// ────────────────────────────────────────────────────────────────────────
		// GET /api/v1/system/info
		// Returns runtime information about the LumaCore instance.
		// ────────────────────────────────────────────────────────────────────────
		group.MapGet(
				"/info",
				(IWebHostEnvironment env) =>
				{
					// Read version information from entry assembly.
					// MinVer populates these attributes based on Git tags during build.
					Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

					// FileVersion: Major.Minor.Patch.0 (e.g., "1.0.0.0")
					string? version = assembly
						.GetCustomAttribute<AssemblyFileVersionAttribute>()
						?.Version;

					// InformationalVersion: Full SemVer with prerelease and Git SHA
					// (e.g., "1.0.0-ci.42+a1b2c3d4")
					string? infoVersion = assembly
						.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
						?.InformationalVersion;

					var response = new V1.SystemInfoResponse(
						Environment: env.EnvironmentName,
						Version: version,
						InformationalVersion: infoVersion,
						MachineName: Environment.MachineName,
						UtcNow: DateTime.UtcNow);

					return Results.Ok(response);
				})
			.MapToApiVersion(ApiVersions.V1)
			.RequireAuthorization(policy => policy.RequireRole("admin"))
			.Produces<V1.SystemInfoResponse>(StatusCodes.Status200OK)
			.WithSummary("Returns runtime information about the LumaCore instance.")
			.WithDescription(
				"Returns a snapshot of the running LumaCore instance including environment, " +
				"version, machine name, and current server time. Useful for verifying " +
				"which instance is being accessed and for time synchronization debugging. " +
				"Requires the 'admin' role.")
			.WithName("SystemInfo");

		// ────────────────────────────────────────────────────────────────────────
		// GET /api/v1/system/configuration
		// Returns all configuration sections with secrets masked.
		// ────────────────────────────────────────────────────────────────────────
		group.MapGet(
				"/configuration",
				(OptionsRegistry registry) =>
				{
					IDictionary<string, IDictionary<string, object?>> allConfig = registry.GetAllSanitized();
					return Results.Ok(allConfig);
				})
			.MapToApiVersion(ApiVersions.V1)
			.RequireAuthorization(policy => policy.RequireRole("admin"))
			.Produces<IDictionary<string, IDictionary<string, object?>>>(StatusCodes.Status200OK)
			.WithSummary("Returns all configuration sections with secrets masked.")
			.WithDescription(
				"Returns all registered configuration options grouped by section name. " +
				"Sensitive values are automatically masked (e.g., 'SigningKey' becomes " +
				"'*** (length 32)'). Requires the 'admin' role.")
			.WithName("SystemConfiguration");

		// ────────────────────────────────────────────────────────────────────────
		// GET /api/v1/system/configuration/{section}
		// Returns a specific configuration section with secrets masked.
		// ────────────────────────────────────────────────────────────────────────
		group.MapGet(
				"/configuration/{section}",
				(OptionsRegistry registry, string section) =>
				{
					IDictionary<string, object?>? sectionConfig = registry.GetSanitized(section);

					return sectionConfig is null
						       ? Results.NotFound(new { error = $"Configuration section '{section}' not found." })
						       : Results.Ok(sectionConfig);
				})
			.MapToApiVersion(ApiVersions.V1)
			.RequireAuthorization(policy => policy.RequireRole("admin"))
			.Produces<IDictionary<string, object?>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status404NotFound)
			.WithSummary("Returns a specific configuration section with secrets masked.")
			.WithDescription(
				"Returns all values within a configuration section. " +
				"Sensitive values are automatically masked. Requires the 'admin' role.")
			.WithName("SystemConfigurationSection");

		// ────────────────────────────────────────────────────────────────────────
		// GET /api/v1/system/configuration/{section}/{key}
		// Returns a specific configuration value. Useful for scripting.
		// ────────────────────────────────────────────────────────────────────────
		group.MapGet(
				"/configuration/{section}/{key}",
				(OptionsRegistry registry, string section, string key) =>
				{
					IDictionary<string, object?>? sectionConfig = registry.GetSanitized(section);

					if (sectionConfig is null)
					{
						return Results.NotFound(new { error = $"Configuration section '{section}' not found." });
					}

					// Perform case-insensitive key lookup to match appsettings.json behavior.
					string? matchingKey = sectionConfig.Keys
						.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));

					if (matchingKey is null)
					{
						return Results.NotFound(new { error = $"Key '{key}' not found in section '{section}'." });
					}

					return Results.Ok(sectionConfig[matchingKey]);
				})
			.MapToApiVersion(ApiVersions.V1)
			.RequireAuthorization(policy => policy.RequireRole("admin"))
			.Produces<object>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status404NotFound)
			.WithSummary("Returns a specific configuration value with secrets masked.")
			.WithDescription(
				"Returns a single configuration value by section and key. " +
				"Sensitive values are returned in masked form. Requires the 'admin' role.")
			.WithName("SystemConfigurationValue");

		return group;
	}
}
