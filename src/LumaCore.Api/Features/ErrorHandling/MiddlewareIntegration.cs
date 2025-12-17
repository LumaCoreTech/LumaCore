// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.ErrorHandling;

/// <summary>
/// Provides extension methods for integrating error handling middleware in the request pipeline.
/// </summary>
public static class MiddlewareIntegration
{
	/// <summary>
	/// Configures the application to return RFC 7807 <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>
	/// responses for API error status codes.
	/// </summary>
	/// <param name="app">The <see cref="WebApplication"/> to configure.</param>
	/// <returns>The <paramref name="app"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This middleware intercepts non-success HTTP status codes for <c>/api/*</c> paths
	///     and converts them into structured <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>
	///     responses with LumaCore-specific error type URNs.
	///     </para>
	///     <para>
	///     <b>Scope:</b> Only requests to <c>/api/*</c> paths are affected. Non-API paths
	///     (Blazor SPA routes, static files) pass through unchanged, allowing the SPA
	///     fallback to serve <c>index.html</c> for client-side routing.
	///     </para>
	///     <para>
	///         <b>Status code mapping:</b>
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>401 → <see cref="ErrorTypes.Unauthorized"/></description>
	///         </item>
	///         <item>
	///             <description>403 → <see cref="ErrorTypes.Forbidden"/></description>
	///         </item>
	///         <item>
	///             <description>404 → <see cref="ErrorTypes.NotFound"/></description>
	///         </item>
	///         <item>
	///             <description>409 → <see cref="ErrorTypes.Conflict"/></description>
	///         </item>
	///         <item>
	///             <description>429 → <see cref="ErrorTypes.RateLimited"/></description>
	///         </item>
	///         <item>
	///             <description>Other → Standard RFC 9110 type reference</description>
	///         </item>
	///     </list>
	///     <para>
	///     <b>Pipeline position:</b> Call this method early in the pipeline, after
	///     <c>UseProxyHeadersFeature()</c> and <c>UseExceptionHandler()</c>.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// // In Program.Pipeline.cs
	/// app.UseProxyHeadersFeature();
	/// app.UseExceptionHandler();
	/// app.UseErrorHandlingFeature();
	/// </code>
	/// </example>
	public static WebApplication UseErrorHandlingFeature(this WebApplication app)
	{
		app.UseStatusCodePages(async context =>
		{
			// Only generate ProblemDetails for API endpoints.
			// Non-API paths (Blazor routes, static files) are handled elsewhere.
			if (!context.HttpContext.Request.Path.StartsWithSegments("/api"))
				return;

			// Map status codes to LumaCore-specific error type URNs.
			int statusCode = context.HttpContext.Response.StatusCode;
			string? errorType = MapStatusCodeToErrorType(statusCode);

			// Generate a ProblemDetails response for the status code.
			context.HttpContext.Response.ContentType = "application/problem+json";
			await Results.Problem(statusCode: statusCode, type: errorType)
				.ExecuteAsync(context.HttpContext);
		});

		return app;
	}

	/// <summary>
	/// Maps an HTTP status code to the corresponding LumaCore error type URN.
	/// </summary>
	/// <param name="statusCode">The HTTP status code.</param>
	/// <returns>
	/// The error type URN for known status codes, or <see langword="null"/> for
	/// unknown codes (which will use the default RFC 9110 type reference).
	/// </returns>
	private static string? MapStatusCodeToErrorType(int statusCode)
	{
		return statusCode switch
		{
			StatusCodes.Status401Unauthorized    => ErrorTypes.Unauthorized,
			StatusCodes.Status403Forbidden       => ErrorTypes.Forbidden,
			StatusCodes.Status404NotFound        => ErrorTypes.NotFound,
			StatusCodes.Status409Conflict        => ErrorTypes.Conflict,
			StatusCodes.Status429TooManyRequests => ErrorTypes.RateLimited,
			var _                                => null
		};
	}
}
