// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using LumaCore.Api.Tests.Infrastructure;
using LumaCore.Data.Initialization;

using Xunit;

using V1 = LumaCore.Api.Contracts.V1.Health;

namespace LumaCore.Api.Tests.Features.Health;

public sealed partial class EndpointMappingTests
{
	/// <summary>
	/// Test data for the readiness endpoint covering all five <see cref="DatabaseInitializationState"/> values.
	/// Each row defines the expected HTTP status code, aggregate status, and per-component health detail for the
	/// given state.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The expected components dictionary is structured to accommodate future subsystems (e.g., vector store,
	///     LLM backend). When a new component is added to the <c>/ready</c> endpoint, extend each row's dictionary
	///     with the new entry — the test method itself requires no changes.
	///     </para>
	///     <para>Row descriptions:</para>
	///     <list type="bullet">
	///         <item><b>Completed</b> — 200, aggregate "ready", database "ready", no message.</item>
	///         <item><b>InProgress</b> — 503, aggregate "degraded", database "initializing", progress message.</item>
	///         <item><b>NotStarted</b> — 503, aggregate "degraded", database "initializing", not-started message.</item>
	///         <item><b>Failed</b> — 503, aggregate "degraded", database "failed", failure message from status.</item>
	///         <item>
	///         <b>Disconnected</b> — 503, aggregate "degraded", database "disconnected", disconnect message from
	///         status.
	///         </item>
	///     </list>
	/// </remarks>
	public static TheoryData<string, DatabaseInitializationState, int, string,
			Dictionary<string, V1.ApiHealthComponentStatus>>
		ReadyStateData => new()
	{
		// Completed: all subsystems operational → 200 "ready".
		{
			"Completed",
			DatabaseInitializationState.Completed,
			200,
			"ready",
			new Dictionary<string, V1.ApiHealthComponentStatus>
			{
				["database"] = new("ready", null)
			}
		},

		// InProgress: database still initializing → 503 with progress message.
		{
			"InProgress",
			DatabaseInitializationState.InProgress,
			503,
			"degraded",
			new Dictionary<string, V1.ApiHealthComponentStatus>
			{
				["database"] = new("initializing", "Database initialization is in progress.")
			}
		},

		// NotStarted: initialization hasn't begun yet → 503 with not-started message.
		{
			"NotStarted",
			DatabaseInitializationState.NotStarted,
			503,
			"degraded",
			new Dictionary<string, V1.ApiHealthComponentStatus>
			{
				["database"] = new("initializing", "Database initialization has not started yet.")
			}
		},

		// Failed: single transient failure → 503 with FailureMessage from status.
		{
			"Failed",
			DatabaseInitializationState.Failed,
			503,
			"degraded",
			new Dictionary<string, V1.ApiHealthComponentStatus>
			{
				["database"] = new("failed", TestFailureMessage)
			}
		},

		// Disconnected: runtime connection loss → 503 with disconnect message from status.
		{
			"Disconnected",
			DatabaseInitializationState.Disconnected,
			503,
			"degraded",
			new Dictionary<string, V1.ApiHealthComponentStatus>
			{
				["database"] = new("disconnected", TestDisconnectMessage)
			}
		}
	};

	/// <summary>
	/// Verifies that the readiness endpoint returns the correct HTTP status code, aggregate status,
	/// and per-component health detail for each <see cref="DatabaseInitializationState"/>.
	/// </summary>
	/// <param name="scenario">A human-readable label for the test row.</param>
	/// <param name="state">The <see cref="DatabaseInitializationState"/> to configure before the request.</param>
	/// <param name="expectedStatusCode">The expected HTTP status code (200 or 503).</param>
	/// <param name="expectedAggregateStatus">The expected top-level <c>status</c> field ("ready" or "degraded").</param>
	/// <param name="expectedComponents">
	/// The expected per-component health detail. Each entry maps a subsystem name to its expected
	/// <see cref="V1.ApiHealthComponentStatus"/>. Currently contains only <c>"database"</c>, but is structured to
	/// accommodate future subsystems without test method changes.
	/// </param>
	[Theory]
	[MemberData(nameof(ReadyStateData))]
	public async Task Ready_ReturnsExpectedResponseForState(
		string                                          scenario,
		DatabaseInitializationState                     state,
		int                                             expectedStatusCode,
		string                                          expectedAggregateStatus,
		Dictionary<string, V1.ApiHealthComponentStatus> expectedComponents)
	{
		_ = scenario; // Used by xUnit test display name.

		// Arrange
		DatabaseInitializationStatus initStatus = CreateStatusInState(state);
		MiddlewareTestHarness harness = await CreateHarnessAsync(initStatus);

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync(ReadyEndpoint);

			// Assert
			Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);

			var body = await response.Content.ReadFromJsonAsync<V1.ApiHealthReadyResponse>();

			Assert.NotNull(body);
			Assert.Equal(expectedAggregateStatus, body.Status);
			Assert.Equal(expectedComponents.Count, body.Components.Count);

			foreach (KeyValuePair<string, V1.ApiHealthComponentStatus> expected in expectedComponents)
			{
				Assert.True(
					body.Components.TryGetValue(expected.Key, out V1.ApiHealthComponentStatus? actual),
					$"Component '{expected.Key}' not found in response.");

				Assert.Equal(expected.Value, actual);
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
