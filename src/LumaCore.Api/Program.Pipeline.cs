// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Admin;
using LumaCore.Api.Features.Auth;
using LumaCore.Api.Features.Cors;
using LumaCore.Api.Features.Health;
using LumaCore.Api.Features.HttpsRedirection;
using LumaCore.Api.Features.ProxyHeaders;
using LumaCore.Api.Features.SecurityHeaders;

using Microsoft.OpenApi;

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

		// Enforce HTTPS by redirecting HTTP requests to their HTTPS counterparts.
		// Must come AFTER proxy headers so the scheme is correctly detected.
		app.UseHttpsRedirectionFeature();

		// Add HTTP security headers (HSTS, X-Frame-Options, CSP, etc.) to all responses.
		// This must be early in the pipeline to ensure headers are set before any response.
		app.UseSecurityHeadersFeature();

		if (app.Environment.IsDevelopment())
		{
			// Show detailed exception information and a developer-friendly error page
			// instead of the generic error handler. This should only be enabled in
			// development to avoid leaking implementation details.
			app.UseDeveloperExceptionPage();

			// Generate an OpenAPI 3.1 document for the application using Swashbuckle.
			// The concrete schema version (3.0 vs 3.1) is selected here so that the
			// same SwaggerGen configuration can be reused across versions.
			app.UseSwagger(options =>
			{
				// Explicitly target OpenAPI 3.1 for schema generation.
				options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
			});

			// Expose Swagger UI as a developer-facing API browser that allows
			// interactive exploration and testing of the LumaCore endpoints.
			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint("/swagger/v1/swagger.json", "LumaCore API v1");
				c.RoutePrefix = "swagger";

				// Provide a clearer title in the browser tab and Swagger UI header.
				c.DocumentTitle = "LumaCore API Explorer";

				// Show server-side execution time for each request in the UI.
				c.DisplayRequestDuration();

				// Collapse all operations by default to keep the UI manageable as
				// the number of endpoints grows. Users can expand sections as needed.
				c.DocExpansion(DocExpansion.None);
			});
		}

		// Apply CORS policy based on configuration to control cross-origin requests.
		// This must be done BEFORE UseRouting() to properly handle preflight requests.
		// See https://docs.microsoft.com/aspnet/core/security/cors
		app.UseCorsFeature();

		// Enable routing so that endpoint definitions (controllers, minimal APIs)
		// can match incoming requests to the appropriate handlers.
		app.UseRouting();

		// Enable authentication and authorization middleware to protect endpoints
		app.UseAuthentication();
		app.UseAuthorization();

		// Enable static file handling and Blazor framework files for the SPA.
		app.UseBlazorFrameworkFiles();
		app.UseStaticFiles();

		// Add structured request logging for each HTTP request. This writes a single
		// summary log entry per request including method, path, status code and
		// elapsed time, which is very helpful for diagnostics and monitoring.
		app.UseSerilogRequestLogging(options =>
		{
			options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
		});

		// Apply response compression (as configured in ConfigureServices) to reduce
		// payload sizes and improve perceived latency for clients.
		app.UseResponseCompression();

		// Map authentication-related endpoints (e.g. /auth/login) into the endpoint routing table.
		app.MapAuthFeature();

		// Map admin endpoints (e.g. /admin/*) into the endpoint routing table.
		app.MapAdminFeature();

		// Map health-related endpoints (e.g. /health, /api/health/live, ...)
		app.MapHealthFeature();

		// Map attribute-routed controllers (e.g. [ApiController]) into the endpoint
		// routing table so that they can handle incoming HTTP requests.
		app.MapControllers();

		// Fallback: if no API/other endpoint matches, serve the Blazor index.html.
		// This enables client-side routing for the SPA.
		app.MapFallbackToFile("index.html");
	}
}
