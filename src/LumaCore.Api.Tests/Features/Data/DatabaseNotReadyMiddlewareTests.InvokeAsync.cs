// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;

using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Tests.Infrastructure;
using LumaCore.Data.Initialization;

using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace LumaCore.Api.Tests.Features.Data;

public sealed partial class DatabaseNotReadyMiddlewareTests
{
	// --- 1. Pass-through: database ready / non-API ---

	/// <summary>
	/// Verifies that an API request passes through to the next middleware when
	/// <see cref="DatabaseInitializationStatus.IsReady"/> is <see langword="true"/>.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_DatabaseReady_ApiRequest_ReturnsOk()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(CreateReadyStatus());

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(ApiProbeEndpoint);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.False(response.Headers.Contains("Retry-After"));
	}

	/// <summary>
	/// Verifies that a non-API request (e.g., <c>/probe</c>) passes through regardless of database state.
	/// The middleware only gates requests under the <c>/api/</c> prefix.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_NonApiRequest_WhenDatabaseNotReady_PassesThrough()
	{
		// Arrange — default status is NotStarted (not ready).
		await using MiddlewareTestHarness harness = await CreateHarnessAsync();

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(NonApiProbeEndpoint);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	// --- 2. Transient states (retryable, Retry-After present) ---

	/// <summary>
	/// Verifies that an API request returns 503 with <see cref="ErrorTypes.ServiceUnavailable"/> and a
	/// <c>Retry-After</c> header when the database state is <see cref="DatabaseInitializationState.NotStarted"/>.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_NotStarted_Returns503WithRetryAfter()
	{
		// Arrange — default status is NotStarted.
		await using MiddlewareTestHarness harness = await CreateHarnessAsync();

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(ApiProbeEndpoint);

		// Assert
		ProblemDetails problem = await ReadAndAssertProblemDetailsAsync(response);
		Assert.Equal(ErrorTypes.ServiceUnavailable, problem.Type);
		Assert.Equal("Service Starting", problem.Title);
		Assert.Equal("Database initialization has not started. The service is starting up.", problem.Detail);
		Assert.Equal("10", Assert.Single(response.Headers.GetValues("Retry-After")));
	}

	/// <summary>
	/// Verifies that an API request returns 503 with <see cref="ErrorTypes.ServiceUnavailable"/> and a
	/// <c>Retry-After</c> header when the database state is <see cref="DatabaseInitializationState.InProgress"/>.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_InProgress_Returns503WithRetryAfter()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(CreateInProgressStatus());

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(ApiProbeEndpoint);

		// Assert
		ProblemDetails problem = await ReadAndAssertProblemDetailsAsync(response);
		Assert.Equal(ErrorTypes.ServiceUnavailable, problem.Type);
		Assert.Equal("Service Starting", problem.Title);
		Assert.Equal("Database initialization is in progress. Please retry shortly.", problem.Detail);
		Assert.Equal("10", Assert.Single(response.Headers.GetValues("Retry-After")));
	}

	/// <summary>
	/// Verifies that an API request returns 503 with <see cref="ErrorTypes.ServiceUnavailable"/> and a
	/// <c>Retry-After</c> header when the database state is <see cref="DatabaseInitializationState.Disconnected"/>.
	/// The custom failure message is reflected in the <c>detail</c> field.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_Disconnected_Returns503WithRetryAfterAndCustomMessage()
	{
		// Arrange
		const string expectedMessage = "PostgreSQL connection timed out.";
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(CreateDisconnectedStatus(expectedMessage));

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(ApiProbeEndpoint);

		// Assert
		ProblemDetails problem = await ReadAndAssertProblemDetailsAsync(response);
		Assert.Equal(ErrorTypes.ServiceUnavailable, problem.Type);
		Assert.Equal("Service Temporarily Unavailable", problem.Title);
		Assert.Equal(expectedMessage, problem.Detail);
		Assert.Equal("10", Assert.Single(response.Headers.GetValues("Retry-After")));
	}

	/// <summary>
	/// Verifies that an API request returns 503 with <see cref="ErrorTypes.ServiceUnavailable"/> and a
	/// <c>Retry-After</c> header when the database state is <see cref="DatabaseInitializationState.Failed"/>
	/// with <see cref="DatabaseFailureCategory.Transient"/>.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_FailedTransient_Returns503WithRetryAfter()
	{
		// Arrange
		await using MiddlewareTestHarness harness =
			await CreateHarnessAsync(CreateFailedStatus(DatabaseFailureCategory.Transient));

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(ApiProbeEndpoint);

		// Assert
		ProblemDetails problem = await ReadAndAssertProblemDetailsAsync(response);
		Assert.Equal(ErrorTypes.ServiceUnavailable, problem.Type);
		Assert.Equal("Service Temporarily Unavailable", problem.Title);
		Assert.Equal("Initialization failed.", problem.Detail);
		Assert.Equal("10", Assert.Single(response.Headers.GetValues("Retry-After")));
	}

	// --- 3. Non-retryable failures (no Retry-After) ---

	/// <summary>
	/// Verifies that an API request returns 503 with <see cref="ErrorTypes.DatabaseConfigurationRequired"/>
	/// and <b>no</b> <c>Retry-After</c> header when the database state is
	/// <see cref="DatabaseInitializationState.Failed"/> with
	/// <see cref="DatabaseFailureCategory.ConfigurationRequired"/>.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_FailedConfigurationRequired_Returns503WithoutRetryAfter()
	{
		// Arrange
		const string expectedMessage = "AutoCreate is disabled and database does not exist.";
		await using MiddlewareTestHarness harness =
			await CreateHarnessAsync(
				CreateFailedStatus(DatabaseFailureCategory.ConfigurationRequired, expectedMessage));

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(ApiProbeEndpoint);

		// Assert
		ProblemDetails problem = await ReadAndAssertProblemDetailsAsync(response);
		Assert.Equal(ErrorTypes.DatabaseConfigurationRequired, problem.Type);
		Assert.Equal("Database Configuration Required", problem.Title);
		Assert.Equal(expectedMessage, problem.Detail);
		Assert.False(response.Headers.Contains("Retry-After"));
	}

	/// <summary>
	/// Verifies that an API request returns 503 with <see cref="ErrorTypes.DatabaseFailed"/> and <b>no</b>
	/// <c>Retry-After</c> header when the database state is <see cref="DatabaseInitializationState.Failed"/>
	/// with <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_FailedManualIntervention_Returns503WithoutRetryAfter()
	{
		// Arrange
		const string expectedMessage = "Migration failed and no backup was available.";
		await using MiddlewareTestHarness harness =
			await CreateHarnessAsync(
				CreateFailedStatus(
					DatabaseFailureCategory.ManualInterventionRequired,
					expectedMessage));

		// Act
		using HttpResponseMessage response = await harness.Client.GetAsync(ApiProbeEndpoint);

		// Assert
		ProblemDetails problem = await ReadAndAssertProblemDetailsAsync(response);
		Assert.Equal(ErrorTypes.DatabaseFailed, problem.Type);
		Assert.Equal("Database Error", problem.Title);
		Assert.Equal(expectedMessage, problem.Detail);
		Assert.False(response.Headers.Contains("Retry-After"));
	}
}
