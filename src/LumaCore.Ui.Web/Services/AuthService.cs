// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using LumaCore.Api.Contracts.V1.Auth;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// Provides authentication operations (login and logout) for the LumaCore Blazor WASM UI.
/// </summary>
/// <remarks>
///     <para>
///     This service delegates credential exchange and session management to the backend API. The server sets an
///     <c>HttpOnly</c> cookie on successful login and clears it on logout — the browser handles cookie storage
///     and transmission transparently. No tokens are stored in JavaScript-accessible storage, eliminating
///     XSS-based token theft.
///     </para>
///     <para>
///     The <c>rememberMe</c> flag controls cookie persistence on the server side: when <see langword="true"/>,
///     the server issues a persistent cookie that survives browser restarts; when <see langword="false"/>, a
///     session cookie is issued that is cleared when the browser closes.
///     </para>
/// </remarks>
public sealed class AuthService
{
	private readonly HttpClient mHttpClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthService"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client used to communicate with the backend API.</param>
	public AuthService(HttpClient httpClient)
	{
		mHttpClient = httpClient;
	}

	/// <summary>
	/// Attempts to authenticate the user with the provided credentials.
	/// </summary>
	/// <param name="username">The username to authenticate with.</param>
	/// <param name="password">The password to authenticate with.</param>
	/// <param name="rememberMe">
	/// If <see langword="true"/>, the server sets a persistent <c>HttpOnly</c> cookie that survives browser
	/// restarts. If <see langword="false"/>, a session cookie is set that is cleared when the browser closes.
	/// </param>
	/// <returns>
	/// A <see cref="LoginResult"/> indicating whether the login was successful and containing any error message.
	/// </returns>
	public async Task<LoginResult> LoginAsync(string username, string password, bool rememberMe)
	{
		try
		{
			var request = new LoginRequest
			{
				Username = username,
				Password = password,
				RememberMe = rememberMe
			};

			using HttpResponseMessage response = await mHttpClient
				                                     .PostAsJsonAsync("api/v1/auth/login", request)
				                                     .ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				return response.StatusCode switch
				{
					HttpStatusCode.Unauthorized => new LoginResult(false, "Invalid username or password."),
					HttpStatusCode.ServiceUnavailable => new LoginResult(false, "Backend is unavailable."),
					var _ => new LoginResult(false, $"Login failed with status {(int)response.StatusCode}.")
				};
			}

			// The server sets an HttpOnly cookie automatically — no client-side token storage needed.
			return new LoginResult(true, null);
		}
		catch (HttpRequestException)
		{
			return new LoginResult(false, "Could not connect to the backend.");
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			return new LoginResult(false, $"An unexpected error occurred: {ex.Message}");
		}
	}

	/// <summary>
	/// Logs the user out by calling the server-side logout endpoint.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if the server confirmed the logout (token revoked and cookie cleared);
	/// <see langword="false"/> if the server was unreachable or returned an error.
	/// </returns>
	/// <remarks>
	///     <para>
	///     On success, the server revokes the current access token (recording its <c>jti</c> in the revocation
	///     blacklist) and clears the <c>HttpOnly</c> authentication cookie. Subsequent requests from this browser
	///     session are unauthenticated.
	///     </para>
	///     <para>
	///     On failure, the caller should inform the user that logout could not be completed. The <c>HttpOnly</c>
	///     cookie cannot be cleared client-side, so the session remains active until the token expires naturally.
	///     </para>
	/// </remarks>
	public async Task<bool> LogoutAsync()
	{
		try
		{
			using HttpResponseMessage response = await mHttpClient
				                                     .PostAsync("api/v1/auth/logout", content: null)
				                                     .ConfigureAwait(false);

			// The server handles token revocation and cookie clearing. We consider logout successful if the server
			// confirms the request with a success status code. The client cannot directly clear the HttpOnly
			// cookie, so we rely on the server's response to determine if the logout process was completed.
			return response.IsSuccessStatusCode;
		}
		catch
		{
			// On any exception (network failure, server error, etc.), we treat logout as unsuccessful.
			// The client cannot clear the HttpOnly cookie directly, so we cannot guarantee the session
			// is terminated without a successful server response.
			return false;
		}
	}
}

/// <summary>
/// Represents the result of a login attempt.
/// </summary>
/// <param name="Success">Indicates whether the login was successful.</param>
/// <param name="ErrorMessage">The error message if login failed; otherwise, <see langword="null"/>.</param>
public sealed record LoginResult(bool Success, string? ErrorMessage);
