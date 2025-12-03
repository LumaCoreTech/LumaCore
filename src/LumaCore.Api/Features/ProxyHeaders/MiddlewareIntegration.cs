// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;
using System.Net;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

using IPNetwork = System.Net.IPNetwork;

namespace LumaCore.Api.Features.ProxyHeaders;

/// <summary>
/// Provides extension methods for integrating forwarded headers middleware in the request pipeline.
/// </summary>
public static class MiddlewareIntegration
{
	/// <summary>
	/// Configures the application to process forwarded headers from reverse proxies and load balancers.
	/// </summary>
	/// <param name="app">The web application builder.</param>
	/// <returns>The web application builder for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This middleware processes headers like X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Host,
	///     and X-Forwarded-Prefix that are set by reverse proxies to preserve original request information.
	///     </para>
	///     <para>
	///     <strong>Important:</strong> This middleware must be the FIRST middleware in the pipeline.
	///     It must run before HTTPS redirection, authentication, authorization, and routing middleware
	///     to ensure correct scheme detection, client IP addresses, and URL generation.
	///     Without this ordering, HTTPS redirection may cause infinite redirect loops behind proxies.
	///     </para>
	///     <para>
	///     Behavior is controlled by the <c>ProxyHeaders:Mode</c> configuration setting:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <term>Disabled</term>
	///             <description>No forwarded headers are processed (default, secure-by-default).</description>
	///         </item>
	///         <item>
	///             <term>Cloud</term>
	///             <description>Trust all forwarded headers from platform-managed proxies (Azure, AWS, GCP).</description>
	///         </item>
	///         <item>
	///             <term>SelfManaged</term>
	///             <description>Only trust forwarded headers from explicitly configured proxy IPs/networks.</description>
	///         </item>
	///     </list>
	///     <para>
	///     Configuration example (appsettings.json):
	///     </para>
	///     <code>
	/// {
	///   "ProxyHeaders": {
	///     "Mode": "Cloud"
	///   }
	/// }
	/// </code>
	/// </remarks>
	/// <example>
	///     <code>
	/// // In Program.Pipeline.cs
	/// app.UseProxyHeadersFeature();  // ← Must be the first in the pipeline
	/// app.UseHttpsRedirectionFeature();
	/// app.UseSecurityHeadersFeature();
	/// app.UseCorsFeature();
	/// app.UseRouting();
	/// app.UseAuthentication();
	/// app.UseAuthorization();
	/// </code>
	/// </example>
	public static WebApplication UseProxyHeadersFeature(this WebApplication app)
	{
		// Get logger.
		var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
		ILogger logger = loggerFactory.CreateLogger("LumaCore.ProxyHeaders");

		// Get proxy headers options.
		ProxyHeadersOptions proxyOptions = app.Services
			.GetRequiredService<IOptions<ProxyHeadersOptions>>()
			.Value;

		// Configure based on mode.
		if (proxyOptions.Mode == ForwardedHeaderMode.Cloud)
		{
			// Cloud platform mode
			ConfigureCloudMode(app, proxyOptions, logger);
		}
		else if (proxyOptions.Mode == ForwardedHeaderMode.SelfManaged)
		{
			// Self-managed reverse proxy mode
			ConfigureSelfManagedMode(app, proxyOptions, logger);
		}
		else
		{
			logger.LogDebug(
				"Proxy header processing is disabled. " +
				"X-Forwarded-* headers will not be processed. " +
				"This is correct for direct access without a reverse proxy.");
		}

		return app;
	}

