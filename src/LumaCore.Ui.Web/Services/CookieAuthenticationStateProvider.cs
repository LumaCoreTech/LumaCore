// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

using LumaCore.Api.Contracts.V1.Auth;

using Microsoft.AspNetCore.Components.Authorization;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// Provides Blazor authentication state by querying the server-side <c>/api/v1/auth/whoami</c> endpoint.
/// </summary>
/// <remarks>
///     <para>
///     Unlike the former <c>JwtAuthenticationStateProvider</c> that parsed the JWT client-side from browser storage,
///     this provider relies on the <c>HttpOnly</c> cookie being sent automatically by the browser. The server
///     validates the cookie and returns identity information via <c>GET /api/v1/auth/whoami</c>.
///     </para>
///     <para>
///     The authentication state is cached in memory after the first successful fetch. Call
///     <see cref="NotifyStateChanged"/> after login or logout to invalidate the cache and trigger a re-fetch from
///     the server, which updates all <c>AuthorizeView</c> components and other authorization-dependent UI elements.
///     </para>
///     <para>
///     On initial page load with a persistent <c>HttpOnly</c> cookie, the provider detects the existing session
///     automatically — no manual re-authentication is required.
///     </para>
/// </remarks>
public sealed class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
	/// <summary>
	/// An anonymous <see cref="AuthenticationState"/> with no identity, returned when the user is not authenticated.
	/// </summary>
	private static readonly AuthenticationState sAnonymousState =
		new(new ClaimsPrincipal(new ClaimsIdentity()));

	private readonly HttpClient mHttpClient;

	/// <summary>
	/// Cached authentication state. Set to <see langword="null"/> to force a re-fetch from the server on the next
	/// <see cref="GetAuthenticationStateAsync"/> call.
	/// </summary>
	private AuthenticationState? mCachedState;

	/// <summary>
	/// Initializes a new instance of the <see cref="CookieAuthenticationStateProvider"/> class.
	/// </summary>
	/// <param name="httpClient">
	/// The HTTP client used to call the <c>/api/v1/auth/whoami</c> endpoint. Must be configured with
	/// <see cref="CookieCredentialHandler"/> so the browser includes the <c>HttpOnly</c> authentication cookie.
	/// </param>
	public CookieAuthenticationStateProvider(HttpClient httpClient)
	{
		mHttpClient = httpClient;
	}

	/// <inheritdoc/>
	public override async Task<AuthenticationState> GetAuthenticationStateAsync()
	{
		if (mCachedState is not null)
			return mCachedState;

		mCachedState = await FetchAuthenticationStateAsync().ConfigureAwait(false);
		return mCachedState;
	}

	/// <summary>
	/// Invalidates the cached authentication state and notifies Blazor that the state has changed.
	/// </summary>
	/// <remarks>
	/// Call this method after a successful login or logout to update all <c>AuthorizeView</c> components
	/// and other authorization-dependent UI elements. The next call to <see cref="GetAuthenticationStateAsync"/>
	/// will re-fetch the state from the server.
	/// </remarks>
	public void NotifyStateChanged()
	{
		mCachedState = null;
		NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
	}

	/// <summary>
	/// Queries <c>GET /api/v1/auth/whoami</c> and builds a <see cref="ClaimsPrincipal"/> from the response.
	/// </summary>
	/// <returns>
	/// An authenticated <see cref="AuthenticationState"/> if the server returns user information;
	/// otherwise, an anonymous state.
	/// </returns>
	private async Task<AuthenticationState> FetchAuthenticationStateAsync()
	{
		try
		{
			using HttpResponseMessage response = await mHttpClient
				                                     .GetAsync("api/v1/auth/whoami")
				                                     .ConfigureAwait(false);

			// If the server returns 401 Unauthorized, it means the cookie is missing or invalid — treat as unauthenticated.
			if (response.StatusCode == HttpStatusCode.Unauthorized)
				return sAnonymousState;

			// For any other non-success status code, also treat as unauthenticated (e.g., 500 Internal Server Error).
			if (!response.IsSuccessStatusCode)
				return sAnonymousState;

			AuthWhoAmIResponse? whoami = await response.Content
				                             .ReadFromJsonAsync<AuthWhoAmIResponse>()
				                             .ConfigureAwait(false);

			// If the response body is missing or cannot be deserialized, treat as unauthenticated.
			// This is a safeguard against unexpected server responses.
			// The server should always return a valid JSON body for authenticated requests.
			if (whoami is null)
				return sAnonymousState;

			return BuildAuthenticationState(whoami);
		}
		catch
		{
			// Network errors, serialization failures, etc. — treat as unauthenticated.
			return sAnonymousState;
		}
	}

	/// <summary>
	/// Builds an authenticated <see cref="AuthenticationState"/> from the server's <c>/whoami</c> response.
	/// </summary>
	/// <param name="whoami">The identity information returned by the server.</param>
	/// <returns>An <see cref="AuthenticationState"/> with the user's claims populated.</returns>
	private static AuthenticationState BuildAuthenticationState(AuthWhoAmIResponse whoami)
	{
		var claims = new List<Claim>();

		// Add the user name as the primary identity claim.
		if (!string.IsNullOrEmpty(whoami.Name))
			claims.Add(new Claim(ClaimTypes.Name, whoami.Name));

		// Add role claims for Blazor's role-based authorization (AuthorizeView Roles="...").
		foreach (string role in whoami.Roles)
		{
			claims.Add(new Claim(ClaimTypes.Role, role));
		}

		// Carry over all raw claims from the server response.
		foreach (AuthClaimItem claim in whoami.Claims)
		{
			claims.Add(new Claim(claim.Type, claim.Value));
		}

		// "cookie" as the authentication type signals to Blazor that the identity is authenticated.
		var identity = new ClaimsIdentity(claims, "cookie");
		return new AuthenticationState(new ClaimsPrincipal(identity));
	}
}
