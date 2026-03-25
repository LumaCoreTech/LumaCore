// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text.Json;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Features.Validation;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace LumaCore.Api.Tests.Features.ApiVersioning;

public sealed partial class VersionedApiGroupIntegrationTests
{
	/// <summary>
	/// The header name used by the API versioning middleware to report supported versions.
	/// </summary>
	private const string ApiSupportedVersionsHeader = "api-supported-versions";

	/// <summary>
	/// Builds a minimal <see cref="WebApplication"/> backed by <see cref="TestServer"/>, registers API versioning
	/// services, maps the versioned route group, and adds a GET and POST probe endpoint for testing.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The application is intentionally lightweight — no database, no Serilog, no authentication. It registers
	///     only the services needed to exercise the versioned route group infrastructure:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             API versioning (
	///             <see cref="Api.Features.ApiVersioning.ServiceRegistration.AddApiVersioningFeatureCore"/>)
	///             </description>
	///         </item>
	///         <item>
	///             <description>ProblemDetails (for structured error responses from the validation filter)</description>
	///         </item>
	///     </list>
	///     <para>
	///     Two probe endpoints are mapped on the versioned group:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description><c>GET /api/v1/probe</c> — returns 200 OK (for header inspection)</description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>POST /api/v1/probe</c> — accepts a <see cref="ValidatedProbeRequest"/> body (for validation
	///             filter testing)
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
		builder.Services.AddApiVersioningFeatureCore();
		builder.Services.AddProblemDetails();

		WebApplication app = builder.Build();

		RouteGroupBuilder api = app.MapVersionedApiGroup();

		api.MapGet("/probe", () => Results.Ok(new { Status = "OK" }))
			.MapToApiVersion(ApiVersions.V1)
			.AllowAnonymous();

		api.MapPost("/probe", (ValidatedProbeRequest request) => Results.Ok(request))
			.MapToApiVersion(ApiVersions.V1)
			.AllowAnonymous();

		await app.StartAsync().ConfigureAwait(false);
		return new TestHarness(app.GetTestClient(), app);
	}

	/// <summary>
	/// Asserts that the <paramref name="response"/> body contains a <c>type</c> field equal to
	/// <see cref="Api.Features.ErrorHandling.ErrorTypes.Validation"/>, confirming the <c>400</c> was produced by the
	/// <see cref="ValidationFilter"/> rather than by other middleware.
	/// </summary>
	/// <remarks>
	/// This is a lightweight assertion for integration tests. The complete <c>ProblemDetails</c> envelope
	/// (title, status, field-level errors) is verified in <c>ValidationFilterTests</c>.
	/// </remarks>
	/// <param name="response">The HTTP response whose body to inspect.</param>
	private static async Task AssertValidationTypeUrnAsync(HttpResponseMessage response)
	{
		using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

		// Only the type URN is checked here — it is the most discriminating field because
		// Results.ValidationProblem() in the ValidationFilter is the only code path that sets
		// ErrorTypes.Validation. The remaining ProblemDetails fields (title, status, errors)
		// are generic ASP.NET Core defaults and would not prove which component produced the
		// 400. Full envelope verification lives in ValidationFilterTests.AssertValidationProblemDetailsAsync().
		Assert.True(
			doc.RootElement.TryGetProperty("type", out JsonElement typeElement),
			"ProblemDetails missing 'type' property.");
		Assert.Equal(ErrorTypes.Validation, typeElement.GetString());
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
