// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LumaCore.Api.Features.OpenApi;

/// <summary>
/// Provides extension methods for registering the OpenAPI feature with the application.
/// </summary>
/// <remarks>
///     <para>
///     The OpenAPI feature configures native .NET 10 OpenAPI document generation, replacing
///     the need for Swashbuckle's <c>AddSwaggerGen()</c>. The generated document is served
///     at <c>/openapi/v1.json</c> via <c>MapOpenApi()</c> in the request pipeline.
///     </para>
///     <para>
///     This feature configures:
///     </para>
///     <list type="bullet">
///         <item>Document metadata (title, version, contact, license)</item>
///         <item>JWT Bearer security scheme definition</item>
///         <item>Global security requirement for all operations</item>
///         <item>Automatic error response documentation (400/401/403)</item>
///     </list>
/// </remarks>
public static class ServiceRegistration
{
	/// <summary>
	/// Registers the OpenAPI feature services with the application.
	/// </summary>
	/// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
	/// <returns>The <paramref name="builder"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method configures native .NET 10 OpenAPI document generation with:
	///     </para>
	///     <list type="bullet">
	///         <item>Document metadata via <see cref="IOpenApiDocumentTransformer"/></item>
	///         <item>JWT Bearer security scheme</item>
	///         <item>Global security requirement for all operations</item>
	///         <item><see cref="SecurityResponsesTransformer"/> for automatic error documentation</item>
	///     </list>
	/// </remarks>
	public static WebApplicationBuilder AddOpenApiFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddOpenApi(
			"v1",
			options =>
			{
				// Configure document metadata via a transformer. This approach is more flexible
				// than Swashbuckle's SwaggerDoc() and allows for dynamic metadata generation.
				options.AddDocumentTransformer((document, _, _) =>
				{
					document.Info = new OpenApiInfo
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
					};

					return Task.CompletedTask;
				});

				// Add JWT Bearer security scheme to the OpenAPI document.
				// This transformer adds the security definition and applies it globally to all operations.
				options.AddDocumentTransformer((document, _, _) =>
				{
					// Ensure the Components and SecuritySchemes collections exist.
					document.Components ??= new OpenApiComponents();
					document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

					// Define the Bearer authentication scheme.
					document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
					{
						Type = SecuritySchemeType.Http,
						Scheme = "bearer",
						BearerFormat = "JWT",
						Description = "Enter your JWT token"
					};

					return Task.CompletedTask;
				});

				// Apply global security requirement so all operations require Bearer authentication.
				// This is equivalent to Swashbuckle's AddSecurityRequirement().
				// In Microsoft.OpenApi 2.0+, we use OpenApiSecuritySchemeReference instead of OpenApiReference.
				options.AddOperationTransformer((operation, context, _) =>
				{
					operation.Security ??= [];
					operation.Security.Add(
						new OpenApiSecurityRequirement
						{
							[new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
						});

					return Task.CompletedTask;
				});

				// Automatically document error responses based on endpoint metadata.
				// This transformer adds 400/401/403 responses where appropriate:
				//   - 400: Endpoints with request body (validation errors)
				//   - 401: Endpoints with RequireAuthorization()
				//   - 403: Endpoints with specific roles or policies
				options.AddOperationTransformer<SecurityResponsesTransformer>();
			});

		return builder;
	}
}
