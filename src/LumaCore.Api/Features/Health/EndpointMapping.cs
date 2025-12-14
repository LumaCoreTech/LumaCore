// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Health.Contracts;

using Microsoft.AspNetCore.Mvc;

namespace LumaCore.Api.Features.Health;

/// <summary>
/// Provides extension methods for mapping health check endpoints to the application's routing pipeline.
/// </summary>
/// <remarks>
///     <para>
///     This class is part of the Health feature and exposes endpoints for
///     liveness probes and health monitoring by orchestration systems and the Web UI.
///     </para>
///     <para>
///     <b>Infrastructure Feature:</b> Unlike business API features (Auth, Admin, etc.),
///     the Health feature is considered infrastructure and is NOT mounted on the central
///     <c>/api</c> route group. This is intentional for several reasons:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             Health endpoints must remain accessible to container orchestrators (Kubernetes,
///             Docker) without requiring authentication or going through API middleware.
///             </description>
///         </item>
///         <item>
///             <description>
///             The <c>/health</c> endpoint follows the ASP.NET Core standard convention and
///             must be at the root path for compatibility with existing tooling.
///             </description>
///         </item>
///         <item>
///             <description>
///             Health endpoints have no complex request bodies and do not benefit from the
///             automatic validation that the <c>/api</c> group provides.
///             </description>
///         </item>
///     </list>
///     <para>
///     As a result, this feature maps directly to the <see cref="IEndpointRouteBuilder"/>
///     passed to it, rather than being mounted on the <c>/api</c> group.
///     </para>
/// </remarks>
public static class EndpointMapping
{
	/// <summary>
	/// Maps the health-related endpoints into the application's endpoint routing table.
	/// </summary>
	/// <param name="endpoints">
	/// The <see cref="IEndpointRouteBuilder"/> to map endpoints to. Unlike business API features,
	/// this should be the root application builder, not the <c>/api</c> route group.
	/// </param>
	/// <returns>The <paramref name="endpoints"/> builder for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method maps health endpoints directly to the application root (not via the
	///     central <c>/api</c> route group). The effective endpoints are:
	///     </para>
	///     <list type="bullet">
	///         <item><c>GET /health</c> — Standard ASP.NET Core readiness probe</item>
	///         <item><c>GET /api/health/live</c> — Lightweight liveness probe</item>
	///     </list>
	///     <para>
	///     Note that even though this feature is not mounted on the central <c>/api</c> group,
	///     the liveness endpoint still uses the <c>/api/health</c> prefix for consistency with
	///     the API surface. The difference is that this feature maps absolute paths, while
	///     business API features map relative paths to the <c>/api</c> group.
	///     </para>
	/// </remarks>
	public static IEndpointRouteBuilder MapHealthFeature(this IEndpointRouteBuilder endpoints)
	{
		// -----------------------------------------------------------------------------
		// HEALTH FEATURE – CURRENT STATE
		//
		// This feature defines backend health endpoints that can be consumed by the
		// LumaCore Web UI, orchestration environments, and monitoring systems.
		//
		// IMPORTANT: This feature is infrastructure, not business API. It is mapped
		// directly to the application root, NOT to the central /api route group.
		// This ensures health endpoints remain accessible without going through the
		// ValidationFilter that the /api group applies.
		//
		// However, the liveness endpoint still uses the /api/health prefix for API
		// consistency. The difference is that this feature maps ABSOLUTE paths,
		// while business API features (Auth, Admin) map RELATIVE paths to the
		// central /api group.
		//
		// The endpoints are currently organized as follows:
		//
		//   - GET /health
		//     The standard ASP.NET Core health check endpoint for readiness probes.
		//     Aggregates all registered health checks and returns Healthy/Degraded/Unhealthy.
		//
		//   - GET /api/health/live
		//     A lightweight JSON-based liveness probe that returns an instance of
		//     ApiHealthLiveResponse. It is designed to be safe for frequent polling
		//     and is intentionally kept anonymous so that the UI can display whether
		//     the backend is reachable even before authentication is configured.
		//
		// NOT YET IMPLEMENTED:
		//
		//   - component-level health detail endpoints (e.g. /api/health/details)
		//   - severity / status breakdown (degraded vs. failed)
		//   - per-subsystem diagnostics (database, vector store, LLM backend, storage)
		//
		// These additional capabilities can be added here in the future while keeping
		// all health-related routes grouped under the /api/health prefix.
		// -----------------------------------------------------------------------------

		// -------------------------------------------------------------------------
		// GET /health (ASP.NET Core Standard Health Check)
		// -------------------------------------------------------------------------
		// Maps the standard ASP.NET Core health check endpoint for readiness probes.
		// This endpoint aggregates all registered health checks and returns a simple
		// status (Healthy, Degraded, Unhealthy). Container orchestrators like
		// Kubernetes typically use this for readiness probes.
		endpoints.MapHealthChecks("/health")
			.AllowAnonymous();

		RouteGroupBuilder group = endpoints.MapGroup("/api/health")
			.WithTags("Health");

		// -------------------------------------------------------------------------
		// GET /api/health/live
		// -------------------------------------------------------------------------
		// Returns a small JSON payload that indicates whether the backend is
		// responsive. The response is intentionally minimal and marked as
		// non-cacheable so that callers always receive fresh information.
		group.MapGet(
				"/live",
				() => Results.Ok(new ApiHealthLiveResponse("ok")))
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
}
