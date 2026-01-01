// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net;
using System.Net.Http.Json;

using LumaCore.Api.Contracts.V1.Auth;

using Microsoft.JSInterop;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// Provides authentication services for the LumaCore Web UI, including login, logout, and token management.
/// </summary>
/// <remarks>
///     <para>
///     This service handles JWT token storage using browser storage APIs. The storage location depends on the
///     user's "Remember Me" preference: <c>localStorage</c> for persistent sessions that survive browser restarts,
///     or <c>sessionStorage</c> for sessions that end when the browser is closed.
///     </para>
///     <para>
///     The service also provides methods to retrieve the current token for use in authenticated API requests
///     and to check if the user is currently authenticated.
///     </para>
/// </remarks>
public sealed class AuthService
{
	/// <summary>
	/// The key used to store the storage type preference in localStorage.
	/// </summary>
	private const string StorageTypeKey = "lumacore_storage_type";

	/// <summary>
	/// The storage type identifier for localStorage.
	/// </summary>
	private const string StorageTypeLocal = "local";

	/// <summary>
	/// The storage type identifier for sessionStorage.
	/// </summary>
	private const string StorageTypeSession = "session";

	/// <summary>
	/// The key used to store the JWT token in browser storage.
	/// </summary>
	private const string TokenStorageKey = "lumacore_token";

	private readonly HttpClient mHttpClient;
	private readonly IJSRuntime mJsRuntime;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthService"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client used to communicate with the backend API.</param>
	/// <param name="jsRuntime">The JavaScript runtime for accessing browser storage APIs.</param>
	public AuthService(HttpClient httpClient, IJSRuntime jsRuntime)
	{
		mHttpClient = httpClient;
		mJsRuntime = jsRuntime;
	}

	/// <summary>
	/// Retrieves the currently stored JWT token, if any.
	/// </summary>
	/// <returns>The JWT token if one is stored; otherwise, <see langword="null"/>.</returns>
	/// <remarks>
	/// Returns <see langword="null"/> if browser storage is unavailable (e.g., during SSR/prerendering).
	/// </remarks>
	public async Task<string?> GetTokenAsync()
	{
		try
		{
			// First, check which storage type was used (if any).
			string? storageType = await mJsRuntime
				                      .InvokeAsync<string?>("localStorage.getItem", StorageTypeKey)
				                      .ConfigureAwait(false);

			if (string.IsNullOrEmpty(storageType))
			{
				// No storage type recorded, check both (fallback for edge cases).
				string? token = await mJsRuntime
					                .InvokeAsync<string?>("localStorage.getItem", TokenStorageKey)
					                .ConfigureAwait(false);

				// If found in localStorage, return it.
				if (!string.IsNullOrEmpty(token))
					return token;

				// Otherwise, check sessionStorage.
				return await mJsRuntime
					       .InvokeAsync<string?>("sessionStorage.getItem", TokenStorageKey)
					       .ConfigureAwait(false);
			}

			// Use the recorded storage type.
			string storageMethod = storageType == StorageTypeLocal ? "localStorage" : "sessionStorage";
			return await mJsRuntime
				       .InvokeAsync<string?>(storageMethod + ".getItem", TokenStorageKey)
				       .ConfigureAwait(false);
		}
		catch (JSException)
		{
			// JS runtime not available (SSR, prerendering) - treat as not authenticated.
			return null;
		}
		catch (InvalidOperationException)
		{
			// JSInterop not ready - treat as not authenticated.
			return null;
		}
	}

	/// <summary>
	/// Checks whether the user is currently authenticated (has a valid token stored).
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if a token is stored; otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// This method only checks for token presence, not token validity. The token may have expired.
	/// </remarks>
	public async Task<bool> IsAuthenticatedAsync()
	{
		string? token = await GetTokenAsync().ConfigureAwait(false);
		return !string.IsNullOrEmpty(token);
	}

