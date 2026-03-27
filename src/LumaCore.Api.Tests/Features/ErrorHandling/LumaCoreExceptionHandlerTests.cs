// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;
using System.Net;
using System.Text.Json;

using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Tests.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace LumaCore.Api.Tests.Features.ErrorHandling;

// Exception handler: from thrown exception to structured 500 ProblemDetails.
//
// These tests verify that LumaCoreExceptionHandler converts unhandled exceptions into
// RFC 7807 ProblemDetails responses via a real HTTP pipeline:
//
//   1. Response structure: exception → 500 with ErrorTypes.Internal, title, instance,
//      and traceId extension (WhenEndpointThrows_Returns500ProblemDetails).
//
//   2. Trace correlation:
//      a) W3C traceparent header flows through to traceId via Activity.Current.Id
//         (WhenTraceparentHeaderIsPresent).
//      b) Without an Activity, the handler falls back to HttpContext.TraceIdentifier
//         (WhenNoActivityIsPresent). A pipeline middleware clears Activity.Current
//         before the exception handler runs, forcing the fallback path.
//
//   3. Security: exception message and type name are never leaked in the response body
//      (WhenEndpointThrows_DoesNotExposeExceptionDetails).
//
// For status code mapping (UseStatusCodePages), see StatusCodeMappingIntegrationTests.
// For DI registration, see ServiceRegistrationTests.

/// <summary>
/// Integration tests for <see cref="LumaCoreExceptionHandler"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify that <see cref="LumaCoreExceptionHandler.TryHandleAsync"/> converts unhandled
///     exceptions into RFC 7807 <see cref="ProblemDetails"/> responses with the correct status code, error
///     type URN, and trace correlation — without leaking exception details.
///     </para>
///     <para>
///     The test harness uses <see cref="MiddlewareTestHarness"/> with a throwing endpoint
///     (<c>GET /api/v1/throw</c>). The pipeline registers <see cref="LumaCoreExceptionHandler"/> via
///     <see cref="ServiceRegistration.AddErrorHandlingFeature"/> and activates it with
///     <c>UseExceptionHandler()</c>.
///     </para>
/// </remarks>
[Trait("Category", "ErrorHandling")]
public sealed class LumaCoreExceptionHandlerTests
{
	/// <summary>
	/// The endpoint path used by the test harness to trigger an unhandled exception.
	/// </summary>
	private const string ThrowEndpoint = "/api/v1/throw";

	/// <summary>
	/// A secret message embedded in the test exception to verify it does not leak into the response.
	/// </summary>
	private const string SecretExceptionMessage = "SECRET_EXCEPTION_MESSAGE_d8f3a2";

	// --- 1. Response structure ---

