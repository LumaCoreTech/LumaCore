// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;

using LumaCore.Api.Features.Cors;
using LumaCore.Api.Tests.Infrastructure;

using Xunit;

namespace LumaCore.Api.Tests.Features.Cors;

// CORS middleware integration: from disabled through fully configured policies.
//
// These tests exercise the runtime behavior of UseCorsFeature() by sending actual HTTP requests
// through a TestServer-backed pipeline and inspecting the CORS response headers:
//
//   1. Disabled: Disabled_NoCorsHeaders() verifies no CORS headers appear when CORS is off.
//
//   2. Origin matching: SpecificOrigin_MatchingRequest_ReturnsOrigin() verifies matching
//      origins receive Access-Control-Allow-Origin, while
//      SpecificOrigin_NonMatchingRequest_NoCorsHeaders() confirms non-matching origins are
//      rejected. WildcardOrigin_ReturnsWildcard() covers the wildcard "*" case.
//
//   3. Credentials: AllowCredentials_ReturnsCredentialsHeader() verifies
//      Access-Control-Allow-Credentials is present when configured.
//
//   4. Preflight responses: SpecificMethods_PreflightReturnsConfiguredMethods() and
//      SpecificHeaders_PreflightReturnsConfiguredHeaders() verify configured values appear in
//      preflight responses. NoMethods_PreflightAllowsRequestedMethod() and
//      NoHeaders_PreflightAllowsRequestedHeader() confirm the allow-any fallback.
//
//   5. Exposed headers and max-age: ExposedHeaders_ReturnsExposeHeader() verifies
//      Access-Control-Expose-Headers, and PreflightMaxAge_ReturnsMaxAgeHeader() verifies
//      Access-Control-Max-Age in preflight responses.
//
// For CorsOptions validation tests, see CorsOptionsTests.Validation.
// For CorsOptions unit tests, see CorsOptionsTests.

/// <summary>
/// HTTP-level integration tests for <see cref="MiddlewareIntegration.UseCorsFeature"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify the runtime CORS behavior that is <b>not</b> observable through
///     <see cref="CorsOptions"/> validation alone — specifically the translation of configuration
///     values into actual HTTP response headers by ASP.NET Core's CORS middleware.
///     </para>
///     <para>
///     The test harness uses <see cref="MiddlewareTestHarness"/> with a minimal probe endpoint
///     (<c>GET /probe</c>). No database or authentication is required.
///     </para>
/// </remarks>
[Trait("Category", "Cors")]
public sealed partial class CorsIntegrationTests
{
	// --- 1. Disabled ---

	/// <summary>
	/// Verifies that no CORS headers are returned when <see cref="CorsOptions.Enabled"/> is
	/// <see langword="false"/>.
	/// </summary>
	[Fact]
	public async Task Disabled_NoCorsHeaders()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(config =>
		{
			config["Cors:Enabled"] = "false";
			config.Remove("Cors:AllowedOrigins:0");
		});

