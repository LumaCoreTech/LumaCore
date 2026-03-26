// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Cors;
using LumaCore.Api.Tests.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

using Xunit;

namespace LumaCore.Api.Tests.Features.Cors;

public sealed partial class CorsIntegrationTests
{
	/// <summary>
	/// The origin used as the "matching" origin across all tests that configure specific origins.
	/// </summary>
	private const string TestOrigin = "https://example.com";

	/// <summary>
	/// An origin that is not in the allowed list, used to verify non-matching requests are rejected.
	/// </summary>
	private const string NonMatchingOrigin = "https://evil.com";

	/// <summary>
	/// The relative URL of the probe endpoint mapped by the test harness.
	/// </summary>
	private const string ProbeEndpoint = "/probe";

	/// <summary>
	/// Creates a <see cref="MiddlewareTestHarness"/> configured with CORS services and a minimal probe endpoint.
	/// </summary>
	/// <param name="configure">
	/// Optional delegate to customize the <see cref="CorsOptions"/> beyond the defaults. When
	/// <see langword="null"/>, the harness uses a minimal enabled configuration with <see cref="TestOrigin"/>
	/// as the sole allowed origin.
	/// </param>
	/// <returns>A disposable harness ready for HTTP requests.</returns>
	private static async Task<MiddlewareTestHarness> CreateHarnessAsync(
		Action<Dictionary<string, string?>>? configure = null)
	{
		var config = new Dictionary<string, string?>
		{
			["Cors:Enabled"] = "true",
			["Cors:AllowedOrigins:0"] = TestOrigin
		};

		configure?.Invoke(config);

		return await MiddlewareTestHarness.CreateAsync(
				       builder =>
				       {
					       builder.Configuration.AddInMemoryCollection(config);
					       builder.Services.AddCorsFeatureCore(builder.Configuration);
				       },
				       app =>
				       {
					       app.UseCorsFeature();
					       app.UseRouting();
					       app.MapGet(ProbeEndpoint, () => Results.Ok()).AllowAnonymous();
				       })
			       .ConfigureAwait(false);
	}

	/// <summary>
	/// Creates a simple cross-origin GET request to the probe endpoint with the specified <c>Origin</c> header.
	/// </summary>
	/// <param name="origin">The value for the <c>Origin</c> request header.</param>
	/// <returns>A configured <see cref="HttpRequestMessage"/>.</returns>
	private static HttpRequestMessage CreateSimpleRequest(string origin)
	{
		var request = new HttpRequestMessage(HttpMethod.Get, ProbeEndpoint);
		request.Headers.Add("Origin", origin);
		return request;
	}

	/// <summary>
	/// Creates a CORS preflight (OPTIONS) request to the probe endpoint with the required preflight headers.
	/// </summary>
	/// <param name="origin">The value for the <c>Origin</c> request header.</param>
	/// <param name="method">The HTTP method being requested, sent as <c>Access-Control-Request-Method</c>.</param>
	/// <param name="header">
	/// An optional header name sent as <c>Access-Control-Request-Headers</c>. When <see langword="null"/>, the
	/// header is omitted.
	/// </param>
	/// <returns>A configured <see cref="HttpRequestMessage"/> for a preflight request.</returns>
	private static HttpRequestMessage CreatePreflightRequest(string origin, string method, string? header = null)
	{
		var request = new HttpRequestMessage(HttpMethod.Options, ProbeEndpoint);
		request.Headers.Add("Origin", origin);
		request.Headers.Add("Access-Control-Request-Method", method);

		if (header is not null)
			request.Headers.Add("Access-Control-Request-Headers", header);

		return request;
	}

	/// <summary>
	/// Asserts that the response does not contain any CORS-related headers, confirming that the CORS middleware
	/// did not process the request.
	/// </summary>
	/// <param name="response">The HTTP response to inspect.</param>
	private static void AssertNoCorsHeaders(HttpResponseMessage response)
	{
		Assert.False(
			response.Headers.Contains("Access-Control-Allow-Origin"),
			"Expected no Access-Control-Allow-Origin header, but one was present.");
		Assert.False(
			response.Headers.Contains("Access-Control-Allow-Credentials"),
			"Expected no Access-Control-Allow-Credentials header, but one was present.");
		Assert.False(
			response.Headers.Contains("Access-Control-Allow-Methods"),
			"Expected no Access-Control-Allow-Methods header, but one was present.");
		Assert.False(
			response.Headers.Contains("Access-Control-Allow-Headers"),
			"Expected no Access-Control-Allow-Headers header, but one was present.");
		Assert.False(
			response.Headers.Contains("Access-Control-Expose-Headers"),
			"Expected no Access-Control-Expose-Headers header, but one was present.");
		Assert.False(
			response.Headers.Contains("Access-Control-Max-Age"),
			"Expected no Access-Control-Max-Age header, but one was present.");
	}
}
