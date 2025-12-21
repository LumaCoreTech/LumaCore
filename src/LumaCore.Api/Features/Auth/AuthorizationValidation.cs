// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text;

using Microsoft.AspNetCore.Authorization;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Provides validation for authorization configuration at application startup.
/// </summary>
/// <remarks>
/// This class ensures that all versioned API endpoints have an explicit authorization declaration — either
/// <c>RequireAuthorization()</c> or <c>AllowAnonymous()</c>. Without explicit declaration, endpoints rely on
/// implicit defaults, which can lead to:
/// <list type="bullet">
///     <item>
///         <description>Accidental exposure of unprotected endpoints.</description>
///     </item>
///     <item>
///         <description>Security gaps when global policies change.</description>
///     </item>
///     <item>
///         <description>Unclear security posture during code reviews.</description>
///     </item>
/// </list>
/// By validating at startup, configuration errors are caught immediately rather than manifesting as security
/// vulnerabilities in production.
/// </remarks>
static class AuthorizationValidation
{
	/// <summary>
	/// The route prefix that identifies versioned API endpoints.
	/// </summary>
	/// <remarks>
	/// Only endpoints whose route pattern starts with this prefix are validated.
	/// Infrastructure endpoints (e.g., <c>/health</c>) are intentionally excluded.
	/// </remarks>
	private const string VersionedApiPrefix = "/api/v";

	/// <summary>
	/// Validates that all versioned API endpoints have explicit authorization declarations.
	/// </summary>
	/// <param name="app">The <see cref="WebApplication"/> to validate.</param>
	/// <returns>The <paramref name="app"/> for method chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when one or more versioned endpoints are missing explicit <c>RequireAuthorization()</c> or
	/// <c>AllowAnonymous()</c> calls.
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
	///             Each endpoint must have either <see cref="IAuthorizeData"/> metadata (from
	///             <c>RequireAuthorization()</c>) or <see cref="IAllowAnonymous"/> metadata (from
	///             <c>AllowAnonymous()</c>).
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///         <b>Example of valid endpoints:</b>
	///     </para>
	///     <code>
	///     group.MapPost("/login", HandleLogin)
	///         .AllowAnonymous();  // ✅ Explicit: no auth required
	///     
	///     group.MapGet("/profile", HandleGetProfile)
	///         .RequireAuthorization();  // ✅ Explicit: auth required
	///     </code>
	///     <para>
	///         <b>Example of invalid endpoint:</b>
	///     </para>
	///     <code>
	///     group.MapGet("/data", HandleGetData);  // ❌ Missing authorization declaration
	///     </code>
	/// </remarks>
	public static WebApplication ValidateExplicitAuthorizationPolicies(this WebApplication app)
	{
		List<string> endpointsMissingAuthorization = [];

		// Iterate over all registered endpoints in the application.
		foreach (Endpoint endpoint in app.Services
			         .GetRequiredService<EndpointDataSource>()
			         .Endpoints)
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
			    !routePattern.StartsWith(VersionedApiPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			// Check for explicit authorization metadata.
			// RequireAuthorization() adds IAuthorizeData to metadata.
			// AllowAnonymous() adds IAllowAnonymous to metadata.
			bool hasAuthorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null;
			bool hasAllowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

			if (!hasAuthorizeData && !hasAllowAnonymous)
			{
				endpointsMissingAuthorization.Add($"{routeEndpoint.DisplayName} ({routePattern})");
			}
		}

		if (endpointsMissingAuthorization.Count > 0)
		{
			var message = new StringBuilder();
			message.AppendLine(
				"Authorization validation failed. The following endpoints are missing explicit authorization declarations:");
			message.AppendLine();

			foreach (string endpoint in endpointsMissingAuthorization)
			{
				message.AppendLine($"  • {endpoint}");
			}

			message.AppendLine();
			message.AppendLine("Every versioned API endpoint must explicitly declare its authorization requirement:");
			message.AppendLine();
			message.AppendLine("  // For protected endpoints:");
			message.AppendLine("  group.MapGet(\"/profile\", HandleGetProfile)");
			message.AppendLine("      .RequireAuthorization();");
			message.AppendLine();
			message.AppendLine("  // For public endpoints:");
			message.AppendLine("  group.MapPost(\"/login\", HandleLogin)");
			message.AppendLine("      .AllowAnonymous();");
			message.AppendLine();
			message.AppendLine("This prevents accidental exposure of unprotected endpoints.");

			throw new InvalidOperationException(message.ToString());
		}

		return app;
	}
}