		using HttpRequestMessage request = CreateSimpleRequest(TestOrigin);

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		AssertNoCorsHeaders(response);
	}

	// --- 2. Origin matching ---

	/// <summary>
	/// Verifies that a matching origin receives the <c>Access-Control-Allow-Origin</c> header echoing the
	/// request origin.
	/// </summary>
	[Fact]
	public async Task SpecificOrigin_MatchingRequest_ReturnsOrigin()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync();

		using HttpRequestMessage request = CreateSimpleRequest(TestOrigin);

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		string origin = Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin"));
		Assert.Equal(TestOrigin, origin);
	}

	/// <summary>
	/// Verifies that a non-matching origin does not receive any CORS headers.
	/// </summary>
	[Fact]
	public async Task SpecificOrigin_NonMatchingRequest_NoCorsHeaders()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync();

		using HttpRequestMessage request = CreateSimpleRequest(NonMatchingOrigin);

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		AssertNoCorsHeaders(response);
	}

	/// <summary>
	/// Verifies that a wildcard origin configuration returns <c>Access-Control-Allow-Origin: *</c> for any
	/// request origin.
	/// </summary>
	[Fact]
	public async Task WildcardOrigin_ReturnsWildcard()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(config =>
		{
			config["Cors:AllowedOrigins:0"] = "*";
		});

		using HttpRequestMessage request = CreateSimpleRequest("https://any-origin.com");

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		string origin = Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin"));
		Assert.Equal("*", origin);
	}

	// --- 3. Credentials ---

	/// <summary>
	/// Verifies that <c>Access-Control-Allow-Credentials: true</c> is present when
	/// <see cref="CorsOptions.AllowCredentials"/> is enabled.
	/// </summary>
	[Fact]
	public async Task AllowCredentials_ReturnsCredentialsHeader()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(config =>
		{
			config["Cors:AllowCredentials"] = "true";
		});

		using HttpRequestMessage request = CreateSimpleRequest(TestOrigin);

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		string origin = Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin"));
		Assert.Equal(TestOrigin, origin);
		string credentials = Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials"));
		Assert.Equal("true", credentials);
	}

	// --- 4. Preflight: methods and headers ---

	/// <summary>
	/// Verifies that a preflight response includes the configured allowed methods in
	/// <c>Access-Control-Allow-Methods</c>.
	/// </summary>
	[Fact]
	public async Task SpecificMethods_PreflightReturnsConfiguredMethods()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(config =>
		{
			config["Cors:AllowedMethods:0"] = "GET";
			config["Cors:AllowedMethods:1"] = "POST";
		});

		using HttpRequestMessage request = CreatePreflightRequest(TestOrigin, "POST");

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		string methods = Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods"));
		Assert.Contains("GET", methods);
		Assert.Contains("POST", methods);
	}

	/// <summary>
	/// Verifies that when no methods are configured (empty list), the preflight response allows the requested
	/// method — confirming <c>AllowAnyMethod()</c> behavior.
	/// </summary>
	[Fact]
	public async Task NoMethods_PreflightAllowsRequestedMethod()
	{
		// Arrange — no AllowedMethods configured, so UseCorsFeature() calls AllowAnyMethod().
		await using MiddlewareTestHarness harness = await CreateHarnessAsync();

		using HttpRequestMessage request = CreatePreflightRequest(TestOrigin, "DELETE");

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.True(
			response.Headers.Contains("Access-Control-Allow-Methods"),
			"Expected Access-Control-Allow-Methods header in preflight response.");
	}

	/// <summary>
	/// Verifies that a preflight response includes the configured allowed headers in
	/// <c>Access-Control-Allow-Headers</c>.
	/// </summary>
	[Fact]
	public async Task SpecificHeaders_PreflightReturnsConfiguredHeaders()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(config =>
		{
			config["Cors:AllowedHeaders:0"] = "Content-Type";
			config["Cors:AllowedHeaders:1"] = "Authorization";
		});

		using HttpRequestMessage request = CreatePreflightRequest(TestOrigin, "GET", "Authorization");

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		string headers = Assert.Single(response.Headers.GetValues("Access-Control-Allow-Headers"));
		Assert.Contains("Content-Type", headers, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("Authorization", headers, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Verifies that when no headers are configured (empty list), the preflight response allows the requested
	/// header — confirming <c>AllowAnyHeader()</c> behavior.
	/// </summary>
	[Fact]
	public async Task NoHeaders_PreflightAllowsRequestedHeader()
	{
		// Arrange — no AllowedHeaders configured, so UseCorsFeature() calls AllowAnyHeader().
		await using MiddlewareTestHarness harness = await CreateHarnessAsync();

		using HttpRequestMessage request = CreatePreflightRequest(TestOrigin, "GET", "X-Custom-Header");

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.True(
			response.Headers.Contains("Access-Control-Allow-Headers"),
			"Expected Access-Control-Allow-Headers header in preflight response.");
	}

	// --- 5. Exposed headers and max-age ---

	/// <summary>
	/// Verifies that <c>Access-Control-Expose-Headers</c> includes the configured exposed headers on a simple
	/// cross-origin response.
	/// </summary>
	[Fact]
	public async Task ExposedHeaders_ReturnsExposeHeader()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(config =>
		{
			config["Cors:ExposedHeaders:0"] = "X-Request-Id";
			config["Cors:ExposedHeaders:1"] = "X-Correlation-Id";
		});

		using HttpRequestMessage request = CreateSimpleRequest(TestOrigin);

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		string exposed = Assert.Single(response.Headers.GetValues("Access-Control-Expose-Headers"));
		Assert.Contains("X-Request-Id", exposed, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("X-Correlation-Id", exposed, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Verifies that <c>Access-Control-Max-Age</c> is present in the preflight response with the configured
	/// value.
	/// </summary>
	[Fact]
	public async Task PreflightMaxAge_ReturnsMaxAgeHeader()
	{
		// Arrange
		await using MiddlewareTestHarness harness = await CreateHarnessAsync(config =>
		{
			config["Cors:PreflightMaxAge"] = "3600";
		});

		using HttpRequestMessage request = CreatePreflightRequest(TestOrigin, "GET");

		// Act
		using HttpResponseMessage response = await harness.Client.SendAsync(request);

		// Assert
		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		string maxAge = Assert.Single(response.Headers.GetValues("Access-Control-Max-Age"));
		Assert.Equal("3600", maxAge);
	}
}
