// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text;

using Asp.Versioning;

namespace LumaCore.Api.Features.ApiVersioning;

/// <summary>
/// Provides validation for API versioning configuration at application startup.
/// </summary>
/// <remarks>
/// This class ensures that all versioned API endpoints have an explicit <c>MapToApiVersion()</c> call. Without
/// explicit version mapping, endpoints are implicitly available in all API versions, which can lead to:
/// <list type="bullet">
///     <item>
///         <description>Unintended exposure of endpoints in new API versions.</description>
///     </item>
///     <item>
///         <description>Difficulty tracking which endpoints belong to which version.</description>
///     </item>
///     <item>
///         <description>Inconsistent API surface across versions.</description>
///     </item>
/// </list>
/// By validating at startup, configuration errors are caught immediately rather than manifesting as unexpected
/// runtime behavior.
/// </remarks>
static class ApiVersionValidation
{
	/// <summary>
	/// Validates that all versioned API endpoints have explicit version mappings.
	/// </summary>
	/// <param name="app">The <see cref="WebApplication"/> to validate.</param>
	/// <returns>The <paramref name="app"/> for method chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// One or more versioned endpoints are missing explicit <c>MapToApiVersion()</c> calls.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method should be called after all endpoints have been mapped, typically at the end of
	///     <c>ConfigurePipeline</c> in <c>Program.Pipeline.cs</c>.
	///     </para>
	///     <para>
	///         <b>What is validated:</b>
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>Only endpoints under <c>/api/v{version}</c> are checked.</description>
	///         </item>
	///         <item>
	///             <description>
	///             Each endpoint must have <see cref="ApiVersionMetadata"/> with at least one explicitly mapped version.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///         <b>Example of valid endpoint:</b>
	///     </para>
	///     <code>
	///     group.MapPost("/login", HandleLogin)
	///         .MapToApiVersion(ApiVersions.V1);  // ✅ Explicit mapping
	///     </code>
	///     <para>
	///         <b>Example of invalid endpoint:</b>
	///     </para>
	///     <code>
	///     group.MapPost("/login", HandleLogin);  // ❌ Missing MapToApiVersion
	///     </code>
	/// </remarks>
	public static WebApplication ValidateExplicitApiVersionMappings(this WebApplication app)
	{
		List<string> endpointsMissingVersionMapping = [];

		// ASP.NET Core registers endpoints through two disconnected paths:
		//   1. IEndpointRouteBuilder.DataSources — Minimal API endpoints (MapGet/MapPost etc.)
		//   2. DI-registered EndpointDataSource  — Controller/Razor Pages endpoints
		// The routing middleware sees both at runtime, but neither source includes the other.
		// We merge both and deduplicate so the validator catches misconfigured endpoints
		// regardless of the registration model used.
		IEnumerable<Endpoint> minimalApiEndpoints = ((IEndpointRouteBuilder)app)
			.DataSources
			.SelectMany(ds => ds.Endpoints);

		IEnumerable<Endpoint> controllerEndpoints = app.Services
			.GetRequiredService<EndpointDataSource>()
			.Endpoints;

		IEnumerable<Endpoint> allEndpoints = minimalApiEndpoints
			.Concat(controllerEndpoints)
			.Distinct();

		foreach (Endpoint endpoint in allEndpoints)
		{
			// Only validate RouteEndpoints (endpoints with a route pattern).
			if (endpoint is not RouteEndpoint routeEndpoint)
			{
				continue;
			}

			string? routePattern = routeEndpoint.RoutePattern.RawText;

			// Skip endpoints that are not part of the versioned API surface.
			// This excludes infrastructure endpoints like /health, /swagger, etc.
			if (routePattern is null ||
			    !routePattern.StartsWith(VersionedApiGroup.VersionedRoutePrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			// Get the API version metadata from the endpoint.
			var metadata = endpoint.Metadata.GetMetadata<ApiVersionMetadata>();

			// If there's no metadata at all, the endpoint isn't properly configured.
			if (metadata is null)
			{
				endpointsMissingVersionMapping.Add(
					$"{routeEndpoint.DisplayName} ({routePattern}) - No ApiVersionMetadata");
				continue;
			}

			// Check if the endpoint has explicit version mappings.
			// MappedVersions contains only versions set via MapToApiVersion().
			// If empty, the endpoint inherits all versions from the ApiVersionSet,
			// which is the behavior we want to prevent.
			ApiVersionModel model = metadata.Map(ApiVersionMapping.Explicit);

			if (model.DeclaredApiVersions.Count == 0)
			{
				endpointsMissingVersionMapping.Add($"{routeEndpoint.DisplayName} ({routePattern})");
			}
		}

		if (endpointsMissingVersionMapping.Count > 0)
		{
			var message = new StringBuilder();
			message.AppendLine(
				"API version validation failed. The following endpoints are missing explicit MapToApiVersion() calls:");
			message.AppendLine();

			foreach (string endpoint in endpointsMissingVersionMapping)
			{
				message.AppendLine($"  • {endpoint}");
			}

			message.AppendLine();
			message.AppendLine("Every versioned API endpoint must explicitly declare its API version(s):");
			message.AppendLine();
			message.AppendLine("  group.MapPost(\"/items\", HandleCreateItem)");
			message.AppendLine("      .MapToApiVersion(ApiVersions.V1);");
			message.AppendLine();
			message.AppendLine("This prevents endpoints from being unintentionally exposed in all API versions.");

			throw new InvalidOperationException(message.ToString());
		}

		return app;
	}
}
