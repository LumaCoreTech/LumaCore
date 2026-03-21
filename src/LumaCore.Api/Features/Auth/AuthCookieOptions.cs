// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// Configures how access tokens are transported via HTTP cookies for browser-based clients (Blazor WASM).
/// </summary>
/// <remarks>
///     <para>
///     When <see cref="Enabled"/> is <see langword="true"/>, the login endpoint sets an <c>HttpOnly</c> cookie
///     containing the JWT in addition to returning it in the response body. Browser clients automatically send
///     the cookie on subsequent requests, eliminating the need to store the token in JavaScript-accessible storage
///     (<c>localStorage</c> / <c>sessionStorage</c>) and mitigating XSS-based token theft.
///     </para>
///     <para>
///     API clients (e.g., companion AIs, CLI tools) are unaffected — they continue to use the
///     <c>Authorization: Bearer</c> header. If both a cookie and a Bearer header are present, the Bearer header
///     takes priority.
///     </para>
///     <para>
///     CSRF protection is provided by the <c>SameSite=Strict</c> attribute, which is hardcoded and not configurable.
///     <c>Strict</c> prevents the browser from sending the cookie on any cross-origin request, providing robust CSRF
///     mitigation for LumaCore's SPA architecture. Combined with the existing CORS policy, this eliminates the most
///     common CSRF attack vectors without requiring additional anti-forgery tokens.
///     </para>
///     <para>
///     Options are bound from the configuration section identified by <see cref="SectionName"/>. With the current
///     value this corresponds to the <c>"Jwt:Cookie"</c> section in <c>appsettings.json</c> and environment
///     variables prefixed with <c>Jwt__Cookie__</c>.
///     </para>
/// </remarks>
sealed class AuthCookieOptions
{
	/// <summary>
	/// Gets the configuration section name used to bind <see cref="AuthCookieOptions"/>.
	/// </summary>
	public const string SectionName = "Jwt:Cookie";

	/// <summary>
	/// Gets or sets whether cookie-based token transport is enabled.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Default:</b> <see langword="true"/>.
	///     </para>
	///     <para>
	///     When disabled, the login endpoint returns the token only in the JSON response body. This is useful for
	///     deployments where only API clients are expected and cookie management is undesirable.
	///     </para>
	/// </remarks>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the name of the cookie.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Default:</b> <c>lumacore_access</c>.
	///     </para>
	///     <para>
	///     Choose a name that does not collide with other cookies on the same domain. Avoid generic names like
	///     <c>token</c> or <c>session</c>.
	///     </para>
	/// </remarks>
	[Required]
	public string Name { get; set; } = "lumacore_access";

	/// <summary>
	/// Gets or sets whether the cookie requires a secure (HTTPS) connection.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Default:</b> <see langword="true"/>.
	///     </para>
	///     <para>
	///     Must be <see langword="true"/> in production to prevent cookie transmission over unencrypted HTTP.
	///     Set to <see langword="false"/> in development when using plain HTTP (no TLS).
	///     </para>
	/// </remarks>
	public bool SecureOnly { get; set; } = true;

	/// <summary>
	/// Gets or sets the optional domain restriction for the cookie.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Default:</b> <see langword="null"/> (cookie scoped to the request host, excluding subdomains).
	///     </para>
	///     <para>
	///     Set this if the API and the Blazor UI are served from different subdomains of the same parent domain
	///     (e.g., <c>api.lumacore.local</c> and <c>app.lumacore.local</c> → set to <c>.lumacore.local</c>).
	///     </para>
	/// </remarks>
	public string? Domain { get; set; }

	/// <summary>
	/// Gets or sets the URL path scope of the cookie.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Default:</b> <c>/api</c>.
	///     </para>
	///     <para>
	///     Restricts the cookie to API routes only. The browser will not send it for static file requests or
	///     non-API paths, reducing unnecessary header traffic. When LumaCore is mounted behind a reverse proxy
	///     under a sub-path (e.g., <c>/tools/lumacore/api</c>), this value must match the path the <em>browser</em>
	///     sees, not the internal route.
	///     </para>
	/// </remarks>
	[Required]
	public string Path { get; set; } = "/api";
}
