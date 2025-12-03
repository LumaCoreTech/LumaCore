// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Reflection;

using LumaCore.Api.Features.Admin;
using LumaCore.Api.Features.Auth;
using LumaCore.Api.Features.Cors;
using LumaCore.Api.Features.Health;
using LumaCore.Api.Features.HttpsRedirection;
using LumaCore.Api.Features.ProxyHeaders;
using LumaCore.Api.Features.SecurityHeaders;

using Microsoft.OpenApi;

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

		// Configure a permissive CORS policy for local development: all origins,
		// headers and methods are allowed. This makes it easy to call the API from
		// browser-based tools and frontends during development, but must not be
		// reused as-is for production environments.
		builder.Services.AddCors(options =>
		{
			options.AddPolicy(
				"DevOpen",
				policy =>
					policy.AllowAnyOrigin()
						.AllowAnyHeader()
						.AllowAnyMethod());
		});

		// Swagger/OpenAPI
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen(o =>
		{
			// Register the primary OpenAPI document ("v1") and attach basic metadata
			// such as title, version, description, contact and license information.
			// This metadata is shown in Swagger UI and consumed by tools that import
			// the LumaCore API definition.
			o.SwaggerDoc(
				"v1",
				new OpenApiInfo
				{
					Title = "LumaCore API",
					Version = "v1",
					Description = "API surface of the LumaCore server (self-hosted, persona-focused AI runtime).",
					Contact = new OpenApiContact
					{
						Name = "LumaCore Project",
						Url = new Uri("https://lumacore.tech")
					},
					License = new OpenApiLicense
					{
						Name = "MIT License",
						Url = new Uri("https://github.com/LumaCoreTech/LumaCore/blob/main/LICENSE")
					}
				});

			// Include XML documentation from the compiled assembly so that controller and model
			// comments (///) appear in the generated OpenAPI specification and Swagger UI.
			string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
			string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
			if (File.Exists(xmlPath))
				o.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

			// Align OpenAPI schema generation with C# nullable reference types:
			// non-nullable reference types are treated as required, nullable ones as optional.
			o.SupportNonNullableReferenceTypes();

			// Define a reusable HTTP bearer security scheme so that Swagger/OpenAPI
			// knows about JWT-based authentication via the Authorization header.
			// This scheme is later referenced by the global security requirement.
			o.AddSecurityDefinition(
				"Bearer",
				new OpenApiSecurityScheme
				{
					In = ParameterLocation.Header,
					Description = "Please enter JWT",
					Name = "Authorization",
					Type = SecuritySchemeType.Http,
					Scheme = "bearer",
					BearerFormat = "JWT"
				});

			// Attach a global security requirement so that all operations in this OpenAPI document
			// use the "Bearer" security scheme defined above. The dictionary key is an
			// OpenApiSecuritySchemeReference that resolves to components.securitySchemes["Bearer"]
			// in the generated document; the empty string list indicates that no specific
			// OAuth2 scopes are required – only the presence of a valid bearer token.
			o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
			{
				[new OpenApiSecuritySchemeReference("Bearer", doc)] = []
			});
		});

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
