// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Api.Features.HttpsRedirection;

/// <summary>
/// Configuration options controlling HTTPS redirection behavior.
/// </summary>
/// <remarks>
///     <para>
///     These options are bound from the <c>HttpsRedirection</c> configuration section
///     and control whether HTTP requests are redirected to HTTPS.
///     </para>
///     <para>
///     HTTPS redirection is disabled by default to support development scenarios
///     and deployments behind reverse proxies that terminate TLS.
///     </para>
/// </remarks>
sealed class HttpsRedirectionOptions
{
	/// <summary>
	/// The configuration section name used to bind these options.
	/// </summary>
	public const string SectionName = "HttpsRedirection";

	/// <summary>
	/// Gets or sets a value indicating whether HTTP requests should be redirected to HTTPS.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Defaults to <see langword="false"/>, so redirection is opt-in.
	///     </para>
	///     <para>
	///     When running behind a reverse proxy that terminates TLS, this should typically
	///     remain <see langword="false"/> as the proxy handles HTTPS redirection.
	///     </para>
	/// </remarks>
	public bool Enabled { get; set; } = false;

	/// <summary>
	/// Gets or sets the HTTPS port to use for redirection.
	/// </summary>
	/// <remarks>
	///     <para>
	///     If <see langword="null"/>, the framework attempts to infer the port from
	///     the server addresses or uses the default HTTPS port (443).
	///     </para>
	///     <para>
	///     Set this explicitly when using a non-standard HTTPS port.
	///     Valid port numbers are 1–65535.
	///     </para>
	/// </remarks>
	[Range(1, 65535, ErrorMessage = "HttpsRedirection:HttpsPort must be between 1 and 65535.")]
	public int? HttpsPort { get; set; }
}
