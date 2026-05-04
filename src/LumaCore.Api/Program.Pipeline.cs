// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Asp.Versioning.ApiExplorer;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.Auth;
using LumaCore.Api.Features.Cors;
using LumaCore.Api.Features.Data;
using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Features.Health;
using LumaCore.Api.Features.HttpsRedirection;
using LumaCore.Api.Features.ProxyHeaders;
using LumaCore.Api.Features.SecurityHeaders;
using LumaCore.Api.Features.System;
using LumaCore.Api.Features.User;

using Serilog;

using Swashbuckle.AspNetCore.SwaggerUI;

namespace LumaCore.Api;

public static partial class Program
{
	/// <summary>
	/// Configures middleware, developer utilities and endpoint mapping.
	/// </summary>
	/// <param name="app">The <see cref="WebApplication"/> used to define the HTTP pipeline.</param>
	private static void ConfigurePipeline(WebApplication app)
	{
		// Process proxy-related headers (e.g. X-Forwarded-For, X-Forwarded-Proto) first.
		// This must be the earliest middleware to ensure all subsequent middleware sees
		// the correct client IP, scheme, and host when running behind a reverse proxy.
		// Without this, HTTPS redirection would see HTTP and cause redirect loops.
		app.UseProxyHeadersFeature();

		// Global exception handler that converts unhandled exceptions into RFC 7807
		// ProblemDetails responses. This must be early in the pipeline to catch
		// exceptions from all subsequent middleware. In Development, the developer
		// exception page takes precedence (added below).
		if (!app.Environment.IsDevelopment())
		{
			app.UseExceptionHandler();
		}

		// Convert non-exception error status codes (e.g. 404 Not Found, 401 Unauthorized)
		// into ProblemDetails responses with LumaCore-specific error type URNs.
		// Only applies to /api/* paths; Blazor SPA routes are unaffected.
		app.UseErrorHandlingFeature();

		// Reject API requests if the database initialization failed or is still in progress.
		// Health endpoints are excluded so monitoring systems can still query application status.
		// Returns 503 Service Unavailable with ProblemDetails for affected requests.
		app.UseDatabaseReadinessCheck();

		// Enforce HTTPS by redirecting HTTP requests to their HTTPS counterparts.
		// Must come AFTER proxy headers so the scheme is correctly detected.
		app.UseHttpsRedirectionFeature();

		// Add HTTP security headers (HSTS, X-Frame-Options, CSP, etc.) to all responses.
		// This must be early in the pipeline to ensure headers are set before any response.
		app.UseSecurityHeadersFeature();

		// Add structured request logging early in the pipeline to capture all requests,
		// including 404s on static assets. This writes a single summary log entry per
		// request including method, path, status code and elapsed time.
		app.UseSerilogRequestLogging(options =>
		{
			options.MessageTemplate =
				"HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
		});

		if (app.Environment.IsDevelopment())
		{
			// Show detailed exception information and a developer-friendly error page
			// instead of the generic error handler. This should only be enabled in
			// development to avoid leaking implementation details.
			app.UseDeveloperExceptionPage();
		}

		// ┌─────────────────────────────────────────────────────────────────────────────┐
		// │ OpenAPI Document Endpoints                                                   │
		// ├─────────────────────────────────────────────────────────────────────────────┤
		// │ Exposes OpenAPI documents at /openapi/{version}.json in Development mode.   │
		// │ Each API version has its own document (e.g., /openapi/v1.json).             │
		// │                                                                             │
		// │ BUILD-TIME GENERATION:                                                      │
		// │ Use ./build.net/OpenApi/generate-openapi-json.ps1 to generate JSON specs.   │
		// │ The script invokes MSBuild with Microsoft.Extensions.ApiDescription.Server  │
		// │ which launches the app with GetDocument.Insider as entry assembly.          │
		// │ Program.cs detects this and forces ASPNETCORE_ENVIRONMENT=Development       │
		// │ so the endpoint is available (Production requires secrets).                 │
		// │                                                                             │
		// │ In Production, the /openapi endpoints are NOT exposed.                      │
		// │ See: https://learn.microsoft.com/aspnet/core/fundamentals/openapi           │
		// └─────────────────────────────────────────────────────────────────────────────┘
		if (app.Environment.IsDevelopment())
		{
			app.MapOpenApi();
		}

		if (app.Environment.IsDevelopment())
		{
			// Expose Swagger UI as a developer-facing API browser that allows
			// interactive exploration and testing of the LumaCore endpoints.
			//
			// The UI displays a dropdown to switch between API versions. Each version
			// has its own OpenAPI document generated by the OpenApi feature.
			app.UseSwaggerUI(options =>
			{
				// Get all available API versions from the versioning system.
				// This ensures the Swagger UI dropdown stays in sync with registered versions.
				var versionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

				foreach (ApiVersionDescription description in versionProvider.ApiVersionDescriptions)
				{
					// Build endpoint URL and display name for each version.
					// GroupName matches the document name (e.g., "v1", "v2").
					string url = $"/openapi/{description.GroupName}.json";
					string name = $"LumaCore API {description.GroupName.ToUpperInvariant()}";

					// Mark deprecated versions in the dropdown.
					if (description.IsDeprecated)
					{
						name += " (deprecated)";
					}

					options.SwaggerEndpoint(url, name);
				}

				options.RoutePrefix = "swagger";

				// Provide a clearer title in the browser tab and Swagger UI header.
				options.DocumentTitle = "LumaCore API Explorer";

				// Show server-side execution time for each request in the UI.
				options.DisplayRequestDuration();

				// Collapse all operations by default to keep the UI manageable as
				// the number of endpoints grows. Users can expand sections as needed.
				options.DocExpansion(DocExpansion.None);
			});
		}

		// Apply CORS policy based on configuration to control cross-origin requests.
		// This must be done BEFORE UseRouting() to properly handle preflight requests.
		// See https://docs.microsoft.com/aspnet/core/security/cors
		app.UseCorsFeature();

		// Enable static file handling and Blazor framework files for the SPA.
		// Placed before auth so assets don't run through authentication middleware.
		app.UseBlazorFrameworkFiles();
		app.UseStaticFiles();

		// Enable routing so that endpoint definitions (controllers, minimal APIs)
		// can match incoming requests to the appropriate handlers.
		app.UseRouting();

		// Enable authentication and authorization middleware to protect endpoints
		app.UseAuthentication();
		app.UseAuthorization();

		// Apply response compression (as configured in ConfigureServices) to reduce
		// payload sizes and improve perceived latency for clients.
		app.UseResponseCompression();

		// -------------------------------------------------------------------------
		// API Endpoint Mapping
		// -------------------------------------------------------------------------
		// All business API features are mounted under the versioned /api/v{version}
		// prefix via a central route group. This group applies:
		//
		//   - API versioning (URL segment-based, e.g., /api/v1/auth/login)
		//   - ValidationFilter globally (automatic DataAnnotations validation)
		//
		// Features that are NOT part of the business API (e.g. Health probes,
		// infrastructure endpoints) are mapped directly to `app` instead.
		// -------------------------------------------------------------------------
		RouteGroupBuilder api = app.MapVersionedApiGroup();

		// Map business API features to the versioned /api/v{version} group.
		// Each feature maps its endpoints relative to the group (e.g. /auth, /system).
		api.MapAuthFeature();
		api.MapUserFeature();
		api.MapSystemFeature();
		api.MapHealthApiFeature();

		// Map infrastructure endpoints directly to app (outside versioned API).
		// The /health endpoint is the standard ASP.NET Core health check for container
		// orchestration probes. It must remain unversioned and at a well-known path.
		// See Health/EndpointMapping.cs for details on the split.
		app.MapHealthProbesFeature();

		// Fallback: if no API/other endpoint matches, serve the Blazor index.html.
		// This enables client-side routing for the SPA.
		app.MapFallbackToFile("index.html");

		// -------------------------------------------------------------------------
		// API Version Validation
		// -------------------------------------------------------------------------
		// Validate that all versioned API endpoints have explicit MapToApiVersion()
		// calls. This prevents endpoints from being unintentionally available in
		// all API versions. The validation runs at startup and fails fast if any
		// endpoint is missing an explicit version mapping.
		// -------------------------------------------------------------------------
		app.ValidateExplicitApiVersionMappings();

		// -------------------------------------------------------------------------
		// Authorization Validation
		// -------------------------------------------------------------------------
		// Validate that all versioned API endpoints have explicit authorization
		// declarations (RequireAuthorization() or AllowAnonymous()). This prevents
		// accidental exposure of unprotected endpoints. The validation runs at
		// startup and fails fast if any endpoint is missing an explicit declaration.
		// -------------------------------------------------------------------------
		app.ValidateExplicitAuthorizationPolicies();
	}
}