	/// <summary>
	/// Configures forwarded headers for cloud platform deployments.
	/// </summary>
	/// <param name="app">The web application.</param>
	/// <param name="proxyOptions">The proxy headers feature configuration.</param>
	/// <param name="logger">The logger to use.</param>
	/// <remarks>
	/// In cloud mode, all forwarded headers are trusted because the cloud platform (Azure, AWS, GCP)
	/// controls the reverse proxy infrastructure. The <see cref="ForwardedHeadersOptions.KnownProxies"/>
	/// and <see cref="ForwardedHeadersOptions.KnownIPNetworks"/> restrictions are cleared to allow the
	/// platform to set headers.
	/// </remarks>
	private static void ConfigureCloudMode(WebApplication app, ProxyHeadersOptions proxyOptions, ILogger logger)
	{
		logger.LogDebug(
			"Proxy header processing enabled in 'Cloud' mode. " +
			"All X-Forwarded-* headers will be trusted (suitable for Azure, AWS, GCP managed proxies).");

		int forwardLimit = proxyOptions.ForwardLimit is > 0 ? proxyOptions.ForwardLimit.Value : 1;

		logger.LogDebug(
			"Forward limit set to {ForwardLimit} hop(s).",
			forwardLimit);

		var options = new ForwardedHeadersOptions
		{
			ForwardedHeaders = ForwardedHeaders.All,
			ForwardLimit = forwardLimit
		};

		// Clear restrictions - trust platform-managed proxies
		options.KnownIPNetworks.Clear();
		options.KnownProxies.Clear();

		logger.LogDebug("KnownProxies and KnownIPNetworks restrictions cleared (cloud platform manages proxy infrastructure).");

		app.UseForwardedHeaders(options);
	}

	/// <summary>
	/// Configures forwarded headers for self-managed reverse proxy deployments.
	/// </summary>
	/// <param name="app">The web application.</param>
	/// <param name="proxyOptions">The proxy headers feature configuration.</param>
	/// <param name="logger">The logger to use.</param>
	/// <remarks>
	///     <para>
	///     In self-managed mode, only forwarded headers from explicitly configured proxy IPs
	///     and networks are trusted. This prevents header spoofing attacks where malicious clients
	///     could send fake X-Forwarded-For headers.
	///     </para>
	///     <para>
	///     The configuration must specify at least one trusted proxy IP or network, otherwise
	///     startup validation will fail (see <see cref="ProxyHeadersOptions.Validate"/>).
	///     </para>
	/// </remarks>
	private static void ConfigureSelfManagedMode(WebApplication app, ProxyHeadersOptions proxyOptions, ILogger logger)
	{
		logger.LogDebug(
			"Proxy header processing enabled in 'SelfManaged' mode. " +
			"Only explicitly trusted proxies and networks will be accepted.");

		int forwardLimit = proxyOptions.ForwardLimit is > 0 ? proxyOptions.ForwardLimit.Value : 1;

		logger.LogDebug(
			"Forward limit set to {ForwardLimit} hop(s).",
			forwardLimit);

		// Configure forwarded headers options.
		var options = new ForwardedHeadersOptions
		{
			ForwardedHeaders = ForwardedHeaders.All,
			ForwardLimit = forwardLimit
		};

		// Add trusted proxy IPs.
		if (proxyOptions.TrustedProxies.Count > 0)
		{
			foreach (string proxy in proxyOptions.TrustedProxies)
			{
				IPAddress address = IPAddress.Parse(proxy);
				options.KnownProxies.Add(address);
			}

			logger.LogDebug(
				"Trusted proxy IPs: {Proxies}",
				string.Join(", ", proxyOptions.TrustedProxies));
		}

		// Add trusted networks (CIDR notation).
		if (proxyOptions.TrustedNetworks.Count > 0)
		{
			foreach (string network in proxyOptions.TrustedNetworks)
			{
				string[] parts = network.Split('/', 2, StringSplitOptions.TrimEntries);
				IPAddress address = IPAddress.Parse(parts[0]);
				int prefixLength = int.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture);
				options.KnownIPNetworks.Add(new IPNetwork(address, prefixLength));
			}

			logger.LogDebug(
				"Trusted networks (CIDR): {Networks}",
				string.Join(", ", proxyOptions.TrustedNetworks));
		}

		app.UseForwardedHeaders(options);
	}
}
