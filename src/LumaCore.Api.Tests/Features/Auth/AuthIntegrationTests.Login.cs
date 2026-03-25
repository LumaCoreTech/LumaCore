// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using Xunit;

using V1 = LumaCore.Api.Contracts.V1.Auth;

namespace LumaCore.Api.Tests.Features.Auth;

// Login: from successful authentication through request validation and credential rejection.
//
// These tests verify the login endpoint's HTTP-level behavior through the full pipeline:
//
//   1. Happy path: valid admin credentials → 200 with a fully verified JWT.
//   2. Validation: malformed request body → 400 with validation errors (exercises ValidationFilter).
//   3. Rejection: invalid credentials → 401 without leaking token or identity information.
public sealed partial class AuthIntegrationTests
{
	// --- 1. Happy path ---

	/// <summary>
	/// Verifies that <c>POST /api/v1/auth/login</c> with valid admin credentials returns
	/// <see cref="HttpStatusCode.OK"/> and a JWT whose header, identity claims, configuration claims,
	/// temporal claims, and claim-type set exactly match the test harness configuration.
	/// </summary>
	[Fact]
	public async Task Login_WithValidCredentials_Returns200WithAccessToken()
	{
		// Arrange
		var harness = await TestHarness.CreateAsync();

		try
		{
			var request = new V1.LoginRequest
			{
				Username = TestUsername,
				Password = TestPassword
			};

			// Act
			HttpResponseMessage response = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", request);

			// Assert
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.Content.ReadFromJsonAsync<V1.LoginResponse>();
			Assert.NotNull(body);

			// Full JWT verification — header, claims, timestamps, and claim-type completeness.
			AssertTokenClaims(
				body.AccessToken,
				expectedSubject: TestUsername,
				expectedRoles: [TestRole],
				issuedAtUtc: harness.TimeProvider.GetUtcNow().UtcDateTime);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Validation ---

	/// <summary>
	/// Verifies that <c>POST /api/v1/auth/login</c> with an invalid request body (empty username) returns
	/// <see cref="HttpStatusCode.BadRequest"/>. This exercises the <c>ValidationFilter</c> which relies on
	/// data-annotation attributes being placed on the <b>properties</b> of <see cref="V1.LoginRequest"/>.
	/// </summary>
	[Fact]
	public async Task Login_WithInvalidRequestBody_Returns400()
	{
		// Arrange — send a request with an empty username, which violates the [Required] constraint.
		var harness = await TestHarness.CreateAsync();

		try
		{
			var request = new V1.LoginRequest
			{
				Username = "",
				Password = TestPassword
			};

			// Act
			HttpResponseMessage response = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", request);

			// Assert
			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

			string body = await response.Content.ReadAsStringAsync();
			Assert.Contains("Username", body, StringComparison.Ordinal);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Rejection ---

	/// <summary>
	/// Verifies that <c>POST /api/v1/auth/login</c> with invalid credentials returns
	/// <see cref="HttpStatusCode.Unauthorized"/> without exposing any token or identity information.
	/// </summary>
	[Fact]
	public async Task Login_WithInvalidCredentials_Returns401()
	{
		// Arrange
		var harness = await TestHarness.CreateAsync();

		try
		{
			var request = new V1.LoginRequest
			{
				Username = TestUsername,
				Password = "wrongpassword"
			};

			// Act
			HttpResponseMessage response = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", request);

			// Assert — UseStatusCodePages converts the handler's Results.Unauthorized() into a
			// structured ProblemDetails response. This implicitly verifies no token leaks: the body
			// is a ProblemDetails error, not a LoginResponse containing an accessToken.
			await AssertUnauthorizedProblemDetailsAsync(response);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
