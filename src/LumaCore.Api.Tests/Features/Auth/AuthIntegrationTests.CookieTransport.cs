// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

using Microsoft.Net.Http.Headers;

using Xunit;

using V1 = LumaCore.Api.Contracts.V1.Auth;

namespace LumaCore.Api.Tests.Features.Auth;

// Cookie transport: authentication via HttpOnly cookie instead of Bearer header.
//
// These tests verify the dual-auth mechanism wired up in OnMessageReceived:
// when no Authorization header is present, the JWT bearer handler extracts the
// token from the configured HttpOnly cookie.
//
//   1. Cookie issuance: login with cookie transport enabled sets a Set-Cookie header
//      with correct security attributes and a valid JWT as value. Tested with and
//      without RememberMe to verify session vs. persistent cookie behavior.
//   2. Cookie auth: a protected endpoint accepts the cookie token when no
//      Authorization header is present.
public sealed partial class AuthIntegrationTests
{
	// --- 1. Cookie issuance ---

	/// <summary>
	/// Verifies that <c>POST /api/v1/auth/login</c> with cookie transport enabled sets a
	/// <c>Set-Cookie</c> response header with the correct name, security attributes
	/// (<c>HttpOnly</c>, <c>SameSite=Strict</c>, <c>Path=/api</c>), and a value that is a valid JWT
	/// with the expected claims. The <c>Expires</c> attribute depends on <see cref="V1.LoginRequest.RememberMe"/>.
	/// </summary>
	/// <param name="caseName">A human-readable label for the test case.</param>
	/// <param name="rememberMe">
	/// When <see langword="true"/>, the cookie is persistent with an explicit <c>Expires</c> matching the
	/// configured access token lifetime. When <see langword="false"/>, a session cookie without
	/// <c>Expires</c> is expected.
	/// </param>
	[Theory]
	[InlineData("session cookie (default)", false)]
	[InlineData("persistent cookie (RememberMe)", true)]
	public async Task Login_WithCookieEnabled_SetsCorrectCookieAttributes(string caseName, bool rememberMe)
	{
		_ = caseName;

		// Arrange — enable cookie transport with a known cookie name.
		const string cookieName = "test-auth-cookie";
		var harness = await TestHarness.CreateAsync(cookieEnabled: true, cookieName: cookieName);

		try
		{
			var request = new V1.LoginRequest
			{
				Username = TestUsername,
				Password = TestPassword,
				RememberMe = rememberMe
			};

			// Act
			HttpResponseMessage response = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", request);

			// Assert
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			// Verify the JSON response body contains a valid access token.
			var body = await response.Content.ReadFromJsonAsync<V1.LoginResponse>();
			Assert.NotNull(body);

			// Verify the token is a valid JWT with correct claims — not just a non-empty string.
			DateTime issuedAtUtc = harness.TimeProvider.GetUtcNow().UtcDateTime;
			AssertTokenClaims(
				body.AccessToken,
				expectedSubject: TestUsername,
				expectedRoles: [TestRole],
				issuedAtUtc: issuedAtUtc);

			// Parse the Set-Cookie header and verify all security-relevant attributes.
			Assert.True(
				response.Headers.TryGetValues(HeaderNames.SetCookie, out IEnumerable<string>? cookies),
				"Response is missing the Set-Cookie header.");
			string setCookieRaw = Assert.Single(cookies);
			SetCookieHeaderValue setCookie = SetCookieHeaderValue.Parse(setCookieRaw);

			Assert.Equal(cookieName, setCookie.Name.ToString());
			Assert.Equal(body.AccessToken, setCookie.Value.ToString());
			Assert.True(setCookie.HttpOnly, "Cookie must be HttpOnly to prevent XSS access.");
			Assert.Equal(SameSiteMode.Strict, setCookie.SameSite);
			Assert.Equal("/api", setCookie.Path.ToString());

			// Expires depends on RememberMe: persistent cookie with explicit expiry matching
			// the configured token lifetime vs. session cookie without Expires.
			if (rememberMe)
			{
				DateTimeOffset expectedExpires = harness.TimeProvider
					.GetUtcNow()
					.AddMinutes(TestAccessTokenLifetimeMinutes);
				Assert.NotNull(setCookie.Expires);
				Assert.Equal(expectedExpires, setCookie.Expires.Value);
			}
			else
			{
				Assert.Null(setCookie.Expires);
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Cookie auth ---

	/// <summary>
	/// Verifies that a protected endpoint accepts authentication via an <c>HttpOnly</c> cookie when
	/// no <c>Authorization</c> header is present. This exercises the <c>OnMessageReceived</c> event
	/// that extracts the token from the cookie.
	/// </summary>
	[Fact]
	public async Task WhoAmI_WithCookieOnly_Returns200()
	{
		// Arrange — enable cookie transport and log in.
		const string cookieName = "test-auth-cookie";
		var harness = await TestHarness.CreateAsync(cookieEnabled: true, cookieName: cookieName);

		try
		{
			// Login to get the token via JSON response (we'll also use the cookie).
			string token = await LoginAsync(harness.Client);

			// Build a request with the cookie header but NO Authorization header.
			// This simulates a browser client that sends the HttpOnly cookie automatically.
			var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/whoami");
			request.Headers.Add("Cookie", $"{cookieName}={token}");

			// Act
			HttpResponseMessage response = await harness.Client.SendAsync(request);

			// Assert — the OnMessageReceived event extracts the token from the cookie.
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
}
