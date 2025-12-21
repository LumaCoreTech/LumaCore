// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Api.Features.SecurityHeaders;

/// <summary>
/// Configuration options for HTTP security headers.
/// </summary>
/// <remarks>
///     <para>
///     Security headers protect against common web vulnerabilities like clickjacking, MIME-type sniffing, and
///     cross-site scripting (XSS).
///     </para>
///     <para>These headers are recommended for any publicly exposed web application.</para>
/// </remarks>
sealed class SecurityHeadersOptions : IValidatableObject
{
	private const string HstsMaxAgeRangeError = "HstsMaxAgeSeconds must be a non-negative value.";

	/// <summary>
	/// The configuration section name used to bind these options.
	/// </summary>
	public const string SectionName = "SecurityHeaders";

	/// <summary>
	/// Valid values for the <c>X-Frame-Options</c> header.
	/// </summary>
	private static readonly HashSet<string> sValidXFrameOptions = new(StringComparer.OrdinalIgnoreCase)
	{
		"DENY",
		"SAMEORIGIN"
	};

	/// <summary>
	/// Valid values for the <c>Referrer-Policy</c> header.
	/// </summary>
	private static readonly HashSet<string> sValidReferrerPolicies = new(StringComparer.OrdinalIgnoreCase)
	{
		"no-referrer",
		"no-referrer-when-downgrade",
		"origin",
		"origin-when-cross-origin",
		"same-origin",
		"strict-origin",
		"strict-origin-when-cross-origin",
		"unsafe-url"
	};

	/// <summary>
	/// Gets or sets a value indicating whether security headers should be added to responses.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to add security headers; <see langword="false"/> to disable.
	/// Default is <see langword="true"/>.
	/// </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether HSTS (HTTP Strict Transport Security) should be enabled.
	/// </summary>
	/// <remarks>
	///     <para>
	///     HSTS tells browsers to only connect via HTTPS for a specified duration. This prevents protocol downgrade
	///     attacks and cookie hijacking.
	///     </para>
	///     <para>
	///     <strong>Warning:</strong> Only enable HSTS when your site fully supports HTTPS. Once enabled, browsers
	///     will refuse HTTP connections for the specified duration.
	///     </para>
	///     <para>
	///     <strong>Development:</strong> Consider setting this to <see langword="false"/> in
	///     <c>appsettings.Development.json</c> to avoid localhost HTTPS enforcement issues.
	///     </para>
	///     <para>Default is <see langword="true"/>.</para>
	/// </remarks>
	public bool EnableHsts { get; set; } = true;

	/// <summary>
	/// Gets or sets the HSTS max-age in seconds.
	/// </summary>
	/// <value>
	/// Duration in seconds that browsers remember to use HTTPS only. Common values:
	/// <list type="bullet">
	///     <item>0 — Disable HSTS (clears browser's HSTS entry)</item>
	///     <item>86400 — 1 day (testing)</item>
	///     <item>2592000 — 30 days (initial rollout)</item>
	///     <item>31536000 — 1 year (production, default)</item>
	/// </list>
	/// </value>
	[Range(0, int.MaxValue, ErrorMessage = HstsMaxAgeRangeError)]
	public int HstsMaxAgeSeconds { get; set; } = 31536000;

	/// <summary>
	/// Gets or sets a value indicating whether HSTS should include subdomains.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to apply HSTS to all subdomains; <see langword="false"/> for the main domain only.
	/// Default is <see langword="false"/>.
	/// </value>
	public bool HstsIncludeSubDomains { get; set; }

	/// <summary>
	/// Gets or sets the <c>X-Frame-Options</c> header value.
	/// </summary>
	/// <value>
	/// Valid values:
	/// <list type="bullet">
	///     <item><c>"DENY"</c> — Never allow framing (most secure, default)</item>
	///     <item><c>"SAMEORIGIN"</c> — Allow framing by same origin only</item>
	///     <item><see langword="null"/> — Don't set this header</item>
	/// </list>
	/// </value>
	/// <remarks>
	/// Protects against clickjacking attacks. <c>ALLOW-FROM</c> is deprecated and not supported.
	/// </remarks>
	public string? XFrameOptions { get; set; } = "DENY";

	/// <summary>
	/// Gets or sets a value indicating whether <c>X-Content-Type-Options: nosniff</c> should be set.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to add the header; <see langword="false"/> to omit it.
	/// Default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// Prevents browsers from MIME-sniffing a response away from the declared content-type. Protects against
	/// drive-by download attacks.
	/// </remarks>
	public bool EnableNoSniff { get; set; } = true;

