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
/// </remarks>
public static class EndpointMapping
{
	/// <summary>
	/// Maps the health-related endpoints into the application's endpoint routing table.
	/// </summary>
	/// <param name="app">
	/// The <see cref="IEndpointRouteBuilder"/> used to define HTTP endpoints for the application.
	/// </param>
	/// <returns>
	/// The same <see cref="IEndpointRouteBuilder"/> instance to enable fluent endpoint configuration.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method groups the feature's endpoints under a common prefix (currently
	///     <c>/api/health</c>) and attaches metadata such as tags and caching directives.
	///     It is intended to be called once during startup, typically from the central
	///     pipeline configuration in <c>Program.Pipeline.cs</c>.
	///     </para>
	///     <para>
	///     At the current stage of development the feature exposes a single liveness
	///     endpoint (<c>GET /api/health/live</c>). Additional health or diagnostics
	///     endpoints can be added here in the future while keeping all health-related
	///     routes clearly grouped under the same path prefix.
	///     </para>
	/// </remarks>
	public static IEndpointRouteBuilder MapHealthFeature(this IEndpointRouteBuilder app)
	{
		// -----------------------------------------------------------------------------
		// HEALTH FEATURE – CURRENT STATE
		//
		// This feature defines backend health endpoints that can be consumed by the
		// LumaCore Web UI, orchestration environments, and monitoring systems.
		//
		// The endpoints are currently organized as follows:
		//
		//   - GET /api/health/live
		//     A lightweight JSON-based liveness probe that returns an instance of
		//     ApiHealthLiveResponse. It is designed to be safe for frequent polling
		//     and is intentionally kept anonymous so that the UI can display whether
		//     the backend is reachable even before authentication is configured.
		//
		// In addition to these feature-specific endpoints, the application may expose
		// a more detailed health check endpoint (for example /health) using the
		// standard ASP.NET Core health check middleware. That endpoint typically
		// reflects the readiness of the application by aggregating all registered
		// health checks.
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

		RouteGroupBuilder group = app.MapGroup("/api/health")
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

		return app;
	}
}
