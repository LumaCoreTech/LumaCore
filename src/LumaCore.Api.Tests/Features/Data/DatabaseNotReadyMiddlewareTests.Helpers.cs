// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using LumaCore.Api.Features.Data;
using LumaCore.Api.Tests.Infrastructure;
using LumaCore.Data.Initialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace LumaCore.Api.Tests.Features.Data;

public sealed partial class DatabaseNotReadyMiddlewareTests
{
	/// <summary>
	/// The relative URL of the probe endpoint mapped by the test harness. Placed under <c>/api/v1/</c> so the
	/// middleware treats it as an API request subject to the database readiness gate.
	/// </summary>
	private const string ApiProbeEndpoint = "/api/v1/probe";

	/// <summary>
	/// A non-API path used to verify that the middleware passes through requests outside the <c>/api/</c> prefix.
	/// </summary>
	private const string NonApiProbeEndpoint = "/probe";

	/// <summary>
	/// Creates a <see cref="MiddlewareTestHarness"/> with the <see cref="DatabaseNotReadyMiddleware"/> and a
	/// minimal probe endpoint, using the supplied <paramref name="status"/> as the singleton
	/// <see cref="DatabaseInitializationStatus"/>.
	/// </summary>
	/// <param name="status">
	/// A pre-configured <see cref="DatabaseInitializationStatus"/> instance. When <see langword="null"/>,
	/// a default (not-started) instance is registered.
	/// </param>
	/// <returns>A disposable harness ready for HTTP requests.</returns>
	private static Task<MiddlewareTestHarness> CreateHarnessAsync(DatabaseInitializationStatus? status = null)
	{
		status ??= new DatabaseInitializationStatus();

		return MiddlewareTestHarness.CreateAsync(
			builder => { builder.Services.AddSingleton(status); },
			app =>
			{
				app.UseDatabaseReadinessCheck();
				app.UseRouting();
				app.MapGet(ApiProbeEndpoint, () => Results.Ok("healthy"));
				app.MapGet(NonApiProbeEndpoint, () => Results.Ok("healthy"));

				// Health endpoints for IsHealthEndpoint() tests.
				app.MapGet("/health", () => Results.Ok("health"));
				app.MapGet("/health/ready", () => Results.Ok("ready"));
				app.MapGet("/health/live", () => Results.Ok("live"));
				app.MapGet("/api/v1/health", () => Results.Ok("api-health"));
				app.MapGet("/api/v2/health/live", () => Results.Ok("api-health-live"));

				// Non-health API endpoint that could be a false positive.
				app.MapGet("/api/v1/users/health-records", () => Results.Ok("records"));
			});
	}

	/// <summary>
	/// Creates a <see cref="DatabaseInitializationStatus"/> in the <see cref="DatabaseInitializationState.Completed"/>
	/// state (database ready).
	/// </summary>
	/// <returns>A status instance where <see cref="DatabaseInitializationStatus.IsReady"/> is <see langword="true"/>.</returns>
	private static DatabaseInitializationStatus CreateReadyStatus()
	{
		var status = new DatabaseInitializationStatus();
		status.SetCompleted();
		return status;
	}

	/// <summary>
	/// Creates a <see cref="DatabaseInitializationStatus"/> in the <see cref="DatabaseInitializationState.InProgress"/>
	/// state.
	/// </summary>
	/// <returns>A status instance representing an ongoing initialization.</returns>
	private static DatabaseInitializationStatus CreateInProgressStatus()
	{
		var status = new DatabaseInitializationStatus();
		status.SetInProgress();
		return status;
	}

	/// <summary>
	/// Creates a <see cref="DatabaseInitializationStatus"/> in the <see cref="DatabaseInitializationState.Disconnected"/>
	/// state with an optional custom failure message.
	/// </summary>
	/// <param name="message">
	/// An optional failure message. When <see langword="null"/>, a generic placeholder is used.
	/// </param>
	/// <returns>A status instance representing a runtime connection loss.</returns>
	private static DatabaseInitializationStatus CreateDisconnectedStatus(string? message = null)
	{
		var status = new DatabaseInitializationStatus();
		// SetDisconnected() only transitions from Completed, so we must complete first.
		status.SetCompleted();
		status.SetDisconnected(
			new InvalidOperationException("Connection lost"),
			message ?? "Database connection lost.");
		return status;
	}

	/// <summary>
	/// Creates a <see cref="DatabaseInitializationStatus"/> in the <see cref="DatabaseInitializationState.Failed"/>
	/// state with the specified <paramref name="category"/> and optional <paramref name="message"/>.
	/// </summary>
	/// <param name="category">The failure category determining the error type and retryability.</param>
	/// <param name="message">
	/// An optional failure message. When <see langword="null"/>, a generic placeholder is used.
	/// </param>
	/// <returns>A status instance representing a failed initialization.</returns>
	private static DatabaseInitializationStatus CreateFailedStatus(
		DatabaseFailureCategory category,
		string?                 message = null)
	{
		var status = new DatabaseInitializationStatus();
		status.SetFailed(
			new InvalidOperationException("Init failed"),
			message ?? "Initialization failed.",
			category);
		return status;
	}

	/// <summary>
	/// Reads the response body as a <see cref="ProblemDetails"/> instance and asserts common 503 properties.
	/// </summary>
	/// <param name="response">The HTTP response to parse.</param>
	/// <returns>The deserialized <see cref="ProblemDetails"/>.</returns>
	private static async Task<ProblemDetails> ReadAndAssertProblemDetailsAsync(HttpResponseMessage response)
	{
		Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
		Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

		ProblemDetails? problem = await response.Content
			                          .ReadFromJsonAsync<ProblemDetails>()
			                          .ConfigureAwait(false);

		Assert.NotNull(problem);
		Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);

		// Verify exactly one extension exists (no unintended data leakage) and that the traceId is non-empty.
		KeyValuePair<string, object?> extension = Assert.Single(problem.Extensions);
		Assert.Equal("traceId", extension.Key);
		Assert.False(string.IsNullOrEmpty(extension.Value?.ToString()));

		return problem;
	}
}
