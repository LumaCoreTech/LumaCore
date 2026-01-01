// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.ApiVersioning;

using Microsoft.AspNetCore.Mvc;

using V1 = LumaCore.Api.Contracts.V1.Health;

namespace LumaCore.Api.Features.Health;

/// <summary>
/// Provides extension methods for mapping health check endpoints to the application's routing pipeline.
/// </summary>
/// <remarks>
///     <para>
///     Exposes endpoints for liveness probes and health monitoring by orchestration systems and the Web UI.
///     </para>
///     <para>The Health feature is split into two mapping methods to accommodate different routing requirements:</para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="MapHealthProbesFeature"/> — Maps the standard ASP.NET Core health check endpoint at
///             <c>/health</c>. This is infrastructure-level and must remain unversioned for compatibility with
///             container orchestrators.
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="MapHealthApiFeature"/> — Maps the JSON-based liveness endpoint at
///             <c>/api/v{version}/health/live</c>. This is part of the versioned API surface and follows the same
///             versioning scheme as other business features.
///             </description>
///         </item>
///     </list>
/// </remarks>
static class EndpointMapping
{
	// -------------------------------------------------------------------------
	// HEALTH FEATURE – OVERVIEW
	//
	// This feature defines backend health endpoints that can be consumed by the
	// LumaCore Web UI, orchestration environments, and monitoring systems.
	//
	// The feature is split into two parts:
	//
	//   1. VERSIONED API (MapHealthApiFeature)
	//      - GET /api/v1/health/live
	//        A lightweight JSON-based liveness probe that returns an instance of
	//        ApiHealthLiveResponse. It is designed to be safe for frequent polling
	//        and is intentionally kept anonymous so that the UI can display whether
	//        the backend is reachable even before authentication is configured.
	//
	//   2. INFRASTRUCTURE (MapHealthProbesFeature)
	//      - GET /health
	//        The standard ASP.NET Core health check endpoint for readiness probes.
	//        Aggregates all registered health checks and returns Healthy/Degraded/Unhealthy.
	//        This endpoint is unversioned and mapped directly to the application root
	//        for compatibility with Kubernetes, Docker, and other orchestrators.
	//
	// NOT YET IMPLEMENTED:
	//
	//   - component-level health detail endpoints (e.g. /api/v1/health/details)
	//   - severity / status breakdown (degraded vs. failed)
	//   - per-subsystem diagnostics (database, vector store, LLM backend, storage)
	//
	// These additional capabilities can be added to the versioned API in the future.
	// -------------------------------------------------------------------------

	/// <summary>
	/// Maps the health API endpoints to the versioned API route group.
	/// </summary>
	/// <param name="endpoints">
	/// The <see cref="IEndpointRouteBuilder"/> to map endpoints to. This should be the
	/// versioned API route group (<c>/api/v{version}</c>), not the root application.
	/// </param>
	/// <returns>The <paramref name="endpoints"/> builder for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method maps health-related API endpoints that are part of the versioned
	///     API surface:
	///     </para>
	///     <list type="bullet">
	///         <item><c>GET /api/v1/health/live</c> — Lightweight JSON-based liveness probe</item>
	///     </list>
	///     <para>
	///     Unlike the infrastructure probe (<see cref="MapHealthProbesFeature"/>), these
	///     endpoints follow the standard API versioning scheme and may evolve across
	///     API versions.
	///     </para>
	///     <para>
	///         <b>Usage in Program.Pipeline.cs:</b>
	///     </para>
	///     <code>
	///     RouteGroupBuilder api = app.MapVersionedApiGroup();
	///     
	///     api.MapAuthFeature();
	///     api.MapAdminFeature();
	///     api.MapHealthApiFeature();  // /api/v1/health/live
	///     
	///     // Infrastructure (unversioned)
	///     app.MapHealthProbesFeature();  // /health
	///     </code>
	/// </remarks>
	public static IEndpointRouteBuilder MapHealthApiFeature(this IEndpointRouteBuilder endpoints)
	{
		// Note: This feature maps relative paths. The /api/v{version} prefix is provided
		// by the central route group in Program.Pipeline.cs which also applies global
		// filters like validation. Features should NOT include /api in their paths.
		RouteGroupBuilder group = endpoints
			.MapGroup("/health")
			.WithTags("Health");

		// -------------------------------------------------------------------------
		// GET /api/v{version}/health/live
		// -------------------------------------------------------------------------
		// Returns a small JSON payload that indicates whether the backend is
		// responsive. The response is intentionally minimal and marked as
		// non-cacheable so that callers always receive fresh information.
		//
		// This endpoint is:
		//   - Versioned (follows the /api/v{version} scheme)
		//   - Anonymous (no authentication required)
		//   - Non-cacheable (always returns fresh status)
		// -------------------------------------------------------------------------
		group.MapGet(
				"/live",
				() => Results.Ok(new V1.ApiHealthLiveResponse("ok")))
			.MapToApiVersion(ApiVersions.V1)
			.WithName("ApiHealthLive")
			.WithSummary("Returns a simple JSON-based liveness indicator for the backend.")
			.WithDescription(
				"Returns a minimal JSON payload that indicates whether the backend is " +
				"currently reachable. This endpoint is primarily intended for use by the " +
				"LumaCore Web UI and by external monitoring systems as a lightweight " +
				"liveness probe.")
			.WithMetadata(
				new ResponseCacheAttribute
				{
					NoStore = true,
					Location = ResponseCacheLocation.None
				})
			.AllowAnonymous();

		return endpoints;
	}

	/// <summary>
	/// Maps the infrastructure health probe endpoint to the application root.
	/// </summary>
	/// <param name="app">
	/// The <see cref="IEndpointRouteBuilder"/> to map endpoints to. This should be the
	/// root application builder (<see cref="WebApplication"/>), not the versioned API group.
	/// </param>
	/// <returns>The <paramref name="app"/> builder for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method maps the standard ASP.NET Core health check endpoint:
	///     </para>
	///     <list type="bullet">
	///         <item><c>GET /health</c> — Aggregated readiness probe for all registered health checks</item>
	///     </list>
	///     <para>
	///         <b>Why Unversioned?</b>
	///     </para>
	///     <para>
	///     The <c>/health</c> endpoint is intentionally unversioned because:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             Container orchestrators (Kubernetes, Docker) expect it at a fixed, well-known path.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             The ASP.NET Core health check middleware follows this convention by default.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             Infrastructure probes should not require knowledge of API versioning.
	///             </description>
	///         </item>
	///     </list>
	/// </remarks>
	public static IEndpointRouteBuilder MapHealthProbesFeature(this IEndpointRouteBuilder app)
	{
		// -------------------------------------------------------------------------
		// GET /health (ASP.NET Core Standard Health Check)
		// -------------------------------------------------------------------------
		// Maps the standard ASP.NET Core health check endpoint for readiness probes.
		// This endpoint aggregates all registered health checks and returns a simple
		// status (Healthy, Degraded, Unhealthy). Container orchestrators like
		// Kubernetes typically use this for readiness probes.
		//
		// This endpoint is intentionally:
		//   - Unversioned (no /api/v1 prefix)
		//   - Anonymous (no authentication required)
		//   - At the root path (standard convention)
		// -------------------------------------------------------------------------
		app.MapHealthChecks("/health")
			.AllowAnonymous();

		return app;
	}
}
