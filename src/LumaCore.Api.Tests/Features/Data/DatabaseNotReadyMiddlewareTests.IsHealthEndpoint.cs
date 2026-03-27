// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;

using LumaCore.Api.Features.Data;
using LumaCore.Api.Tests.Infrastructure;

using Xunit;

namespace LumaCore.Api.Tests.Features.Data;

public sealed partial class DatabaseNotReadyMiddlewareTests
{
	/// <summary>
	/// Test data for health endpoint paths that should bypass the database readiness gate.
	/// </summary>
	/// <remarks>
	/// Each row contains a scenario description and the request path. All paths should receive 200 OK
	/// even when the database is not ready, because <see cref="DatabaseNotReadyMiddleware"/> treats them
	/// as health endpoints.
	/// </remarks>
	public static TheoryData<string, string> HealthEndpointPaths => new()
	{
		// Infrastructure health probes
		{ "infrastructure root", "/health" },
		{ "infrastructure ready", "/health/ready" },
		{ "infrastructure live", "/health/live" },

		// Versioned API health endpoints
		{ "versioned v1 health", "/api/v1/health" },
		{ "versioned v2 health live", "/api/v2/health/live" }
	};

	/// <summary>
	/// Verifies that health check endpoints always pass through to the next middleware, even when the database
	/// is not ready. This ensures monitoring systems can still query application status during initialization
	/// failures.
	/// </summary>
	/// <param name="scenario">A description of the health endpoint being tested (for test output readability).</param>
	/// <param name="path">The request path that should be recognized as a health endpoint.</param>
	[Theory]
	[MemberData(nameof(HealthEndpointPaths))]
	public async Task InvokeAsync_HealthEndpoint_WhenDatabaseNotReady_PassesThrough(string scenario, string path)
	{
		_ = scenario;

		// Arrange — default status is NotStarted (not ready).
		await using MiddlewareTestHarness harness = await CreateHarnessAsync();

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(path);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.False(response.Headers.Contains("Retry-After"));
	}

	/// <summary>
	/// Test data for API paths that look similar to health endpoints but should <b>not</b> be treated as such.
	/// </summary>
	/// <remarks>
	/// Each row contains a scenario description and the request path. These paths must be rejected with
	/// 503 when the database is not ready, confirming that <see cref="DatabaseNotReadyMiddleware"/> does
	/// not produce false positives.
	/// </remarks>
	public static TheoryData<string, string> NonHealthApiPaths => new()
	{
		// "health" is NOT the third segment — it's the fourth (after api/v1/users/).
		{ "health-records under users", "/api/v1/users/health-records" },

		// The probe endpoint is a regular API endpoint, not a health endpoint.
		{ "regular API probe", "/api/v1/probe" }
	};

	/// <summary>
	/// Verifies that API paths containing "health" in non-health positions are <b>not</b> treated as health
	/// endpoints. These requests must be rejected with 503 when the database is not ready.
	/// </summary>
	/// <param name="scenario">A description of the false-positive case being tested (for test output readability).</param>
	/// <param name="path">The request path that should <b>not</b> be recognized as a health endpoint.</param>
	[Theory]
	[MemberData(nameof(NonHealthApiPaths))]
	public async Task InvokeAsync_NonHealthApiPath_WhenDatabaseNotReady_Returns503(string scenario, string path)
	{
		_ = scenario;

		// Arrange — default status is NotStarted (not ready).
		await using MiddlewareTestHarness harness = await CreateHarnessAsync();

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(path);

		// Assert
		Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
	}
}
