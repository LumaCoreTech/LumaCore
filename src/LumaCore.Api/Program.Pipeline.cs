// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Admin;
using LumaCore.Api.Features.Auth;
using LumaCore.Api.Features.Cors;
using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Features.Health;
using LumaCore.Api.Features.HttpsRedirection;
using LumaCore.Api.Features.ProxyHeaders;
using LumaCore.Api.Features.SecurityHeaders;
using LumaCore.Api.Features.Validation;

using Serilog;

using Swashbuckle.AspNetCore.SwaggerUI;

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
			options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
		});

		if (app.Environment.IsDevelopment())
		{
			// Show detailed exception information and a developer-friendly error page
			// instead of the generic error handler. This should only be enabled in
			// development to avoid leaking implementation details.
			app.UseDeveloperExceptionPage();
		}

		// ┌─────────────────────────────────────────────────────────────────────────────┐
		// │ OpenAPI Document Endpoint                                                   │
		// ├─────────────────────────────────────────────────────────────────────────────┤
		// │ Exposes the OpenAPI document at /openapi/v1.json in Development mode.       │
		// │                                                                             │
		// │ BUILD-TIME GENERATION:                                                      │
		// │ When running `dotnet build /p:GenerateOpenApi=true`, MSBuild invokes        │
		// │ Microsoft.Extensions.ApiDescription.Server which launches the app with      │
		// │ GetDocument.Insider as entry assembly. Program.cs detects this and forces   │
		// │ ASPNETCORE_ENVIRONMENT=Development so the endpoint is available and         │
		// │ fallback configuration values are used (Production requires secrets).       │
		// │                                                                             │
		// │ In Production, the /openapi/v1.json endpoint is NOT exposed.                │
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
			// Note: The endpoint path changed from /swagger/v1/swagger.json to /openapi/v1.json.
			app.UseSwaggerUI(options =>
			{
				options.SwaggerEndpoint("/openapi/v1.json", "LumaCore API v1");
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
		// All business API features are mounted under the /api prefix via a central
		// route group. This group applies the ValidationFilter globally, ensuring
		// that all API endpoints automatically validate request bodies using
		// DataAnnotations without requiring each feature to register the filter.
		//
		// Features that are NOT part of the business API (e.g. Health probes,
		// infrastructure endpoints) are mapped directly to `app` instead.
		// -------------------------------------------------------------------------
		RouteGroupBuilder api = app.MapGroup("/api")
			.WithValidation();

		// Map business API features to the /api group.
		// Each feature maps its endpoints relative to the group (e.g. /auth, /admin).
		api.MapAuthFeature();
		api.MapAdminFeature();

		// Map infrastructure endpoints directly to app (outside /api).
		// Health endpoints are infrastructure, not business API. They have no complex
		// request bodies requiring validation and must remain accessible for container
		// orchestration probes. See Health/EndpointMapping.cs for details.
		app.MapHealthFeature();

		// Map attribute-routed controllers (e.g. [ApiController]) into the endpoint
		// routing table so that they can handle incoming HTTP requests.
		app.MapControllers();

		// Fallback: if no API/other endpoint matches, serve the Blazor index.html.
		// This enables client-side routing for the SPA.
		app.MapFallbackToFile("index.html");
	}
}
