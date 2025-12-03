// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Options;

namespace LumaCore.Api.Features.HttpsRedirection;

/// <summary>
/// Provides extension methods for integrating HTTPS redirection middleware into the request pipeline.
/// </summary>
/// <remarks>
///     <para>
///     This class is part of the HttpsRedirection feature and conditionally enables HTTPS redirection
///     based on the <see cref="HttpsRedirectionOptions"/> configuration.
///     </para>
/// </remarks>
public static class MiddlewareIntegration
{
	/// <summary>
	/// Adds the HTTPS redirection middleware to the application pipeline if enabled.
	/// </summary>
	/// <param name="app">The web application to configure.</param>
	/// <returns>The modified application for fluent chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method checks the <see cref="HttpsRedirectionOptions.Enabled"/> setting
	///     and only enables HTTPS redirection when explicitly configured.
	///     </para>
	///     <para>
	///     <strong>Important:</strong> This middleware must be configured AFTER
	///     <c>UseProxyHeadersFeature()</c> (or <c>UseForwardedHeaders()</c>) when running
	///     behind a reverse proxy. Otherwise, the middleware sees HTTP instead of the
	///     forwarded HTTPS scheme, causing infinite redirect loops.
	///     </para>
	///     <para>
	///     When running behind a reverse proxy that terminates TLS, HTTPS redirection
	///     should typically be disabled as the proxy handles redirection.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// // In Program.Pipeline.cs
	/// app.UseProxyHeadersFeature();       // ← Must be before HttpsRedirection
	/// app.UseHttpsRedirectionFeature();
	/// app.UseSecurityHeadersFeature();
	/// app.UseCorsFeature();
	/// </code>
	/// </example>
	public static WebApplication UseHttpsRedirectionFeature(this WebApplication app)
	{
		HttpsRedirectionOptions options = app.Services
			.GetRequiredService<IOptions<HttpsRedirectionOptions>>()
			.Value;

		ILogger logger = app.Services
			.GetRequiredService<ILoggerFactory>()
			.CreateLogger("LumaCore.HttpsRedirection");

		if (!options.Enabled)
		{
			logger.LogDebug(
				"HTTPS redirection is disabled. " +
				"HTTP requests will not be redirected to HTTPS.");
			return app;
		}

		logger.LogDebug("HTTPS redirection is enabled.");

		if (options.HttpsPort.HasValue)
		{
			logger.LogDebug(
				"HTTPS redirection configured with explicit port: {HttpsPort}",
				options.HttpsPort.Value);
		}
		else
		{
			logger.LogDebug(
				"HTTPS redirection port not specified. " +
				"Port will be auto-detected from server configuration or default to 443.");
		}

		logger.LogDebug("HTTP requests will be redirected with status code 307 (Temporary Redirect).");

		app.UseHttpsRedirection();

		return app;
	}
}
