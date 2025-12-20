// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Options;

namespace LumaCore.Api.Features.Cors;

/// <summary>
/// Provides extension methods for integrating CORS middleware in the request pipeline.
/// </summary>
static class MiddlewareIntegration
{
	/// <summary>
	/// The name of the CORS policy used by LumaCore.
	/// </summary>
	private const string PolicyName = "LumaCorePolicy";

	/// <summary>
	/// Configures the application to apply the CORS policy based on <see cref="CorsOptions"/> configuration.
	/// </summary>
	/// <param name="app">The web application.</param>
	/// <returns>The web application for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This middleware applies Cross-Origin Resource Sharing (CORS) policies that control which
	///     origins, methods, and headers are allowed in cross-origin HTTP requests.
	///     </para>
	///     <para>
	///     <strong>Important:</strong> This middleware must be configured BEFORE <c>UseRouting()</c>
	///     to properly handle preflight requests (OPTIONS method), but AFTER proxy headers,
	///     HTTPS redirection, and security headers middleware.
	///     </para>
	///     <para>
	///     The CORS policy is built dynamically from the <c>Cors</c> configuration section:
	///     </para>
	///     <code>
	/// {
	///   "Cors": {
	///     "Enabled": true,
	///     "AllowedOrigins": ["https://example.com", "http://localhost:3000"],
	///     "AllowCredentials": true,
	///     "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
	///     "AllowedHeaders": ["Content-Type", "Authorization"],
	///     "ExposedHeaders": ["X-Request-Id"],
	///     "PreflightMaxAge": 3600
	///   }
	/// }
	/// </code>
	///     <para>
	///     If <c>Cors:Enabled</c> is <see langword="false"/>, no CORS policy is applied
	///     and cross-origin requests will be blocked by browsers.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// // In Program.Pipeline.cs
	/// app.UseProxyHeadersFeature();
	/// app.UseHttpsRedirectionFeature();
	/// app.UseSecurityHeadersFeature();
	/// app.UseCorsFeature();  // ← Must be before UseRouting()
	/// app.UseRouting();
	/// app.UseAuthentication();
	/// app.UseAuthorization();
	/// </code>
	/// </example>
	public static WebApplication UseCorsFeature(this WebApplication app)
	{
		// Get logger.
		var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
		ILogger logger = loggerFactory.CreateLogger("LumaCore.Cors");

		// Get CORS options.
		CorsOptions corsOptions = app.Services
			.GetRequiredService<IOptions<CorsOptions>>()
			.Value;

		// Apply CORS policy if enabled.
		if (corsOptions.Enabled)
		{
			logger.LogDebug("CORS enabled with policy '{PolicyName}'.", PolicyName);

			// Build the CORS policy dynamically based on configuration.
			app.UseCors(builder =>
			{
				// Configure allowed origins.
				if (corsOptions.AllowedOrigins.Contains("*"))
				{
					logger.LogWarning("CORS policy allows all origins (*). This is insecure and should not be used in production.");
					builder.AllowAnyOrigin();
				}
				else
				{
					builder.WithOrigins([.. corsOptions.AllowedOrigins]);
					logger.LogDebug(
						"CORS policy allows origins: {Origins}",
						string.Join(", ", corsOptions.AllowedOrigins));
				}

				// Configure credentials.
				if (corsOptions.AllowCredentials)
				{
					builder.AllowCredentials();
					logger.LogDebug("CORS policy allows credentials (cookies, auth headers).");
				}

				// Configure allowed methods.
				if (corsOptions.AllowedMethods.Count > 0)
				{
					builder.WithMethods([.. corsOptions.AllowedMethods]);
					logger.LogDebug(
						"CORS policy allows methods: {Methods}",
						string.Join(", ", corsOptions.AllowedMethods));
				}
				else
				{
					builder.AllowAnyMethod();
					logger.LogDebug("CORS policy allows all HTTP methods.");
				}

				// Configure allowed headers.
				if (corsOptions.AllowedHeaders.Count > 0)
				{
					builder.WithHeaders([.. corsOptions.AllowedHeaders]);
					logger.LogDebug(
						"CORS policy allows headers: {Headers}",
						string.Join(", ", corsOptions.AllowedHeaders));
				}
				else
				{
					builder.AllowAnyHeader();
					logger.LogDebug("CORS policy allows all headers.");
				}

				// Configure exposed headers.
				if (corsOptions.ExposedHeaders.Count > 0)
				{
					builder.WithExposedHeaders([.. corsOptions.ExposedHeaders]);
					logger.LogDebug(
						"CORS policy exposes headers: {Headers}",
						string.Join(", ", corsOptions.ExposedHeaders));
				}

				// Configure preflight cache duration.
				if (corsOptions.PreflightMaxAge.HasValue)
				{
					builder.SetPreflightMaxAge(TimeSpan.FromSeconds(corsOptions.PreflightMaxAge.Value));
					logger.LogDebug(
						"CORS preflight responses cached for {Seconds} seconds.",
						corsOptions.PreflightMaxAge.Value);
				}
			});
		}
		else
		{
			logger.LogDebug("CORS is disabled. Cross-origin requests will be blocked by browsers.");
		}

		return app;
	}
}
