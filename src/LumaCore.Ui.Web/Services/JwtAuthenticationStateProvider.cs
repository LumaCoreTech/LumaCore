// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// Provides authentication state for Blazor components based on JWT tokens stored in browser storage.
/// </summary>
/// <remarks>
///     <para>
///     This provider reads the JWT token from the <see cref="AuthService"/> and parses its claims to build
///     a <see cref="ClaimsPrincipal"/> that Blazor's authorization components can use.
///     </para>
///     <para>
///     The provider also exposes a <see cref="NotifyStateChanged"/> method that should be called after
///     login or logout to update all components that depend on authentication state.
///     </para>
/// </remarks>
public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
	private readonly AuthService mAuthService;

	/// <summary>
	/// Initializes a new instance of the <see cref="JwtAuthenticationStateProvider"/> class.
	/// </summary>
	/// <param name="authService">The authentication service used to retrieve the current token.</param>
	public JwtAuthenticationStateProvider(AuthService authService)
	{
		mAuthService = authService;
	}

	/// <inheritdoc/>
	public override async Task<AuthenticationState> GetAuthenticationStateAsync()
	{
		// Retrieve the JWT token from the authentication service.
		// If no token is found, the user is considered anonymous.
		// ConfigureAwait(true) to resume on the original context (Blazor UI thread).
		string? token = await mAuthService.GetTokenAsync().ConfigureAwait(true);

		// If no token is present, return an anonymous principal.
		if (string.IsNullOrEmpty(token))
			return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

		// Parse the token to extract claims and create a ClaimsPrincipal.
		ClaimsPrincipal principal = ParseTokenToPrincipal(token);
		return new AuthenticationState(principal);
	}

	/// <summary>
	/// Notifies the authentication system that the authentication state has changed.
	/// </summary>
	/// <remarks>
	/// Call this method after a successful login or logout to update all <c>AuthorizeView</c> components
	/// and other authorization-dependent UI elements.
	/// </remarks>
	public void NotifyStateChanged()
	{
		NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
	}

	/// <summary>
	/// Parses a JWT token and extracts its claims into a <see cref="ClaimsPrincipal"/>.
	/// </summary>
	/// <param name="token">The JWT token to parse.</param>
	/// <returns>
	/// A <see cref="ClaimsPrincipal"/> containing the claims from the token, or an anonymous principal
	/// if the token is invalid or expired.
	/// </returns>
	private static ClaimsPrincipal ParseTokenToPrincipal(string token)
	{
		try
		{
			var handler = new JwtSecurityTokenHandler();

			// Validate that the token is a well-formed JWT.
			// If not, return an anonymous principal.
			if (!handler.CanReadToken(token))
				return new ClaimsPrincipal(new ClaimsIdentity());

			// Read the JWT token.
			JwtSecurityToken jwtToken = handler.ReadJwtToken(token);

			// Check if the token has expired.
			// If expired, return an anonymous principal.
			if (jwtToken.ValidTo < DateTime.UtcNow)
				return new ClaimsPrincipal(new ClaimsIdentity());

			// Map JWT claims to standard .NET claims.
			var claims = new List<Claim>();

			// Iterate through each claim in the JWT token.
			// Map known claim types to .NET equivalents.
			// Retain other claims as-is.
			// This mapping ensures compatibility with Blazor's authorization system.
			foreach (Claim claim in jwtToken.Claims)
			{
				// Map 'sub' to NameIdentifier.
				if (claim.Type == "sub")
				{
					claims.Add(new Claim(ClaimTypes.NameIdentifier, claim.Value));
				}
				// Map 'name' to Name.
				else if (claim.Type is "name" or ClaimTypes.Name)
				{
					claims.Add(new Claim(ClaimTypes.Name, claim.Value));
				}
				// Map 'role' to Role.
				else if (claim.Type is "role" or ClaimTypes.Role)
				{
					claims.Add(new Claim(ClaimTypes.Role, claim.Value));
				}
				else
				{
					// Keep other claims as-is.
					claims.Add(claim);
				}
			}

			// Create an identity with "jwt" as the authentication type.
			// This non-empty string indicates the user is authenticated.
			var identity = new ClaimsIdentity(claims, "jwt");
			return new ClaimsPrincipal(identity);
		}
		catch
		{
			// If token parsing fails for any reason, return an anonymous principal.
			return new ClaimsPrincipal(new ClaimsIdentity());
		}
	}
}