	/// <summary>
	/// Gets or sets the <c>Referrer-Policy</c> header value.
	/// </summary>
	/// <value>
	/// Controls how much referrer information is included with requests. Valid values:
	/// <list type="bullet">
	///     <item><c>"no-referrer"</c> — Never send referrer</item>
	///     <item><c>"no-referrer-when-downgrade"</c> — No referrer on HTTPS→HTTP</item>
	///     <item><c>"origin"</c> — Only send origin</item>
	///     <item><c>"origin-when-cross-origin"</c> — Full URL same-origin, origin cross-origin</item>
	///     <item><c>"same-origin"</c> — Only send for same-origin requests</item>
	///     <item><c>"strict-origin"</c> — Origin only, no downgrade</item>
	///     <item><c>"strict-origin-when-cross-origin"</c> — Full URL same-origin, origin cross-origin, no downgrade (default)</item>
	///     <item><c>"unsafe-url"</c> — Always send full URL (not recommended)</item>
	///     <item><see langword="null"/> — Don't set this header</item>
	/// </list>
	/// </value>
	public string? ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

	/// <summary>
	/// Gets or sets the <c>Content-Security-Policy</c> header value.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Defines approved sources of content that browsers may load. Powerful protection against XSS attacks — even
	///     if malicious code is injected, the browser blocks loading resources from unauthorized sources.
	///     </para>
	///     <para>Set to <see langword="null"/> to disable the CSP header.</para>
	///     <para>
	///         <strong>Default policy breakdown:</strong>
	///     </para>
	///     <list type="bullet">
	///         <item><c>default-src 'self'</c> — Only allow resources from same origin (fallback for all types)</item>
	///         <item>
	///         <c>script-src 'self' 'unsafe-eval'</c> — Scripts from same origin; <c>'unsafe-eval'</c> required for
	///         Blazor WebAssembly runtime
	///         </item>
	///         <item>
	///         <c>style-src 'self' 'unsafe-inline'</c> — Styles from same origin; <c>'unsafe-inline'</c> required
	///         for Blazor's dynamic styling
	///         </item>
	///         <item>
	///         <c>img-src 'self' data:</c> — Images from same origin plus inline <c>data:</c> URIs (common for
	///         small icons)
	///         </item>
	///         <item><c>font-src 'self'</c> — Fonts only from same origin</item>
	///         <item><c>connect-src 'self'</c> — Fetch/XHR/WebSocket only to same origin</item>
	///         <item>
	///         <c>frame-ancestors 'none'</c> — Prevent embedding in iframes (same as <c>X-Frame-Options: DENY</c>)
	///         </item>
	///     </list>
	///     <para>
	///         <strong>Blazor WebAssembly requirements:</strong>
	///     </para>
	///     <para>
	///     Blazor WASM requires <c>'unsafe-eval'</c> for its .NET runtime and <c>'unsafe-inline'</c> for dynamic
	///     styles. These weaken CSP somewhat but are unavoidable for Blazor. A weak CSP is still better than no CSP —
	///     it blocks external script injection.
	///     </para>
	///     <para>
	///         <strong>Customization examples:</strong>
	///     </para>
	///     <list type="bullet">
	///         <item>Allow external CDN: <c>script-src 'self' 'unsafe-eval' https://cdn.example.com</c></item>
	///         <item>Allow external images: <c>img-src 'self' data: https:</c></item>
	///     </list>
	/// </remarks>
	public string? ContentSecurityPolicy { get; set; } =
		"default-src 'self'; " +
		"script-src 'self' 'unsafe-eval'; " +
		"style-src 'self' 'unsafe-inline'; " +
		"img-src 'self' data:; " +
		"font-src 'self'; " +
		"connect-src 'self'; " +
		"frame-ancestors 'none'";

	/// <inheritdoc/>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		// If disabled, no further validation needed.
		if (!Enabled)
			yield break;

		// Validate X-Frame-Options.
		if (!string.IsNullOrEmpty(XFrameOptions) && !sValidXFrameOptions.Contains(XFrameOptions))
		{
			yield return new ValidationResult(
				$"SecurityHeaders:XFrameOptions '{XFrameOptions}' is invalid. " +
				$"Valid values are: {string.Join(", ", sValidXFrameOptions)}, or null to disable.",
				[nameof(XFrameOptions)]);
		}

		// Validate Referrer-Policy.
		if (!string.IsNullOrEmpty(ReferrerPolicy) && !sValidReferrerPolicies.Contains(ReferrerPolicy))
		{
			yield return new ValidationResult(
				$"SecurityHeaders:ReferrerPolicy '{ReferrerPolicy}' is invalid. " +
				$"Valid values are: {string.Join(", ", sValidReferrerPolicies)}, or null to disable.",
				[nameof(ReferrerPolicy)]);
		}

		// Validate Content-Security-Policy is not empty whitespace.
		if (ContentSecurityPolicy is not null && string.IsNullOrWhiteSpace(ContentSecurityPolicy))
		{
			yield return new ValidationResult(
				"SecurityHeaders:ContentSecurityPolicy cannot be empty or whitespace. " +
				"Set to null to disable CSP header.",
				[nameof(ContentSecurityPolicy)]);
		}
	}
}
