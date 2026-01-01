// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net.Http.Headers;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// A <see cref="DelegatingHandler"/> that automatically attaches JWT bearer tokens to outgoing HTTP requests.
/// </summary>
/// <remarks>
///     <para>
///     This handler retrieves the JWT token from browser storage via <see cref="AuthService.GetTokenAsync"/>
///     and adds it as a Bearer token in the <c>Authorization</c> header for all outgoing requests.
///     </para>
///     <para>
///     If no token is available (user not authenticated), requests are sent without an Authorization header.
///     The backend will return <c>401 Unauthorized</c> for protected endpoints.
///     </para>
/// </remarks>
public sealed class JwtAuthorizationHandler : DelegatingHandler
{
	private readonly IServiceProvider mServiceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="JwtAuthorizationHandler"/> class.
	/// </summary>
	/// <param name="serviceProvider">
	/// The service provider used to resolve <see cref="AuthService"/>.
	/// We use <see cref="IServiceProvider"/> instead of direct injection to avoid circular dependencies,
	/// since <see cref="AuthService"/> depends on <see cref="HttpClient"/>.
	/// </param>
	public JwtAuthorizationHandler(IServiceProvider serviceProvider)
	{
		mServiceProvider = serviceProvider;
	}

	/// <inheritdoc/>
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken  cancellationToken)
	{
		// Resolve AuthService from the current scope.
		// We can't inject AuthService directly because it creates a circular dependency:
		// AuthService -> HttpClient -> JwtAuthorizationHandler -> AuthService
		var authService = mServiceProvider.GetRequiredService<AuthService>();

		// Get the JWT token from AuthService.
		string? token = await authService.GetTokenAsync().ConfigureAwait(false);

		// If a token is available, add it to the Authorization header.
		if (!string.IsNullOrEmpty(token))
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

		// Continue with the request.
		return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