	/// <summary>
	/// Verifies that an unhandled exception produces a 500 <see cref="ProblemDetails"/> response with
	/// <see cref="ErrorTypes.Internal"/> type, the expected title, the request path as instance, and a
	/// <c>traceId</c> extension for log correlation.
	/// </summary>
	[Fact]
	public async Task TryHandleAsync_WhenEndpointThrows_Returns500ProblemDetails()
	{
		// Arrange
		MiddlewareTestHarness harness = await CreateHarnessAsync();

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync(ThrowEndpoint);

			// Assert — envelope (status code, content-type, deserialization) is verified by the helper.
			ProblemDetails problem =
				await ProblemDetailsAssert.ReadAndAssertAsync(response, HttpStatusCode.InternalServerError);

			Assert.Equal(ErrorTypes.Internal, problem.Type);
			Assert.Equal("An unexpected error occurred", problem.Title);
			Assert.Equal(ThrowEndpoint, problem.Instance);
			Assert.Null(problem.Detail);

			// Verify exactly one extension exists (traceId) with a non-empty value.
			KeyValuePair<string, object?> extension = Assert.Single(problem.Extensions);
			Assert.Equal("traceId", extension.Key);
			Assert.False(string.IsNullOrEmpty(extension.Value?.ToString()));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Trace correlation ---

	/// <summary>
	/// Verifies that when a W3C <c>traceparent</c> header is present on the request, the handler uses
	/// <c>Activity.Current.Id</c> (which inherits the trace-id from the header) as the <c>traceId</c>
	/// extension in the <see cref="ProblemDetails"/> response.
	/// </summary>
	[Fact]
	public async Task TryHandleAsync_WhenTraceparentHeaderIsPresent_UsesActivityIdAsTraceId()
	{
		// Arrange
		// Inject a known W3C trace-id via the standard traceparent header. ASP.NET Core creates an
		// Activity that inherits this trace-id, so Activity.Current?.Id will contain it.
		const string knownTraceId = "0af7651916cd43dd8448eb211c80319c";
		const string traceparent = $"00-{knownTraceId}-b7ad6b7169203331-01";

		MiddlewareTestHarness harness = await CreateHarnessAsync();

		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, ThrowEndpoint);
			request.Headers.Add("traceparent", traceparent);

			// Act
			HttpResponseMessage response = await harness.Client.SendAsync(request);

			// Assert
			ProblemDetails problem =
				await ProblemDetailsAssert.ReadAndAssertAsync(response, HttpStatusCode.InternalServerError);

			KeyValuePair<string, object?> extension = Assert.Single(problem.Extensions);
			Assert.Equal("traceId", extension.Key);

			// ProblemDetails.Extensions values deserialize as JsonElement via System.Text.Json.
			// The Activity.Id preserves our trace-id but generates a new span-id.
			string traceId = Assert.IsType<JsonElement>(extension.Value).GetString()!;
			Assert.StartsWith($"00-{knownTraceId}-", traceId);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when no <see cref="Activity"/> is present, the handler falls back to
	/// <see cref="HttpContext.TraceIdentifier"/> as the <c>traceId</c> extension in the
	/// <see cref="ProblemDetails"/> response.
	/// </summary>
	/// <remarks>
	/// ASP.NET Core's TestServer always creates an <see cref="Activity"/> for each request, so the
	/// <see langword="null"/>-fallback path is unreachable under normal conditions. This test inserts a
	/// middleware <em>before</em> <c>UseExceptionHandler()</c> that clears <c>Activity.Current</c> and sets
	/// a known <see cref="HttpContext.TraceIdentifier"/>. Because both values live in the same
	/// <see cref="AsyncLocal{T}"/> / request scope, they remain <see langword="null"/> / overridden when
	/// the exception handler reads them.
	/// </remarks>
	[Fact]
	public async Task TryHandleAsync_WhenNoActivityIsPresent_UsesHttpContextTraceIdentifier()
	{
		// Arrange
		const string expectedTraceId = "test-fallback-trace-id";

		// Build a pipeline with a middleware that clears Activity.Current and injects a known
		// TraceIdentifier before the exception handler runs. Activity.Current is AsyncLocal,
		// so setting it to null in this middleware persists through the entire downstream flow
		// — including the exception handler's TryHandleAsync() call.
		var harness = await MiddlewareTestHarness.CreateAsync(
			              builder =>
			              {
				              builder.Services.AddProblemDetails();
				              builder.AddErrorHandlingFeature();
			              },
			              app =>
			              {
				              app.Use((context, next) =>
				              {
					              Activity.Current?.Stop();
					              Activity.Current = null;
					              context.TraceIdentifier = expectedTraceId;
					              return next(context);
				              });

				              app.UseExceptionHandler();
				              app.UseRouting();
				              app.MapGet(
					              ThrowEndpoint,
					              string () => throw new InvalidOperationException(SecretExceptionMessage));
			              });

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync(ThrowEndpoint);

			// Assert
			ProblemDetails problem = await ProblemDetailsAssert.ReadAndAssertAsync(
				                         response,
				                         HttpStatusCode.InternalServerError);

			KeyValuePair<string, object?> extension = Assert.Single(problem.Extensions);
			Assert.Equal("traceId", extension.Key);

			// With Activity.Current cleared, the handler must fall back to TraceIdentifier.
			string traceId = Assert.IsType<JsonElement>(extension.Value).GetString()!;
			Assert.Equal(expectedTraceId, traceId);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Security ---

	/// <summary>
	/// Verifies that exception details (message, type name) are not exposed in the response body,
	/// preventing information disclosure to API clients.
	/// </summary>
	[Fact]
	public async Task TryHandleAsync_WhenEndpointThrows_DoesNotExposeExceptionDetails()
	{
		// Arrange
		MiddlewareTestHarness harness = await CreateHarnessAsync();

		try
		{
			// Act
			HttpResponseMessage response = await harness.Client.GetAsync(ThrowEndpoint);

			// Assert
			string body = await response.Content.ReadAsStringAsync();
			Assert.DoesNotContain(SecretExceptionMessage, body);
			Assert.DoesNotContain("InvalidOperationException", body);
			Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Creates a <see cref="MiddlewareTestHarness"/> with <see cref="LumaCoreExceptionHandler"/> registered
	/// and a minimal throwing endpoint at <see cref="ThrowEndpoint"/>.
	/// </summary>
	/// <returns>A disposable harness ready for HTTP requests.</returns>
	private static Task<MiddlewareTestHarness> CreateHarnessAsync()
	{
		return MiddlewareTestHarness.CreateAsync(
			builder =>
			{
				builder.Services.AddProblemDetails();
				builder.AddErrorHandlingFeature();
			},
			app =>
			{
				app.UseExceptionHandler();
				app.UseRouting();
				app.MapGet(
					ThrowEndpoint,
					string () => throw new InvalidOperationException(SecretExceptionMessage));
			});
	}
}
