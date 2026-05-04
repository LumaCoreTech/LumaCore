// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Api.Features.HttpsRedirection;

/// <summary>
/// Configuration options controlling HTTPS redirection behavior.
/// </summary>
/// <remarks>
/// These options are bound from the <c>HttpsRedirection</c> configuration section and control whether HTTP
/// requests are redirected to HTTPS.
/// HTTPS redirection is disabled by default to support development scenarios and deployments behind reverse
/// proxies that terminate TLS.
/// </remarks>
sealed class HttpsRedirectionOptions
{
	/// <summary>
	/// The configuration section name used to bind these options.
	/// </summary>
	public const string SectionName = "HttpsRedirection";

	private const string HttpsPortRangeError = "HttpsRedirection:HttpsPort must be between 1 and 65535.";

	/// <summary>
	/// Gets or sets a value indicating whether HTTP requests should be redirected to HTTPS.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to redirect HTTP to HTTPS;
	/// <see langword="false"/> to allow HTTP.
	/// Default is <see langword="false"/> (opt-in).
	/// </value>
	/// <remarks>
	/// When running behind a reverse proxy that terminates TLS, this should typically remain
	/// <see langword="false"/> as the proxy handles HTTPS redirection.
	/// </remarks>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the HTTPS port to use for redirection.
	/// </summary>
	/// <value>
	/// A port number between 1 and 65535, or <see langword="null"/> to infer from server addresses or use the
	/// default HTTPS port (443).
	/// </value>
	[Range(1, 65535, ErrorMessage = HttpsPortRangeError)]
	public int? HttpsPort { get; set; }
}
