// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;

using LumaCore.Api.Features.Health;
using LumaCore.Api.Tests.Infrastructure;
using LumaCore.Data.Initialization;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Xunit;

namespace LumaCore.Api.Tests.Features.Health;

public sealed partial class EndpointMappingTests
{
	/// <summary>
	/// Test data for the infrastructure health probe covering all <see cref="DatabaseInitializationState"/> values
	/// and the <see cref="DatabaseInitializationStatus.ShouldRetry"/> branch inside
	/// <see cref="DatabaseInitializationHealthCheck"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The default ASP.NET Core health check response writer returns a plain-text aggregate status:
	///     </para>
	///     <list type="bullet">
	///         <item><see cref="HealthStatus.Healthy"/> → HTTP 200 <c>"Healthy"</c></item>
	///         <item><see cref="HealthStatus.Degraded"/> → HTTP 200 <c>"Degraded"</c></item>
	///         <item><see cref="HealthStatus.Unhealthy"/> → HTTP 503 <c>"Unhealthy"</c></item>
	///     </list>
	///     <para>Row descriptions:</para>
	///     <list type="bullet">
	///         <item><b>Completed</b> — Healthy → 200.</item>
	///         <item><b>InProgress</b> — Degraded → 200.</item>
	///         <item><b>NotStarted</b> — Degraded → 200.</item>
	///         <item><b>FailedRetrying</b> — single transient failure, <c>ShouldRetry = true</c> → Unhealthy → 503.</item>
	///         <item>
	///         <b>FailedGivingUp</b> — max failures reached, <c>ShouldRetry = false</c> → Unhealthy → 503.
	///         Same HTTP output as FailedRetrying but exercises the "giving up" branch in
	///         <see cref="DatabaseInitializationHealthCheck.CheckHealthAsync"/>.
	///         </item>
	///         <item><b>Disconnected</b> — Unhealthy → 503.</item>
	///     </list>
	/// </remarks>
	public static TheoryData<string, DatabaseInitializationStatus, int, string> ProbeStateData => new()
	{
		// Completed: database healthy → 200 "Healthy".
		{
			"Completed",
			CreateStatusInState(DatabaseInitializationState.Completed),
			200,
			"Healthy"
		},

		// InProgress: initialization running → 200 "Degraded".
		{
			"InProgress",
			CreateStatusInState(DatabaseInitializationState.InProgress),
			200,
			"Degraded"
		},

		// NotStarted: initialization hasn't begun → 200 "Degraded".
		{
			"NotStarted",
			CreateStatusInState(DatabaseInitializationState.NotStarted),
			200,
			"Degraded"
		},

		// FailedRetrying: single transient failure, ShouldRetry = true → 503 "Unhealthy".
		{
			"FailedRetrying",
			CreateStatusInState(DatabaseInitializationState.Failed),
			503,
			"Unhealthy"
		},

		// FailedGivingUp: max failures reached, ShouldRetry = false → 503 "Unhealthy".
		// Same HTTP output but exercises the "giving up" branch in CheckHealthAsync().
		{
			"FailedGivingUp",
			CreateFailedGivingUpStatus(),
			503,
			"Unhealthy"
		},

		// Disconnected: runtime connection loss → 503 "Unhealthy".
		{
			"Disconnected",
			CreateStatusInState(DatabaseInitializationState.Disconnected),
			503,
			"Unhealthy"
		}
	};

	/// <summary>
	/// Verifies that the infrastructure health probe returns the correct HTTP status code and aggregate
	/// health text for each <see cref="DatabaseInitializationState"/>, including the
	/// <see cref="DatabaseInitializationStatus.ShouldRetry"/> branch variation for the
	/// <see cref="DatabaseInitializationState.Failed"/> state.
	/// </summary>
	/// <param name="scenario">A human-readable label identifying the database state scenario.</param>
	/// <param name="initStatus">
	/// A pre-configured <see cref="DatabaseInitializationStatus"/> instance representing the database state
	/// under test.
	/// </param>
	/// <param name="expectedStatusCode">The expected HTTP status code (200 or 503).</param>
	/// <param name="expectedBody">The expected plain-text response body ("Healthy", "Degraded", or "Unhealthy").</param>
	[Theory]
	[MemberData(nameof(ProbeStateData))]
	public async Task Probe_ReturnsExpectedHealthStatusForState(
		string                       scenario,
		DatabaseInitializationStatus initStatus,
		int                          expectedStatusCode,
		string                       expectedBody)
	{
		_ = scenario; // Only used for test case identification in test runner output.

		// Arrange
		MiddlewareTestHarness harness = await CreateHarnessAsync(initStatus);

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync(ProbeEndpoint);

			// Assert
			Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);

			string body = await response.Content.ReadAsStringAsync();
			Assert.Equal(expectedBody, body);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
