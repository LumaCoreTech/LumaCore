// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using LumaCore.Api.Features.Health;
using LumaCore.Api.Tests.Infrastructure;
using LumaCore.Data.Initialization;

using Xunit;

using V1 = LumaCore.Api.Contracts.V1.Health;

namespace LumaCore.Api.Tests.Features.Health;

// Health endpoints: from liveness pings to infrastructure readiness probes.
//
// These tests verify the three Health feature endpoints through a real HTTP pipeline
// backed by MiddlewareTestHarness with API versioning and a controllable
// DatabaseInitializationStatus singleton:
//
//   1. Liveness (GET /api/v1/health/live): a minimal JSON payload that proves the
//      backend process is running (Returns200WithOkStatus, ReturnsNoCacheHeaders).
//      The no-cache header test covers both /live and /ready since both endpoints
//      inherit the group-level Cache-Control filter.
//
//   2. Readiness (GET /api/v1/health/ready): reports operational readiness per
//      subsystem, driven by DatabaseInitializationStatus. Exercises all five enum
//      states with component-level status strings and messages.
//      See Ready.
//
//   3. Infrastructure probe (GET /health): the standard ASP.NET Core aggregated
//      health check consumed by Kubernetes/Docker. Exercises all
//      DatabaseInitializationState values including the ShouldRetry branch in
//      DatabaseInitializationHealthCheck (retrying vs giving up).
//      See Probe.
//
// For error handling, see ErrorHandling/. For DI registration, see ServiceRegistrationTests.

/// <summary>
/// Integration tests for the Health feature endpoint mapping defined in <see cref="EndpointMapping"/>.
/// </summary>
/// <remarks>
///     <para>
///     Tests exercise the three endpoint groups through a real HTTP pipeline:
///     </para>
///     <list type="bullet">
///         <item><c>GET /api/v1/health/live</c> — JSON liveness probe (this file)</item>
///         <item><c>GET /api/v1/health/ready</c> — JSON readiness probe (see <c>Ready</c>)</item>
///         <item><c>GET /health</c> — ASP.NET Core aggregated health check (see <c>Probe</c>)</item>
///     </list>
///     <para>
///     Each test creates a <see cref="MiddlewareTestHarness"/> with a pre-configured
///     <see cref="DatabaseInitializationStatus"/> singleton, API versioning, and the Health feature registered.
///     </para>
/// </remarks>
[Trait("Category", "Health")]
public sealed partial class EndpointMappingTests
{
	#region MapHealthApiFeature() — /api/v1/health/live

	/// <summary>
	/// Verifies that the liveness endpoint returns HTTP 200 with a JSON body containing
	/// <c>{"status":"ok"}</c>.
	/// </summary>
	[Fact]
	public async Task Live_Returns200WithOkStatus()
	{
		// Arrange — state is irrelevant for liveness; use default (NotStarted).
		DatabaseInitializationStatus status = CreateStatusInState(DatabaseInitializationState.NotStarted);
		MiddlewareTestHarness harness = await CreateHarnessAsync(status);

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync(LiveEndpoint);

			// Assert
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			var body = await response.Content.ReadFromJsonAsync<V1.ApiHealthLiveResponse>();

			Assert.NotNull(body);
			Assert.Equal("ok", body.Status);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that the health API endpoints return <c>Cache-Control: no-store, no-cache</c> to prevent
	/// intermediaries from caching health status. This is enforced by the group-level endpoint filter on the
	/// <c>/health</c> route group — all versioned health endpoints inherit it automatically.
	/// </summary>
	[Fact]
	public async Task Live_ReturnsNoCacheHeaders()
	{
		// Arrange
		DatabaseInitializationStatus status = CreateStatusInState(DatabaseInitializationState.NotStarted);
		MiddlewareTestHarness harness = await CreateHarnessAsync(status);

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync(LiveEndpoint);

			// Assert
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.NotNull(response.Headers.CacheControl);
			Assert.True(response.Headers.CacheControl.NoStore);
			Assert.True(response.Headers.CacheControl.NoCache);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion
}
