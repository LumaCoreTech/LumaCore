// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// A <see cref="DelegatingHandler"/> that ensures the browser includes credentials
/// (the <c>HttpOnly</c> authentication cookie) on every outgoing HTTP request.
/// </summary>
/// <remarks>
///     <para>
///     In Blazor WebAssembly, <see cref="HttpClient"/> uses the browser's <c>fetch</c> API internally. By default,
///     <c>fetch</c> only sends cookies for same-origin requests (<c>credentials: 'same-origin'</c>). This handler
///     sets <c>credentials: 'include'</c> on every request, ensuring the <c>HttpOnly</c> authentication cookie is
///     sent even in cross-origin deployments (e.g., a CDN-hosted SPA calling an API on a different subdomain).
///     </para>
///     <para>
///     This handler replaces the former <c>JwtAuthorizationHandler</c> that read the JWT from JavaScript-accessible
///     browser storage and attached it as a <c>Bearer</c> header — an approach vulnerable to XSS-based token theft.
///     With <c>HttpOnly</c> cookie transport, the token is never accessible to JavaScript.
///     </para>
/// </remarks>
public sealed class CookieCredentialHandler : DelegatingHandler
{
	/// <inheritdoc/>
	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken  cancellationToken)
	{
		// Instruct the browser's fetch API to include cookies (credentials: 'include') on this request.
		// This is required for the HttpOnly authentication cookie to be sent in cross-origin deployments.
		request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

		return base.SendAsync(request, cancellationToken);
	}
}
