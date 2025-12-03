// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Options;

namespace LumaCore.Api.Features.SecurityHeaders;

/// <summary>
/// Provides extension methods for integrating security headers middleware in the request pipeline.
/// </summary>
public static class MiddlewareIntegration
{
	/// <summary>
	/// Configures the application to add security headers to all responses.
	/// </summary>
	/// <param name="app">The web application.</param>
	/// <returns>The web application for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This middleware adds various HTTP security headers based on the
	///     <see cref="SecurityHeadersOptions"/> configuration.
	///     </para>
	///     <para>
	///     <strong>Important:</strong> This middleware should be added early in the pipeline,
	///     after <c>UseProxyHeadersFeature()</c> and <c>UseHttpsRedirectionFeature()</c>,
	///     but before CORS, routing, and authentication middleware.
	///     </para>
	///     <para>
	///     The following headers may be added:
	///     </para>
	///     <list type="bullet">
	///         <item><c>Strict-Transport-Security</c> — HSTS, enforces HTTPS</item>
	///         <item><c>X-Frame-Options</c> — Prevents clickjacking</item>
	///         <item><c>X-Content-Type-Options</c> — Prevents MIME sniffing</item>
	///         <item><c>Referrer-Policy</c> — Controls referrer information</item>
	///         <item><c>Content-Security-Policy</c> — Restricts resource loading</item>
	///     </list>
	/// </remarks>
	/// <example>
	///     <code>
	/// // In Program.Pipeline.cs
	/// app.UseProxyHeadersFeature();
	/// app.UseHttpsRedirectionFeature();
	/// app.UseSecurityHeadersFeature();
	/// app.UseCorsFeature();
	/// </code>
	/// </example>
	public static WebApplication UseSecurityHeadersFeature(this WebApplication app)
	{
		// Get logger.
		var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
		ILogger logger = loggerFactory.CreateLogger("LumaCore.SecurityHeaders");

		// Get options.
		SecurityHeadersOptions options = app.Services
			.GetRequiredService<IOptions<SecurityHeadersOptions>>()
			.Value;

		// Skip if disabled.
		if (!options.Enabled)
		{
			logger.LogDebug(
				"Security headers are disabled. " +
				"No additional HTTP security headers will be added to responses.");
			return app;
		}

		logger.LogDebug("Security headers are enabled.");

		// Add HSTS if enabled.
		if (options.EnableHsts)
		{
			if (options.HstsMaxAgeSeconds == 0)
			{
				logger.LogWarning(
					"HSTS max-age is 0. This will clear the HSTS entry in browsers. " +
					"Set HstsMaxAgeSeconds > 0 for production use.");
			}

			app.UseHsts();
			logger.LogDebug(
				"HSTS enabled with max-age {MaxAge} seconds, includeSubDomains={IncludeSubDomains}.",
				options.HstsMaxAgeSeconds,
				options.HstsIncludeSubDomains);
		}

		// Add custom security headers middleware.
		app.Use((context, next) =>
		{
			// X-Frame-Options
			if (!string.IsNullOrEmpty(options.XFrameOptions))
			{
				context.Response.Headers["X-Frame-Options"] = options.XFrameOptions;
			}

			// X-Content-Type-Options
			if (options.EnableNoSniff)
			{
				context.Response.Headers["X-Content-Type-Options"] = "nosniff";
			}

			// Referrer-Policy
			if (!string.IsNullOrEmpty(options.ReferrerPolicy))
			{
				context.Response.Headers["Referrer-Policy"] = options.ReferrerPolicy;
			}

			// Content-Security-Policy
			if (!string.IsNullOrEmpty(options.ContentSecurityPolicy))
			{
				context.Response.Headers["Content-Security-Policy"] = options.ContentSecurityPolicy;
			}

			return next();
		});

		// Log configured headers.
		if (!string.IsNullOrEmpty(options.XFrameOptions))
			logger.LogDebug("X-Frame-Options: {Value}", options.XFrameOptions);

		if (options.EnableNoSniff)
			logger.LogDebug("X-Content-Type-Options: nosniff");

		if (!string.IsNullOrEmpty(options.ReferrerPolicy))
			logger.LogDebug("Referrer-Policy: {Value}", options.ReferrerPolicy);

		if (!string.IsNullOrEmpty(options.ContentSecurityPolicy))
			logger.LogDebug("Content-Security-Policy: {Value}", options.ContentSecurityPolicy);

		return app;
	}
}
