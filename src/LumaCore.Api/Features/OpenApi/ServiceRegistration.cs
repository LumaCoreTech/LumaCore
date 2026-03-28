// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Asp.Versioning;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.Auth;

using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace LumaCore.Api.Features.OpenApi;

/// <summary>
/// Provides extension methods for registering the OpenAPI feature with the application.
/// </summary>
/// <remarks>
///     <para>
///     The OpenAPI feature configures native .NET 10 OpenAPI document generation, replacing the need for
///     Swashbuckle's <c>AddSwaggerGen()</c>. The generated documents are served at <c>/openapi/{version}.json</c>
///     via <c>MapOpenApi()</c> in the request pipeline.
///     </para>
///     <para>This feature generates one OpenAPI document per API version:</para>
///     <list type="bullet">
///         <item><c>/openapi/v1.json</c> – API version 1 specification</item>
///     </list>
///     <para>Each document includes:</para>
///     <list type="bullet">
///         <item>Document metadata (title, version, contact, license)</item>
///         <item>JWT Bearer and Cookie authentication security scheme definitions</item>
///         <item>Global security requirement (Bearer or Cookie) for all operations</item>
///         <item>Automatic error response documentation (400/401/403)</item>
///     </list>
/// </remarks>
static class ServiceRegistration
{
	/// <summary>
	/// Registers the OpenAPI feature services with the application.
	/// </summary>
	/// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
	/// <returns>The <paramref name="builder"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method registers OpenAPI documents for each supported API version. The document names match the version
	///     group names used by the API versioning feature (e.g., <c>v1</c>).
	///     </para>
	///     <para>
	///         <b>Adding New Versions:</b>
	///     </para>
	///     <para>
	///     When adding a new API version, add a corresponding <see cref="AddOpenApiDocument"/> call for the new version.
	///     For example:
	///     </para>
	///     <code>
	///     AddOpenApiDocument(builder.Services, ApiVersions.V2);
	///     </code>
	/// </remarks>
	public static WebApplicationBuilder AddOpenApiFeature(this WebApplicationBuilder builder)
	{
		// -------------------------------------------------------------------------
		// OPENAPI DOCUMENT REGISTRATION
		// -------------------------------------------------------------------------
		// Register one OpenAPI document per API version. The document name must match
		// the GroupName produced by the API versioning feature's GroupNameFormat.
		//
		// GroupNameFormat = "'v'VVV" produces: v1, v2, v2.1, v3-beta, etc.
		//
		// When adding a new API version:
		//   1. Add the version constant to ApiVersions.cs
		//   2. Register it in VersionedApiGroup.cs
		//   3. Add an AddOpenApiDocument() call here
		// -------------------------------------------------------------------------

		AddOpenApiDocument(builder.Services, ApiVersions.V1);

		// Future versions:
		// AddOpenApiDocument(builder.Services, ApiVersions.V2);

		return builder;
	}

	/// <summary>
	/// Registers an OpenAPI document for the specified API version.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to register services with.</param>
	/// <param name="version">The <see cref="ApiVersion"/> to generate a document for.</param>
	/// <remarks>
	///     <para>This method configures the OpenAPI document with:</para>
	///     <list type="bullet">
	///         <item>Document metadata (title, version, contact, license)</item>
	///         <item>JWT Bearer and Cookie authentication security schemes</item>
	///         <item>Global security requirement (Bearer or Cookie) for all operations</item>
	///         <item>Automatic error response documentation via <see cref="SecurityResponsesTransformer"/></item>
	///     </list>
	/// </remarks>
	private static void AddOpenApiDocument(IServiceCollection services, ApiVersion version)
	{
		// Build the document name to match the GroupNameFormat from API versioning.
		// Format: 'v'VVV → v1, v2, v2.1, etc.
		string documentName = $"v{version.MajorVersion}";
		if (version.MinorVersion is > 0)
		{
			documentName += $".{version.MinorVersion.Value}";
		}

		if (!string.IsNullOrEmpty(version.Status))
		{
			documentName += $"-{version.Status}";
		}

		services.AddOpenApi(
			documentName,
			options =>
			{
				// Configure document metadata via a transformer.
				options.AddDocumentTransformer((document, _, _) =>
				{
					document.Info = new OpenApiInfo
					{
						Title = "LumaCore API",
						Version = documentName,
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
					};

					return Task.CompletedTask;
				});

				// Add security schemes to the OpenAPI document.
				// Bearer: for API clients using the Authorization header.
				// CookieAuth: for browser clients using the HttpOnly access token cookie.
				options.AddDocumentTransformer((document, context, _) =>
				{
					document.Components ??= new OpenApiComponents();
					document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

					document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
					{
						Type = SecuritySchemeType.Http,
						Scheme = "bearer",
						BearerFormat = "JWT",
						Description = "Enter your JWT token"
					};

					// Cookie-based authentication for browser clients (Blazor WASM).
					// The cookie name is read from AuthCookieOptions to stay consistent
					// with the runtime configuration.
					AuthCookieOptions cookieOptions = context.ApplicationServices
						.GetRequiredService<IOptions<AuthCookieOptions>>()
						.Value;

					document.Components.SecuritySchemes["CookieAuth"] = new OpenApiSecurityScheme
					{
						Type = SecuritySchemeType.ApiKey,
						In = ParameterLocation.Cookie,
						Name = cookieOptions.Name,
						Description =
							"HttpOnly cookie containing the JWT access token. " +
							"Set automatically by the login endpoint for browser clients. " +
							"API clients should use the Bearer scheme instead."
					};

					return Task.CompletedTask;
				});

				// Apply global security requirement: Bearer OR Cookie authentication.
				// Separate entries in the OpenAPI security array represent OR alternatives.
				options.AddOperationTransformer((operation, context, _) =>
				{
					operation.Security ??= [];

					// Bearer token authentication (API clients).
					operation.Security.Add(
						new OpenApiSecurityRequirement
						{
							[new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
						});

					// Cookie authentication (browser clients).
					operation.Security.Add(
						new OpenApiSecurityRequirement
						{
							[new OpenApiSecuritySchemeReference("CookieAuth", context.Document)] = []
						});

					return Task.CompletedTask;
				});

				// Automatically document error responses based on endpoint metadata.
				options.AddOperationTransformer<SecurityResponsesTransformer>();

				// Add Set-Cookie response headers for endpoints annotated with SetCookieHeaderMetadata.
				options.AddOperationTransformer<SetCookieHeaderTransformer>();
			});
	}
}
