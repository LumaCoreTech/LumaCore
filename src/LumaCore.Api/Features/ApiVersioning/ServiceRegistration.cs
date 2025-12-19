// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Asp.Versioning;

namespace LumaCore.Api.Features.ApiVersioning;

/// <summary>
/// Provides extension methods for registering the API versioning feature services.
/// </summary>
/// <remarks>
///     <para>
///     This class is part of the API Versioning feature and configures URL segment-based
///     versioning for all LumaCore API endpoints. The versioning strategy uses path-based
///     version indicators (e.g., <c>/api/v1/...</c>, <c>/api/v2/...</c>).
///     </para>
///     <para>
///         <b>Why URL Segment Versioning?</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <b>Explicit:</b> The version is immediately visible in the URL, making it
///             clear which API version a client is targeting.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>Cacheable:</b> Different versions have different URLs, enabling proper
///             HTTP caching without version-specific cache keys.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>Simple Routing:</b> Standard URL routing handles version selection;
///             no custom middleware or header inspection required.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>Developer Friendly:</b> Easy to test with tools like curl, browsers,
///             and API clients without configuring custom headers.
///             </description>
///         </item>
///     </list>
/// </remarks>
public static class ServiceRegistration
{
	/// <summary>
	/// Registers the API versioning feature services using the <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <returns>The <paramref name="builder"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This is a convenience wrapper that forwards to <see cref="AddApiVersioningFeatureCore"/>
	///     using the <see cref="IServiceCollection"/> exposed by the builder.
	///     </para>
	/// </remarks>
	public static WebApplicationBuilder AddApiVersioningFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddApiVersioningFeatureCore();
		return builder;
	}

	/// <summary>
	/// Registers the API versioning feature services using the underlying <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register services with.</param>
	/// <returns>The <paramref name="services"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method configures ASP.NET Core API versioning with the following settings:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <b>Default Version:</b> <see cref="ApiVersions.V1"/> is used when no version
	///             is specified and <c>AssumeDefaultVersionWhenUnspecified</c> is enabled.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <b>Version Reporting:</b> The <c>api-supported-versions</c> and
	///             <c>api-deprecated-versions</c> headers are included in responses.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <b>URL Segment Reader:</b> Versions are read from the URL path segment
	///             (e.g., <c>/api/v1/...</c>) using <see cref="UrlSegmentApiVersionReader"/>.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <b>API Explorer:</b> Integrates with <c>IApiVersionDescriptionProvider</c>
	///             for OpenAPI document generation per API version.
	///             </description>
	///         </item>
	///     </list>
	/// </remarks>
	public static IServiceCollection AddApiVersioningFeatureCore(this IServiceCollection services)
	{
		services.AddApiVersioning(options =>
			{
				// Set the default API version to V1.
				// This is used when AssumeDefaultVersionWhenUnspecified is true and
				// no version is provided in the request.
				options.DefaultApiVersion = ApiVersions.V1;

				// Include api-supported-versions and api-deprecated-versions headers
				// in all API responses. This helps clients discover available versions
				// and plan migrations away from deprecated versions.
				options.ReportApiVersions = true;

				// Use URL segment versioning exclusively.
				// The version is extracted from the {version:apiVersion} route parameter.
				// Example: /api/v1/auth/login → version 1
				options.ApiVersionReader = new UrlSegmentApiVersionReader();
			})
			.AddApiExplorer(options =>
			{
				// Format the version as 'v1', 'v2', etc. in the OpenAPI document names.
				// The format string uses: 'v' = literal, VVV = major[.minor][-status]
				// See: https://github.com/dotnet/aspnet-api-versioning/wiki/Version-Format
				options.GroupNameFormat = "'v'VVV";

				// Replace the {version:apiVersion} placeholder in route templates with
				// the actual version number in the OpenAPI document paths.
				// Example: /api/v{version:apiVersion}/auth/login → /api/v1/auth/login
				options.SubstituteApiVersionInUrl = true;
			});

		return services;
	}
}
