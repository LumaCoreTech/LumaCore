// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Tests.Infrastructure;

using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace LumaCore.Api.Tests.Features.ErrorHandling;

// Status code mapping: from bare HTTP error codes to structured ProblemDetails.
//
// These tests verify that UseErrorHandlingFeature() intercepts error status codes
// on /api/* paths and converts them into RFC 7807 ProblemDetails responses with
// LumaCore-specific error type URNs:
//
//   1. Known status codes: all 18 mapped codes (400–503) produce ProblemDetails with
//      the correct error type URN, title, and detail
//      (KnownStatusCode_ReturnsMappedProblemDetails).
//
//   2. Unknown status codes: unmapped codes (e.g., 418) produce ProblemDetails with
//      framework defaults instead of custom URNs
//      (UnknownStatusCode_ReturnsDefaultProblemDetails).
//
//   3. Non-API path bypass: requests outside /api/* are not transformed, preserving
//      Blazor SPA fallback behavior (NonApiPath_DoesNotTransformResponse).
//
// For exception handling (unhandled exceptions → 500), see LumaCoreExceptionHandlerTests.
// For DI registration, see ServiceRegistrationTests.

/// <summary>
/// Integration tests for <see cref="MiddlewareIntegration.UseErrorHandlingFeature"/> and the private
/// <c>MapStatusCodeToErrorInfo()</c> method it delegates to.
/// </summary>
/// <remarks>
///     <para>
///     These tests exercise the <c>UseStatusCodePages</c> middleware configured by
///     <see cref="MiddlewareIntegration.UseErrorHandlingFeature"/>. Each test sends an HTTP request to an
///     endpoint that returns a bare status code (no body), and verifies that the middleware rewrites the
///     response into a structured RFC 7807 <see cref="ProblemDetails"/> with the correct LumaCore error type
///     URN.
///     </para>
///     <para>
///     The test harness uses <see cref="MiddlewareTestHarness"/> with two dynamic endpoints:
///     <c>GET /api/status/{code}</c> (API path — subject to mapping) and <c>GET /status/{code}</c>
///     (non-API path — bypassed).
///     </para>
/// </remarks>
[Trait("Category", "ErrorHandling")]
public sealed partial class StatusCodeMappingIntegrationTests
{
	// --- 1. Known status codes ---

	/// <summary>
	/// Verifies that each known HTTP error status code produces a <see cref="ProblemDetails"/> response
	/// with the correct LumaCore error type URN, title, and detail message.
	/// </summary>
	/// <param name="scenario">A human-readable label for test runner output.</param>
	/// <param name="statusCode">The HTTP status code to return from the test endpoint.</param>
	/// <param name="expectedType">The expected <see cref="ErrorTypes"/> URN in the response.</param>
	/// <param name="expectedTitle">The expected title in the <see cref="ProblemDetails"/> response.</param>
	/// <param name="expectedDetail">The expected detail message in the <see cref="ProblemDetails"/> response.</param>
	[Theory]
	[MemberData(nameof(KnownStatusCodeData))]
	public async Task UseErrorHandlingFeature_KnownStatusCode_ReturnsMappedProblemDetails(
		string scenario,
		int    statusCode,
		string expectedType,
		string expectedTitle,
		string expectedDetail)
	{
		_ = scenario; // Used for test runner display only.

		// Arrange
		MiddlewareTestHarness harness = await CreateHarnessAsync();

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync($"{ApiStatusEndpoint}/{statusCode}");

			// Assert
			ProblemDetails problem =
				await ProblemDetailsAssert.ReadAndAssertAsync(response, (HttpStatusCode)statusCode);

			Assert.Equal(expectedType, problem.Type);
			Assert.Equal(expectedTitle, problem.Title);
			Assert.Equal(expectedDetail, problem.Detail);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Unknown status codes ---

	/// <summary>
	/// Verifies that an unmapped status code (418 I'm a Teapot) produces a <see cref="ProblemDetails"/>
	/// response without a LumaCore error type URN, falling back to framework defaults.
	/// </summary>
	[Fact]
	public async Task UseErrorHandlingFeature_UnknownStatusCode_ReturnsDefaultProblemDetails()
	{
		// Arrange
		MiddlewareTestHarness harness = await CreateHarnessAsync();

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync($"{ApiStatusEndpoint}/418");

			// Assert
			Assert.Equal((HttpStatusCode)418, response.StatusCode);
			Assert.Equal(
				"application/problem+json",
				response.Content.Headers.ContentType?.MediaType);

			var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

			Assert.NotNull(problem);
			Assert.Equal(418, problem.Status);

			// 418 has no entry in MapStatusCodeToErrorInfo() — the type must NOT be a LumaCore URN.
			// ProblemDetailsDefaults may fill in framework defaults (title, type) for standard codes.
			Assert.True(
				problem.Type is null ||
				!problem.Type.StartsWith("urn:lumacore:", StringComparison.Ordinal),
				$"Expected no LumaCore URN for unmapped status 418, but got: {problem.Type}");
			Assert.Null(problem.Detail);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Non-API path bypass ---

	/// <summary>
	/// Verifies that error status codes on non-API paths (<c>/status/*</c>) are not transformed into
	/// <see cref="ProblemDetails"/> responses, preserving Blazor SPA fallback behavior.
	/// </summary>
	[Fact]
	public async Task UseErrorHandlingFeature_NonApiPath_DoesNotTransformResponse()
	{
		// Arrange
		MiddlewareTestHarness harness = await CreateHarnessAsync();

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync($"{NonApiStatusEndpoint}/404");

			// Assert
			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

			// Non-API paths should not receive a ProblemDetails body.
			string body = await response.Content.ReadAsStringAsync();
			Assert.Equal("", body);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
