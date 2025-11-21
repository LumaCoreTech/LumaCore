// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Admin;
using LumaCore.Api.Features.Auth;

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

		// Enforce HTTPS by redirecting HTTP requests to their HTTPS counterparts.
		// This improves security by ensuring traffic is encrypted in transit.
		app.UseHttpsRedirection();

		// Enable routing so that endpoint definitions (controllers, minimal APIs)
		// can match incoming requests to the appropriate handlers.
		app.UseRouting();

		// Enable authentication and authorization middleware to protect endpoints
		app.UseAuthentication();
		app.UseAuthorization();

		// In development, apply the permissive CORS policy configured as "DevOpen".
		// This allows frontends and tools running on arbitrary origins to call the
		// API without CORS issues. For production, a stricter policy should be used.
		if (app.Environment.IsDevelopment())
		{
			app.UseCors("DevOpen");
		}

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

		// Map attribute-routed controllers (e.g. [ApiController]) into the endpoint
		// routing table so that they can handle incoming HTTP requests.
		app.MapControllers();

		// Register a standard health check endpoint that integrates with the
		// health checks infrastructure. This is suitable for readiness probes,
		// because it can aggregate the state of multiple subsystems and fail
		// if any of them is unhealthy.
		app.MapHealthChecks("/health");

		// Provide a very lightweight liveness probe that always returns "ok".
		// This is intentionally decoupled from the main health checks to avoid
		// cascading failures and keep liveness semantics simple for orchestrators
		// such as Kubernetes or Docker.
		app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
			.WithName("HealthLive")
			.WithDescription("Liveness probe (static OK).");

		// Minimal fallback for the root URL to confirm that the API host is up.
		// This is convenient for quick manual checks and smoke tests.
		app.MapGet("/", () => Results.Text("LumaCore API is running.", "text/plain"));
	}
}
