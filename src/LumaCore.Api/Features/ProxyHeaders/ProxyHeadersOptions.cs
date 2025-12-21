// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace LumaCore.Api.Features.ProxyHeaders;

/// <summary>
/// Configuration options for processing forwarded headers from reverse proxies and load balancers.
/// </summary>
/// <remarks>
/// These options control how LumaCore processes headers like X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Host,
/// and X-Forwarded-Prefix that are set by reverse proxies.
/// </remarks>
sealed class ProxyHeadersOptions : IValidatableObject
{
	private const string ForwardLimitRangeError =
		"ForwardLimit must be greater than or equal to 1 when specified.";

	/// <summary>
	/// The configuration section name used to bind these options.
	/// </summary>
	public const string SectionName = "ProxyHeaders";

	/// <summary>
	/// Gets or sets the mode for processing forwarded headers. Defaults to
	/// <see cref="ForwardedHeaderMode.Disabled"/> for security (secure-by-default).
	/// </summary>
	/// <value>
	///     <list type="bullet">
	///         <item><see cref="ForwardedHeaderMode.Disabled"/> — no header processing (direct access)</item>
	///         <item><see cref="ForwardedHeaderMode.Cloud"/> — trust all headers (managed cloud proxies)</item>
	///         <item><see cref="ForwardedHeaderMode.SelfManaged"/> — only trust explicitly configured proxies/networks</item>
	///     </list>
	/// </value>
	public ForwardedHeaderMode Mode { get; set; } = ForwardedHeaderMode.Disabled;

	/// <summary>
	/// Gets or sets the maximum number of proxy hops to consider when processing forwarded headers.
	/// </summary>
	/// <value>
	/// A positive integer, or <see langword="null"/> to use a conservative default of 1 (single proxy scenario).
	/// </value>
	[Range(1, int.MaxValue, ErrorMessage = ForwardLimitRangeError)]
	public int? ForwardLimit { get; set; }

	/// <summary>
	/// Gets or sets the list of proxy IP addresses that are considered trusted. Only relevant in
	/// <see cref="ForwardedHeaderMode.SelfManaged"/> mode.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <strong>SelfManaged mode:</strong> Must contain at least one entry (this list OR
	///     <see cref="TrustedNetworks"/>). Startup validation will fail if both lists are empty.
	///     </para>
	///     <para><strong>Cloud mode:</strong> This list is ignored (cloud platforms control headers).</para>
	///     <para><strong>Disabled mode:</strong> This list is ignored (no header processing).</para>
	///     <para>Example: <c>["10.0.0.100", "192.168.1.50"]</c></para>
	/// </remarks>
	public List<string> TrustedProxies { get; set; } = [];

	/// <summary>
	/// Gets or sets the list of network addresses (CIDR notation) that are considered trusted. Only relevant in
	/// <see cref="ForwardedHeaderMode.SelfManaged"/> mode.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <strong>SelfManaged mode:</strong> Must contain at least one entry (this list OR
	///     <see cref="TrustedProxies"/>). Startup validation will fail if both lists are empty.
	///     </para>
	///     <para>Supports CIDR notation: <c>"192.168.1.0/24"</c> (subnet) or <c>"10.0.0.100/32"</c> (single IP).</para>
	///     <para><strong>Cloud mode:</strong> This list is ignored (cloud platforms control headers).</para>
	///     <para><strong>Disabled mode:</strong> This list is ignored (no header processing).</para>
	///     <para>Example: <c>["10.0.0.0/8", "192.168.0.0/16"]</c></para>
	/// </remarks>
	public List<string> TrustedNetworks { get; set; } = [];

