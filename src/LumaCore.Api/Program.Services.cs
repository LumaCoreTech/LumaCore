// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Admin;
using LumaCore.Api.Features.Auth;
using LumaCore.Api.Features.Cors;
using LumaCore.Api.Features.Health;
using LumaCore.Api.Features.HttpsRedirection;
using LumaCore.Api.Features.OpenApi;
using LumaCore.Api.Features.ProxyHeaders;
using LumaCore.Api.Features.SecurityHeaders;

public static partial class Program
{
	/// <summary>
	/// Registers all services, options, and health checks used by the LumaCore API.
	/// </summary>
	/// <param name="builder">The <see cref="WebApplicationBuilder"/> used to register services.</param>
	/// <remarks>
	///     <para>
	///     This method configures:
	///     <list type="bullet">
	///         <item>Controllers and API endpoints</item>
	///         <item>ProblemDetails for RFC 7807 compliant error responses</item>
	///         <item>Response compression for HTTPS</item>
	///         <item>CORS policy for development</item>
	///         <item>Swagger/OpenAPI documentation</item>
	///         <item>Configuration options with validation</item>
	///         <item>Health check infrastructure for all subsystems</item>
	///     </list>
	///     </para>
	/// </remarks>
	private static void ConfigureServices(WebApplicationBuilder builder)
	{
		// Register MVC-style controllers so that attribute-routed API endpoints
		// (e.g. [ApiController] + [Route]) are discovered and exposed by the app.
		builder.Services.AddControllers();

		// Register ProblemDetails services for RFC 7807 compliant error responses.
		// This enables consistent, machine-readable error payloads across all endpoints.
		// Unhandled exceptions, validation errors, and status codes are automatically
		// transformed into structured JSON responses with type, title, status, and detail.
		builder.Services.AddProblemDetails();

		// Enable HTTP response compression to reduce payload size for JSON and other
		// textual responses. This improves bandwidth usage and perceived latency.
		// Compression is explicitly enabled for HTTPS traffic only.
		builder.Services.AddResponseCompression(options =>
		{
			options.EnableForHttps = true; // Enable for HTTPS (careful with sensitive data!)
		});

		// Register and configure the Proxy Headers feature to correctly handle
		// X-Forwarded-* headers when the API is running behind a reverse proxy.
		builder.AddProxyHeadersFeature();

		// Register and configure the CORS feature for cross-origin requests.
		builder.AddCorsFeature();

		// Register and configure the Security Headers feature for HTTP security.
		builder.AddSecurityHeadersFeature();

		// Register authentication and authorization services and configure JWT bearer.
		builder.AddAuthFeature();

		// Register admin feature services and options.
		builder.AddAdminFeature();

		// Native OpenAPI document generation (.NET 10).
		// This replaces Swashbuckle's AddSwaggerGen() with the built-in OpenAPI support.
		// The document is generated at runtime and served via MapOpenApi() in the pipeline.
		builder.AddOpenApiFeature();

		// Register the health checks infrastructure so that individual subsystems
		// (e.g. database, vector store, model backends) can expose their status via
		// the centralized health endpoint. Concrete checks are added in separate
		// extension methods or feature modules.
		builder.AddHealthFeature();

		// Register HTTPS redirection feature.
		// Redirects HTTP requests to HTTPS when enabled in configuration.
		builder.AddHttpsRedirectionFeature();
	}
}