	/// <summary>
	/// Attempts to authenticate the user with the provided credentials.
	/// </summary>
	/// <param name="username">The username to authenticate with.</param>
	/// <param name="password">The password to authenticate with.</param>
	/// <param name="rememberMe">
	/// If <see langword="true"/>, the token is stored in <c>localStorage</c> (persistent across browser restarts).<br/>
	/// If <see langword="false"/>, the token is stored in <c>sessionStorage</c> (cleared when browser closes).
	/// </param>
	/// <returns>
	/// A <see cref="LoginResult"/> indicating whether the login was successful and containing any error message.
	/// </returns>
	public async Task<LoginResult> LoginAsync(string username, string password, bool rememberMe)
	{
		try
		{
			var request = new LoginRequest(username, password);

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

			// Parse the response to extract the token.
			LoginResponse? loginResponse = await response.Content
				                               .ReadFromJsonAsync<LoginResponse>()
				                               .ConfigureAwait(false);

			// Validate the response.
			if (loginResponse is null || string.IsNullOrEmpty(loginResponse.AccessToken))
			{
				return new LoginResult(false, "Invalid response from server.");
			}

			// Store the token in the appropriate storage based on "Remember Me" preference.
			bool stored = await StoreTokenAsync(loginResponse.AccessToken, rememberMe).ConfigureAwait(false);
			if (!stored)
			{
				return new LoginResult(false, "Could not store authentication token.");
			}

			// Login successful.
			return new LoginResult(true, null);
		}
		catch (HttpRequestException)
		{
			// Network or connection error.
			return new LoginResult(false, "Could not connect to the backend.");
		}
		catch (Exception ex)
		{
			// General error handling.
			return new LoginResult(false, $"An unexpected error occurred: {ex.Message}");
		}
	}

	/// <summary>
	/// Logs the user out by removing the stored token from browser storage.
	/// </summary>
	/// <remarks>
	/// This method performs best-effort cleanup. If browser storage is unavailable, the operation silently succeeds.
	/// </remarks>
	public async Task LogoutAsync()
	{
		try
		{
			// Try to remove from both storage types to ensure complete cleanup.
			await mJsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenStorageKey).ConfigureAwait(false);
			await mJsRuntime.InvokeVoidAsync("sessionStorage.removeItem", TokenStorageKey).ConfigureAwait(false);
			await mJsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageTypeKey).ConfigureAwait(false);
		}
		catch
		{
			// Best effort cleanup - if storage is unavailable, there's nothing to clean up anyway.
		}
	}

	/// <summary>
	/// Stores the JWT token in the appropriate browser storage.
	/// </summary>
	/// <param name="token">The JWT token to store.</param>
	/// <param name="rememberMe">
	/// If <see langword="true"/>, stores in <c>localStorage</c>; otherwise, stores in <c>sessionStorage</c>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the token was stored successfully; otherwise, <see langword="false"/>.
	/// </returns>
	private async Task<bool> StoreTokenAsync(string token, bool rememberMe)
	{
		try
		{
			if (rememberMe)
			{
				// Persistent storage - survives browser restart.
				await mJsRuntime.InvokeVoidAsync("localStorage.setItem", TokenStorageKey, token).ConfigureAwait(false);
				await mJsRuntime.InvokeVoidAsync("localStorage.setItem", StorageTypeKey, StorageTypeLocal)
					.ConfigureAwait(false);
			}
			else
			{
				// Session storage - cleared when browser closes.
				await mJsRuntime.InvokeVoidAsync("sessionStorage.setItem", TokenStorageKey, token)
					.ConfigureAwait(false);
				await mJsRuntime.InvokeVoidAsync("localStorage.setItem", StorageTypeKey, StorageTypeSession)
					.ConfigureAwait(false);
			}

			return true;
		}
		catch (JSException)
		{
			// JS runtime not available.
			return false;
		}
		catch (InvalidOperationException)
		{
			// JSInterop not ready.
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
