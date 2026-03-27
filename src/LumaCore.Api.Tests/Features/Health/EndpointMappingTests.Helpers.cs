// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.Health;
using LumaCore.Api.Tests.Infrastructure;
using LumaCore.Data.Initialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LumaCore.Api.Tests.Features.Health;

public sealed partial class EndpointMappingTests
{
	/// <summary>
	/// The versioned endpoint path for the liveness probe.
	/// </summary>
	private const string LiveEndpoint = "/api/v1/health/live";

	/// <summary>
	/// The versioned endpoint path for the readiness probe.
	/// </summary>
	private const string ReadyEndpoint = "/api/v1/health/ready";

	/// <summary>
	/// The infrastructure endpoint path for the ASP.NET Core aggregated health check.
	/// </summary>
	private const string ProbeEndpoint = "/health";

	/// <summary>
	/// Standard failure message used when configuring <see cref="DatabaseInitializationStatus"/> in the
	/// <see cref="DatabaseInitializationState.Failed"/> state.
	/// </summary>
	private const string TestFailureMessage = "Test failure";

	/// <summary>
	/// Standard failure message used when configuring <see cref="DatabaseInitializationStatus"/> in the
	/// <see cref="DatabaseInitializationState.Disconnected"/> state.
	/// </summary>
	private const string TestDisconnectMessage = "Test disconnect";

	/// <summary>
	/// Creates a <see cref="MiddlewareTestHarness"/> with the Health feature, API versioning, and a
	/// pre-configured <see cref="DatabaseInitializationStatus"/> singleton.
	/// </summary>
	/// <param name="initStatus">
	/// The <see cref="DatabaseInitializationStatus"/> instance to register as a singleton. Configure the desired
	/// state on this instance <em>before</em> passing it — the harness registers it as-is in DI.
	/// </param>
	/// <returns>A disposable harness ready for HTTP requests against the health endpoints.</returns>
	private static Task<MiddlewareTestHarness> CreateHarnessAsync(DatabaseInitializationStatus initStatus)
	{
		return MiddlewareTestHarness.CreateAsync(
			builder =>
			{
				builder.Services.AddSingleton(initStatus);
				builder.AddApiVersioningFeature();
				builder.AddHealthFeature();
			},
			app =>
			{
				app.UseRouting();

				RouteGroupBuilder api = app.MapVersionedApiGroup();
				api.MapHealthApiFeature();

				app.MapHealthProbesFeature();
			});
	}

	/// <summary>
	/// Creates a <see cref="DatabaseInitializationStatus"/> pre-configured to the specified
	/// <paramref name="state"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     State transitions follow the real lifecycle: <see cref="DatabaseInitializationState.Completed"/> passes
	///     through <see cref="DatabaseInitializationState.InProgress"/> first, and
	///     <see cref="DatabaseInitializationState.Disconnected"/> requires a prior
	///     <see cref="DatabaseInitializationState.Completed"/> (since only a connected database can disconnect).
	///     </para>
	///     <para>
	///     For <see cref="DatabaseInitializationState.Failed"/>, a single transient failure is recorded with
	///     <see cref="TestFailureMessage"/>. For <see cref="DatabaseInitializationState.Disconnected"/>, the
	///     message is <see cref="TestDisconnectMessage"/>.
	///     </para>
	/// </remarks>
	/// <param name="state">The target <see cref="DatabaseInitializationState"/>.</param>
	/// <returns>A new instance configured to the requested state.</returns>
	private static DatabaseInitializationStatus CreateStatusInState(DatabaseInitializationState state)
	{
		var status = new DatabaseInitializationStatus();

		switch (state)
		{
			case DatabaseInitializationState.NotStarted:
				break;

			case DatabaseInitializationState.InProgress:
				status.SetInProgress();
				break;

			case DatabaseInitializationState.Completed:
				status.SetInProgress();
				status.SetCompleted();
				break;

			case DatabaseInitializationState.Failed:
				status.SetInProgress();
				status.SetFailed(
					new InvalidOperationException(TestFailureMessage),
					TestFailureMessage,
					DatabaseFailureCategory.Transient);
				break;

			case DatabaseInitializationState.Disconnected:
				status.SetInProgress();
				status.SetCompleted();
				status.SetDisconnected(
					new InvalidOperationException(TestDisconnectMessage),
					TestDisconnectMessage);
				break;
		}

		return status;
	}

	/// <summary>
	/// Creates a <see cref="DatabaseInitializationStatus"/> that has exhausted all retry attempts by recording
	/// <see cref="DatabaseInitializationStatus.MaxConsecutiveFailures"/> consecutive transient failures.
	/// </summary>
	/// <remarks>
	/// After <see cref="DatabaseInitializationStatus.MaxConsecutiveFailures"/> failures,
	/// <see cref="DatabaseInitializationStatus.ShouldRetry"/> becomes <see langword="false"/> and the category
	/// is automatically escalated to <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	/// This exercises the "giving up" branch in <see cref="DatabaseInitializationHealthCheck.CheckHealthAsync"/>.
	/// </remarks>
	/// <returns>A new instance in the <see cref="DatabaseInitializationState.Failed"/> state with retries exhausted.</returns>
	private static DatabaseInitializationStatus CreateFailedGivingUpStatus()
	{
		var status = new DatabaseInitializationStatus();
		status.SetInProgress();

		// Record MaxConsecutiveFailures to trigger automatic escalation.
		for (int i = 0; i < DatabaseInitializationStatus.MaxConsecutiveFailures; i++)
		{
			status.SetFailed(
				new InvalidOperationException(TestFailureMessage),
				TestFailureMessage,
				DatabaseFailureCategory.Transient);
		}

		return status;
	}
}