	/// <inheritdoc/>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		// SelfManaged requires at least one trusted proxy or network
		if (Mode == ForwardedHeaderMode.SelfManaged)
		{
			if (TrustedProxies.Count == 0 && TrustedNetworks.Count == 0)
			{
				yield return new ValidationResult(
					"ProxyHeaders:Mode is set to 'SelfManaged' but neither TrustedProxies nor TrustedNetworks are configured. " +
					"You must specify at least one trusted proxy IP or network to prevent security vulnerabilities.",
					[nameof(TrustedProxies), nameof(TrustedNetworks)]);
			}

			// Validate TrustedProxies are valid IPs
			foreach (string proxy in TrustedProxies)
			{
				if (!IPAddress.TryParse(proxy, out IPAddress? _))
				{
					yield return new ValidationResult(
						$"ProxyHeaders:TrustedProxies contains invalid IP address: '{proxy}'. " +
						$"Expected format: '10.0.0.100' or '2001:db8::1'",
						[nameof(TrustedProxies)]);
				}
			}

			// Validate TrustedNetworks are valid CIDR notation
			foreach (string network in TrustedNetworks)
			{
				if (!TryParseCidr(network, out IPNetwork _, out string? error))
				{
					yield return new ValidationResult(
						$"ProxyHeaders:TrustedNetworks contains invalid CIDR notation: '{network}'. {error}",
						[nameof(TrustedNetworks)]);
				}
			}
		}
	}

	/// <summary>
	/// Attempts to parse a CIDR notation string and validates its format and prefix length.
	/// </summary>
	/// <param name="value">
	/// The CIDR notation string to parse, in the format 'IP/prefixLength' (e.g., '192.168.1.0/24').
	/// </param>
	/// <param name="network">
	/// When the method returns <see langword="true"/>, contains the parsed <see cref="IPNetwork"/>; otherwise, the
	/// default value.
	/// </param>
	/// <param name="error">
	/// When the method returns <see langword="false"/>, contains a description of the validation error; otherwise,
	/// <see langword="null"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the CIDR string is valid and correctly formatted; otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// The method checks that the input string is in the correct CIDR format, that the IP address is valid, and that
	/// the prefix length is within the allowed range (0–32 for IPv4, 0–128 for IPv6). No parsing of the actual
	/// network is performed beyond validation.
	/// </remarks>
	// ReSharper disable once OutParameterValueIsAlwaysDiscarded.Local
	private static bool TryParseCidr(string value, out IPNetwork network, out string? error)
	{
		network = default;
		error = null;

		// Split "IP/PrefixLength" with trimming and at most 2 parts
		string[] parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
		if (parts.Length != 2)
		{
			error = $"Invalid CIDR notation: '{value}'. Expected format 'IP/PrefixLength'.";
			return false;
		}

		if (!IPAddress.TryParse(parts[0], out IPAddress? address))
		{
			error = $"Invalid IP address in CIDR '{value}'.";
			return false;
		}

		if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int prefixLength))
		{
			error = $"Invalid prefix length in CIDR '{value}'.";
			return false;
		}

		// Enforce different prefix ranges for IPv4 and IPv6
		if (address.AddressFamily == AddressFamily.InterNetwork)
		{
			if (prefixLength is < 0 or > 32)
			{
				error = $"Invalid prefix length: {prefixLength}. IPv4 prefixes must be between 0 and 32.";
				return false;
			}
		}
		else if (address.AddressFamily == AddressFamily.InterNetworkV6)
		{
			if (prefixLength is < 0 or > 128)
			{
				error = $"Invalid prefix length: {prefixLength}. IPv6 prefixes must be between 0 and 128.";
				return false;
			}
		}
		else
		{
			error = $"Unsupported address family for CIDR '{value}': {address.AddressFamily}.";
			return false;
		}

		network = new IPNetwork(address, prefixLength);
		return true;
	}
}

/// <summary>
/// Specifies the mode for processing forwarded headers from reverse proxies and load balancers.
/// </summary>
enum ForwardedHeaderMode
{
	/// <summary>
	/// Forwarded headers are not processed. Use this when LumaCore is accessed directly without a reverse proxy. This
	/// is the default (secure-by-default).
	/// </summary>
	Disabled,

	/// <summary>
	/// Trust all forwarded headers from platform-managed reverse proxies. Use this when deploying to cloud platforms
	/// (Azure App Service, AWS Elastic Beanstalk, Google Cloud Run) where the platform controls the reverse proxy
	/// infrastructure.
	/// </summary>
	/// <remarks>
	/// Use this mode only when the application is guaranteed to receive traffic exclusively through a platform-managed
	/// reverse proxy (Azure, AWS, GCP). If direct public access is possible, prefer <see cref="SelfManaged"/> and
	/// configure trusted proxies or networks explicitly.
	/// </remarks>
	Cloud,

	/// <summary>
	/// Only trust forwarded headers from explicitly configured proxies and networks. Use this when running behind your
	/// own reverse proxy (Nginx, Traefik, Caddy) where you control the proxy IP addresses. Requires
	/// <see cref="ProxyHeadersOptions.TrustedProxies"/> or <see cref="ProxyHeadersOptions.TrustedNetworks"/> to be
	/// configured.
	/// </summary>
	SelfManaged
}
