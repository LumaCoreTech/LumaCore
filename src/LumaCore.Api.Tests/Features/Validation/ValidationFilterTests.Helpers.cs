// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Text.Json;

using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Features.Validation;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace LumaCore.Api.Tests.Features.Validation;

public sealed partial class ValidationFilterTests
{
	/// <summary>
	/// Builds a minimal <see cref="WebApplication"/> backed by <see cref="TestServer"/>, registers
	/// <c>ProblemDetails</c> services, and maps probe endpoints with
	/// <see cref="ValidationExtensions.WithValidation{TBuilder}"/>
	/// applied directly — <b>without</b> API versioning infrastructure.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The application is intentionally minimal — no API versioning, no authentication, no database.
	///     This isolates the <see cref="ValidationFilter"/> behavior from all other middleware.
	///     </para>
	///     <para>
	///     Three probe endpoints are mapped:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <c>POST /probe</c> — accepts a <see cref="ValidatedProbeRequest"/> body (single required field).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>GET /probe/optional</c> — accepts an optional <c>[FromQuery]</c> parameter that resolves
	///             to <see langword="null"/> when omitted, exercising the filter's null-argument skip logic.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>POST /probe/multi</c> — accepts a <see cref="MultiFieldRequest"/> body (multiple required
	///             fields).
	///             </description>
	///         </item>
	///     </list>
	/// </remarks>
	/// <returns>
	/// A <see cref="TestHarness"/> wrapping the <see cref="HttpClient"/> and the backing
	/// <see cref="WebApplication"/>. The caller must dispose the harness after use.
	/// </returns>
	private static async Task<TestHarness> CreateTestHarnessAsync()
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddProblemDetails();

		WebApplication app = builder.Build();

		// Use a plain route group with WithValidation() — no API versioning.
		RouteGroupBuilder group = app.MapGroup("/").WithValidation();

		group.MapPost("/probe", (ValidatedProbeRequest request) => Results.Ok(request));

		// Optional query parameter — the filter must skip null arguments.
		group.MapGet(
			"/probe/optional",
			([FromQuery] string? name) =>
				Results.Ok(new { Received = name is not null }));

		group.MapPost("/probe/multi", (MultiFieldRequest request) => Results.Ok(request));

		await app.StartAsync().ConfigureAwait(false);
		return new TestHarness(app.GetTestClient(), app);
	}

	/// <summary>
	/// Asserts that the <paramref name="response"/> is a <c>400 Bad Request</c> with a complete
	/// <see cref="ProblemDetails"/> body containing the <see cref="ErrorTypes.Validation"/> type URN,
	/// the standard validation title, the <c>400</c> status field, and exactly
	/// <paramref name="expectedFieldCount"/> entries in the <c>errors</c> dictionary.
	/// </summary>
	/// <param name="response">The HTTP response to validate.</param>
	/// <param name="expectedFieldCount">The expected number of fields in the <c>errors</c> dictionary.</param>
	/// <returns>
	/// A tuple of the parsed <see cref="JsonDocument"/> and the <c>errors</c> <see cref="JsonElement"/> for
	/// test-specific field assertions. The caller must dispose the <see cref="JsonDocument"/>.
	/// </returns>
	private static async Task<(JsonDocument Document, JsonElement Errors)> AssertValidationProblemDetailsAsync(
		HttpResponseMessage response,
		int                 expectedFieldCount)
	{
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

		JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
		JsonElement root = doc.RootElement;

		// type — LumaCore-specific validation error URN.
		Assert.True(
			root.TryGetProperty("type", out JsonElement typeElement),
			"ProblemDetails missing 'type' property.");
		Assert.Equal(ErrorTypes.Validation, typeElement.GetString());

		// title — standard ASP.NET Core validation problem title.
		Assert.True(
			root.TryGetProperty("title", out JsonElement titleElement),
			"ProblemDetails missing 'title' property.");
		Assert.Equal("One or more validation errors occurred.", titleElement.GetString());

		// status — must match the HTTP status code in the JSON body.
		Assert.True(
			root.TryGetProperty("status", out JsonElement statusElement),
			"ProblemDetails missing 'status' property.");
		Assert.Equal(400, statusElement.GetInt32());

		// errors — field-level validation errors.
		Assert.True(
			root.TryGetProperty("errors", out JsonElement errorsElement),
			"ProblemDetails missing 'errors' property.");
		Assert.Equal(expectedFieldCount, errorsElement.EnumerateObject().Count());

		return (doc, errorsElement);
	}

	/// <summary>
	/// Wraps an <see cref="HttpClient"/> and the backing <see cref="WebApplication"/> so both are disposed
	/// together when the test completes.
	/// </summary>
	/// <param name="client">The <see cref="HttpClient"/> connected to the in-memory <see cref="TestServer"/>.</param>
	/// <param name="application">The <see cref="WebApplication"/> hosting the <see cref="TestServer"/>.</param>
	private sealed class TestHarness(HttpClient client, WebApplication application) : IDisposable
	{
		/// <summary>Gets the <see cref="HttpClient"/> connected to the in-memory <see cref="TestServer"/>.</summary>
		public HttpClient Client { get; } = client;

		/// <summary>
		/// Disposes the <see cref="HttpClient"/> and the backing <see cref="WebApplication"/>, releasing all
		/// resources held by the in-memory <see cref="TestServer"/>.
		/// </summary>
		public void Dispose()
		{
			Client.Dispose();
			((IDisposable)application).Dispose();
		}
	}
}
