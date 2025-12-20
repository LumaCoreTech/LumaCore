// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Asp.Versioning.Builder;

using LumaCore.Api.Features.Validation;

namespace LumaCore.Api.Features.ApiVersioning;

/// <summary>
/// Provides extension methods for creating the versioned API route group.
/// </summary>
/// <remarks>
///     <para>
///     This class is part of the API Versioning feature and provides the central
///     route group that all business API features are mounted on. The route group
///     is configured with:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             URL segment-based versioning via the <c>/api/v{version:apiVersion}</c> prefix.
///             </description>
///         </item>
///         <item>
///             <description>
///             Automatic request validation via <see cref="ValidationExtensions.WithValidation(RouteGroupBuilder)"/>.
///             </description>
///         </item>
///         <item>
///             <description>
///             Version reporting headers (<c>api-supported-versions</c>, <c>api-deprecated-versions</c>).
///             </description>
///         </item>
///     </list>
/// </remarks>
public static class VersionedApiGroup
{
	/// <summary>
	/// The route prefix for all versioned API endpoints.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The <c>{version:apiVersion}</c> segment is a route constraint that extracts the
	///     API version from the URL. For example:
	///     </para>
	///     <list type="bullet">
	///         <item><c>/api/v1/auth/login</c> → Version 1</item>
	///         <item><c>/api/v2/auth/login</c> → Version 2</item>
	///     </list>
	/// </remarks>
	private const string RoutePrefix = "/api/v{version:apiVersion}";

	/// <summary>
	/// Creates the versioned API route group that business features mount their endpoints on.
	/// </summary>
	/// <param name="app">The <see cref="WebApplication"/> to create the route group on.</param>
	/// <returns>
	/// A <see cref="RouteGroupBuilder"/> configured with versioning and validation that
	/// features can use to map their endpoints.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method creates the central route group for all business API features. The
	///     returned <see cref="RouteGroupBuilder"/> is pre-configured with:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <b>Versioning:</b> An <see cref="ApiVersionSet"/> containing all supported
	///             versions defined in <see cref="ApiVersions"/>. Endpoints inherit this
	///             version set automatically.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <b>Validation:</b> The <see cref="ValidationExtensions.WithValidation(RouteGroupBuilder)"/>
	///             filter is applied, ensuring all request bodies are validated using
	///             <see cref="System.ComponentModel.DataAnnotations"/>.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///         <b>Usage in Program.Pipeline.cs:</b>
	///     </para>
	///     <code>
	///     RouteGroupBuilder api = app.MapVersionedApiGroup();
	///     
	///     api.MapAuthFeature();
	///     api.MapAdminFeature();
	///     </code>
	///     <para>
	///         <b>Adding New Versions:</b>
	///     </para>
	///     <para>
	///     When adding a new API version, register it in the <see cref="ApiVersionSet"/>
	///     builder below using <c>HasApiVersion()</c>. To deprecate an older version,
	///     change its registration to <c>HasDeprecatedApiVersion()</c>.
	///     </para>
	///     <para>
	///         <b>Feature Endpoint Mapping:</b>
	///     </para>
	///     <para>
	///     Every endpoint MUST use <c>MapToApiVersion()</c> to specify which version(s)
	///     it belongs to. The application validates this at startup and will fail to
	///     start if any endpoint is missing an explicit version mapping.
	///     </para>
	///     <code>
	///     // ✅ Correct: Explicit version mapping
	///     group.MapPost("/login", LoginHandler)
	///         .MapToApiVersion(ApiVersions.V1);
	///     
	///     // Available in multiple versions
	///     group.MapPost("/login/mfa", MfaLoginHandler)
	///         .MapToApiVersion(ApiVersions.V1)
	///         .MapToApiVersion(ApiVersions.V2);
	///     </code>
	/// </remarks>
	public static RouteGroupBuilder MapVersionedApiGroup(this WebApplication app)
	{
		// ---------------------------------------------------------------------
		// API VERSION SET
		// ---------------------------------------------------------------------
		// The ApiVersionSet defines which versions are supported by the API.
		// All versions registered here will be reported in the
		// api-supported-versions response header.
		//
		// When adding a new version:
		//   1. Add the constant to ApiVersions.cs
		//   2. Register it here with .HasApiVersion(ApiVersions.Vx)
		//
		// When deprecating a version:
		//   1. Change .HasApiVersion() to .HasDeprecatedApiVersion()
		//   2. The version will appear in api-deprecated-versions header
		// ---------------------------------------------------------------------
		ApiVersionSet versionSet = app.NewApiVersionSet()
			.HasApiVersion(ApiVersions.V1)
			// .HasApiVersion(ApiVersions.V2)              // Add when V2 is introduced
			// .HasDeprecatedApiVersion(ApiVersions.V1)   // Use when deprecating V1
			.ReportApiVersions()
			.Build();

		// ---------------------------------------------------------------------
		// VERSIONED ROUTE GROUP
		// ---------------------------------------------------------------------
		// Create the central /api/v{version} route group that all business
		// features mount their endpoints on. The group is configured with:
		//
		//   - API version set (defines supported versions)
		//   - Validation filter (validates request bodies)
		//
		// Features map relative paths to this group:
		//   api.MapAuthFeature()  → /api/v1/auth/...
		//   api.MapAdminFeature() → /api/v1/admin/...
		// ---------------------------------------------------------------------
		RouteGroupBuilder api = app
			.MapGroup(RoutePrefix)
			.WithApiVersionSet(versionSet)
			.WithValidation();

		return api;
	}
}
