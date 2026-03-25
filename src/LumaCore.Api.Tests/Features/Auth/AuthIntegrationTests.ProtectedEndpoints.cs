// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using LumaCore.Api.Features.Auth;

using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

using Xunit;

using V1 = LumaCore.Api.Contracts.V1.Auth;

namespace LumaCore.Api.Tests.Features.Auth;

// Protected endpoints: from unauthenticated rejection through authenticated access.
//
// These tests verify the JWT bearer middleware's interaction with RequireAuthorization()
// on the whoami and introspect endpoints:
//
//   1. No token: request without Authorization header → 401.
//   2. Invalid token: malformed Bearer value → 401 (OnAuthenticationFailed fires).
//   3. Valid token on whoami: authenticated request returns principal information.
//   4. Valid token on introspect: authenticated request returns token diagnostics.
//   5. Near-expiry token: 30-second clock skew saves a token that is past its exp claim → 200.
//   6. Expired token: once-valid token past expiry + 30-second clock skew → 401.
public sealed partial class AuthIntegrationTests
{
	// --- 1. No token ---

	/// <summary>
	/// Verifies that <c>GET /api/v1/auth/whoami</c> without an <c>Authorization</c> header returns
	/// <see cref="HttpStatusCode.Unauthorized"/>.
	/// </summary>
	[Fact]
	public async Task WhoAmI_WithoutToken_Returns401()
	{
		// Arrange
		var harness = await TestHarness.CreateAsync();

		try
		{
			// Act — no Authorization header.
			HttpResponseMessage response = await harness.Client.GetAsync("/api/v1/auth/whoami");

			// Assert
			await AssertUnauthorizedProblemDetailsAsync(response);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Invalid token ---

	/// <summary>
	/// Verifies that <c>GET /api/v1/auth/whoami</c> with a malformed Bearer token returns
	/// <see cref="HttpStatusCode.Unauthorized"/>, exercising the <c>OnAuthenticationFailed</c> event
	/// wired up in <see cref="ServiceRegistration"/>.
	/// </summary>
	[Fact]
	public async Task WhoAmI_WithInvalidToken_Returns401()
	{
		// Arrange
		var harness = await TestHarness.CreateAsync();

		try
		{
			HttpRequestMessage request = CreateAuthorizedRequest(
				HttpMethod.Get,
				"/api/v1/auth/whoami",
				"this-is-not-a-valid-jwt");

			// Act
			HttpResponseMessage response = await harness.Client.SendAsync(request);

			// Assert
			await AssertUnauthorizedProblemDetailsAsync(response);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. WhoAmI with valid token ---

	/// <summary>
	/// Verifies that <c>GET /api/v1/auth/whoami</c> with a valid Bearer token returns
	/// <see cref="HttpStatusCode.OK"/> and a <see cref="V1.AuthWhoAmIResponse"/> containing
	/// the authenticated principal's name and roles.
	/// </summary>
	[Fact]
	public async Task WhoAmI_WithValidToken_Returns200WithPrincipalInfo()
	{
		// Arrange
		var harness = await TestHarness.CreateAsync();

		try
		{
			string token = await LoginAsync(harness.Client);

			// Act
			HttpRequestMessage request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/auth/whoami", token);
			HttpResponseMessage response = await harness.Client.SendAsync(request);

			// Assert
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.Content.ReadFromJsonAsync<V1.AuthWhoAmIResponse>();
			Assert.NotNull(body);
			AssertWhoAmIResponse(body);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 4. Introspect with valid token ---

	/// <summary>
	/// Verifies that <c>GET /api/v1/auth/introspect</c> with a valid Bearer token returns
	/// <see cref="HttpStatusCode.OK"/> and an <see cref="V1.AuthIntrospectResponse"/> with
	/// populated token diagnostics (subject, issuer, audience, expiry).
	/// </summary>
	[Fact]
	public async Task Introspect_WithValidToken_ReturnsTokenDetails()
	{
		// Arrange
		var harness = await TestHarness.CreateAsync();

		try
		{
			string token = await LoginAsync(harness.Client);

			// Act - the token is valid, so we should get diagnostics about it.
			HttpRequestMessage request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/auth/introspect", token);
			HttpResponseMessage response = await harness.Client.SendAsync(request);

			// Assert - the introspect response should reflect the token's claims and metadata.
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.Content.ReadFromJsonAsync<V1.AuthIntrospectResponse>();
			Assert.NotNull(body);
			AssertIntrospectResponse(body, harness.TimeProvider);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 5. Near-expiry token (clock skew saves it) ---

	/// <summary>
	/// Verifies that <c>GET /api/v1/auth/whoami</c> still succeeds when the token's <c>exp</c>
	/// claim has technically passed but the request falls within the 30-second
	/// <see cref="TokenValidationParameters.ClockSkew"/> configured in
	/// <see cref="ServiceRegistration.AddAuthFeatureCore"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The token is issued at T₀ with <c>exp = T₀ + 60 min</c>. Advancing time by
	///     60 min 15 sec places "now" at T₀ + 60:15 — 15 seconds past <c>exp</c> but still
	///     within the 30-second clock skew window (<c>now − skew = T₀ + 59:45 ≤ exp</c>).
	///     </para>
	///     <para>
	///     This is the boundary counterpart to <see cref="WhoAmI_WithExpiredToken_Returns401"/>,
	///     which advances past the skew window.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task WhoAmI_WithTokenNearExpiry_Returns200()
	{
		// Arrange
		var harness = await TestHarness.CreateAsync();

		try
		{
			string token = await LoginAsync(harness.Client);

			// Advance 60 min 15 sec: 15 sec past exp, but within the 30-sec clock skew.
			// LifetimeValidator check: now(T₀+60:15) − skew(30s) = T₀+59:45 ≤ exp(T₀+60) → valid.
			TimeSpan delta = TimeSpan.FromMinutes(TestAccessTokenLifetimeMinutes) + TimeSpan.FromSeconds(15);
			harness.TimeProvider.Advance(delta);

			// Act - the token is technically expired, but the clock skew should keep it valid.
			HttpRequestMessage request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/auth/whoami", token);
			HttpResponseMessage response = await harness.Client.SendAsync(request);

			// Assert — clock skew keeps the token alive.
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.Content.ReadFromJsonAsync<V1.AuthWhoAmIResponse>();
			Assert.NotNull(body);
			AssertWhoAmIResponse(body);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 6. Expired token ---

	/// <summary>
	/// Verifies that <c>GET /api/v1/auth/whoami</c> with a Bearer token whose <c>exp</c> claim
	/// has passed (including the 30-second <see cref="TokenValidationParameters.ClockSkew"/>)
	/// returns <see cref="HttpStatusCode.Unauthorized"/> with a ProblemDetails body.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The token is issued at T₀ and expires at T₀ + 60 min. The custom
	///     <see cref="TokenValidationParameters.LifetimeValidator"/> in the test harness uses
	///     <see cref="FakeTimeProvider"/> for the "now" check, so advancing time by 61 minutes
	///     (lifetime + 31 sec past the 30-sec clock skew) deterministically triggers expiry rejection.
	///     </para>
	///     <para>
	///     This is the boundary counterpart to <see cref="WhoAmI_WithTokenNearExpiry_Returns200"/>,
	///     which stays within the skew window.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task WhoAmI_WithExpiredToken_Returns401()
	{
		// Arrange
		var harness = await TestHarness.CreateAsync();

		try
		{
			// Login while the token is still valid (T₀).
			string token = await LoginAsync(harness.Client);

			// Sanity check: the token works before time advances.
			HttpRequestMessage preCheck = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/auth/whoami", token);
			HttpResponseMessage preCheckResponse = await harness.Client.SendAsync(preCheck);
			Assert.Equal(HttpStatusCode.OK, preCheckResponse.StatusCode);

			// Advance time past expiry + clock skew (30 sec) + 1 sec margin.
			TimeSpan span = TimeSpan.FromMinutes(TestAccessTokenLifetimeMinutes) + TimeSpan.FromSeconds(31);
			harness.TimeProvider.Advance(span);

			// Act - the token is now expired even with clock skew, so we should get a 401.
			HttpRequestMessage request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/auth/whoami", token);
			HttpResponseMessage response = await harness.Client.SendAsync(request);

			// Assert
			await AssertUnauthorizedProblemDetailsAsync(response);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
