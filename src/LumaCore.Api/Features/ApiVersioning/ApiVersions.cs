// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Asp.Versioning;

namespace LumaCore.Api.Features.ApiVersioning;

/// <summary>
/// Provides centralized API version constants for the LumaCore API.
/// </summary>
/// <remarks>
///     <para>
///     This class serves as the single source of truth for all supported API versions.
///     All version definitions should be added here to ensure consistency across the
///     application and to make version management straightforward.
///     </para>
///     <para>
///     When adding a new API version:
///     </para>
///     <list type="number">
///         <item>
///             <description>
///             Add a new <see cref="ApiVersion"/> constant in this class.
///             </description>
///         </item>
///         <item>
///             <description>
///             Register the version in <see cref="VersionedApiGroup.MapVersionedApiGroup"/>
///             using <c>HasApiVersion()</c> or <c>HasDeprecatedApiVersion()</c>.
///             </description>
///         </item>
///         <item>
///             <description>
///             Use <c>MapToApiVersion()</c> in your feature's endpoint mapping to bind
///             endpoints to the new version.
///             </description>
///         </item>
///     </list>
/// </remarks>
static class ApiVersions
{
	/// <summary>
	/// API version 1 – the initial stable release of the LumaCore API.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This version includes all foundational endpoints:
	///     </para>
	///     <list type="bullet">
	///         <item><c>/api/v1/auth/*</c> – Authentication and token management</item>
	///         <item><c>/api/v1/admin/*</c> – Administrative operations</item>
	///         <item><c>/api/v1/health/*</c> – Health and liveness probes</item>
	///     </list>
	///     <para>
	///     Every endpoint MUST specify its version(s) explicitly via <c>MapToApiVersion()</c>.
	///     The application validates this at startup and will fail to start if any
	///     endpoint is missing an explicit version mapping.
	///     </para>
	/// </remarks>
	public static readonly ApiVersion V1 = new(1);

	// -------------------------------------------------------------------------
	// ADDING NEW VERSIONS
	// -------------------------------------------------------------------------
	// When introducing breaking changes or new functionality that warrants a
	// new API version, add a new constant here:
	//
	//     public static readonly ApiVersion V2 = new(2);
	//
	// Then register it in VersionedApiGroup.MapVersionedApiGroup() using:
	//
	//     .HasApiVersion(ApiVersions.V2)
	//
	// To deprecate an older version, use:
	//
	//     .HasDeprecatedApiVersion(ApiVersions.V1)
	//
	// This will add the api-deprecated-versions header to responses.
	// -------------------------------------------------------------------------
}
