// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using Xunit;

using V1 = LumaCore.Api.Contracts.V1.Auth;

namespace LumaCore.Api.Tests.Features.Auth;

// Logout: token revocation through the full pipeline.
//
// This test tells the complete authentication lifecycle story: starting from an
// unauthenticated request (401), through login and successful access (200), to
// logout (204) and subsequent rejection of the revoked token (401 again).
// The OnTokenValidated revocation check is what enforces the blacklist.
public sealed partial class AuthIntegrationTests
{
	/// <summary>
	/// Verifies the full logout lifecycle:
	/// <list type="number">
	///     <item>Access a protected endpoint without a token — rejected with <c>401 Unauthorized</c>.</item>
	///     <item>Login and obtain a valid access token.</item>
	///     <item>Use the token on a protected endpoint — succeeds with <c>200 OK</c>.</item>
	///     <item>Logout with the token — returns <c>204 No Content</c>.</item>
	///     <item>
	///     Re-use the same token — rejected with <c>401 Unauthorized</c> because the
	///     <c>OnTokenValidated</c> event checks the revocation blacklist.
	///     </item>
	/// </list>
	/// </summary>
	[Fact]
	public async Task Logout_WithValidToken_RevokesTokenAndSubsequentRequestFails()
	{
		// Arrange — cache disabled (CacheDurationSeconds=0) so revocation is visible immediately.
		var harness = await TestHarness.CreateAsync();

		try
		{
			// Pre-condition: without a token the endpoint is inaccessible.
			HttpResponseMessage unauthResponse = await harness.Client.GetAsync("/api/v1/auth/whoami");
			await AssertUnauthorizedProblemDetailsAsync(unauthResponse);
			Assert.Contains(unauthResponse.Headers.WwwAuthenticate, v => v.Scheme == "Bearer");

			// Login to get a valid token.
			string token = await LoginAsync(harness.Client);

			// Pre-condition: with a valid token the endpoint responds successfully.
			HttpRequestMessage preCheck = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/auth/whoami", token);
			HttpResponseMessage preCheckResponse = await harness.Client.SendAsync(preCheck);
			Assert.Equal(HttpStatusCode.OK, preCheckResponse.StatusCode);
			var preCheckBody = await preCheckResponse.Content.ReadFromJsonAsync<V1.AuthWhoAmIResponse>();
			Assert.NotNull(preCheckBody);
			AssertWhoAmIResponse(preCheckBody);

			// Act — logout revokes the token.
			HttpRequestMessage logoutRequest = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/auth/logout", token);
			HttpResponseMessage logoutResponse = await harness.Client.SendAsync(logoutRequest);

			// Assert — logout returns 204 with no content.
			Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
			Assert.Equal(0, logoutResponse.Content.Headers.ContentLength ?? 0);

			// Assert — the same token is now rejected on a protected endpoint.
			HttpRequestMessage postLogout = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/auth/whoami", token);
			HttpResponseMessage postLogoutResponse = await harness.Client.SendAsync(postLogout);
			await AssertUnauthorizedProblemDetailsAsync(postLogoutResponse);
			Assert.Contains(postLogoutResponse.Headers.WwwAuthenticate, v => v.Scheme == "Bearer");
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
