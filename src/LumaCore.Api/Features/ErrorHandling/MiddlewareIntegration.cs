// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.AspNetCore.Mvc;

namespace LumaCore.Api.Features.ErrorHandling;

/// <summary>
/// Provides extension methods for integrating error handling middleware in the request pipeline.
/// </summary>
static class MiddlewareIntegration
{
	/// <summary>
	/// Configures the application to return RFC 7807 <see cref="ProblemDetails"/> responses for API error status codes.
	/// </summary>
	/// <param name="app">The <see cref="WebApplication"/> to configure.</param>
	/// <returns>The <paramref name="app"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This middleware intercepts non-success HTTP status codes for <c>/api/*</c> paths and converts them into
	///     structured <see cref="ProblemDetails"/> responses with LumaCore-specific error type URNs.
	///     </para>
	///     <para>
	///     <b>Scope:</b> Only requests to <c>/api/*</c> paths are affected. Non-API paths (Blazor SPA routes, static
	///     files) pass through unchanged, allowing the SPA fallback to serve <c>index.html</c> for client-side routing.
	///     </para>
	///     <para>
	///         <b>Status code mapping:</b>
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>400 → <see cref="ErrorTypes.BadRequest"/></description>
	///         </item>
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
	///             <description>405 → <see cref="ErrorTypes.MethodNotAllowed"/></description>
	///         </item>
	///         <item>
	///             <description>406 → <see cref="ErrorTypes.NotAcceptable"/></description>
	///         </item>
	///         <item>
	///             <description>408 → <see cref="ErrorTypes.RequestTimeout"/></description>
	///         </item>
	///         <item>
	///             <description>409 → <see cref="ErrorTypes.Conflict"/></description>
	///         </item>
	///         <item>
	///             <description>410 → <see cref="ErrorTypes.Gone"/></description>
	///         </item>
	///         <item>
	///             <description>413 → <see cref="ErrorTypes.PayloadTooLarge"/></description>
	///         </item>
	///         <item>
	///             <description>415 → <see cref="ErrorTypes.UnsupportedMediaType"/></description>
	///         </item>
	///         <item>
	///             <description>422 → <see cref="ErrorTypes.Validation"/></description>
	///         </item>
	///         <item>
	///             <description>426 → <see cref="ErrorTypes.UpgradeRequired"/></description>
	///         </item>
	///         <item>
	///             <description>429 → <see cref="ErrorTypes.RateLimited"/></description>
	///         </item>
	///         <item>
	///             <description>431 → <see cref="ErrorTypes.HeadersTooLarge"/></description>
	///         </item>
	///         <item>
	///             <description>500 → <see cref="ErrorTypes.Internal"/></description>
	///         </item>
	///         <item>
	///             <description>501 → <see cref="ErrorTypes.NotImplemented"/></description>
	///         </item>
	///         <item>
	///             <description>503 → <see cref="ErrorTypes.ServiceUnavailable"/></description>
	///         </item>
	///         <item>
	///             <description>Other → Standard RFC 9110 type reference</description>
	///         </item>
	///     </list>
	///     <para>
	///     <b>Pipeline position:</b> Call this method early in the pipeline, after <c>UseProxyHeadersFeature()</c> and
	///     <c>UseExceptionHandler()</c>.
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
		app.UseStatusCodePages(context =>
		{
			// Only generate ProblemDetails for API endpoints.
			// Non-API paths (Blazor routes, static files) are handled elsewhere.
			if (!context.HttpContext.Request.Path.StartsWithSegments("/api"))
				return Task.CompletedTask;

			// Map status codes to LumaCore-specific error info.
			int statusCode = context.HttpContext.Response.StatusCode;
			(string? Type, string? Title, string? Detail) errorInfo = MapStatusCodeToErrorInfo(statusCode);

			// Generate a ProblemDetails response for the status code.
			context.HttpContext.Response.ContentType = "application/problem+json";
			return Results
				.Problem(
					statusCode: statusCode,
					type: errorInfo.Type,
					title: errorInfo.Title,
					detail: errorInfo.Detail)
				.ExecuteAsync(context.HttpContext);
		});

		return app;
	}

	/// <summary>
	/// Maps an HTTP status code to the corresponding LumaCore error information.
	/// </summary>
	/// <param name="statusCode">The HTTP status code.</param>
	/// <returns>
	/// A tuple containing the error type URN, title, and detail message for known status codes.
	/// For unknown codes, returns <see langword="null"/> values (which will use RFC 9110 defaults).
	/// </returns>
	private static (string? Type, string? Title, string? Detail) MapStatusCodeToErrorInfo(int statusCode)
	{
		return statusCode switch
		{
			// 4xx Client Errors
			StatusCodes.Status400BadRequest => (
				                                   ErrorTypes.BadRequest,
				                                   "Bad Request",
				                                   "The request is malformed or contains invalid data."),
			StatusCodes.Status401Unauthorized => (
				                                     ErrorTypes.Unauthorized,
				                                     "Authentication Required",
				                                     "Valid credentials are required to access this resource."),
			StatusCodes.Status403Forbidden => (
				                                  ErrorTypes.Forbidden,
				                                  "Access Denied",
				                                  "Insufficient permissions to access this resource."),
			StatusCodes.Status404NotFound => (
				                                 ErrorTypes.NotFound,
				                                 "Resource Not Found",
				                                 "The requested resource does not exist."),
			StatusCodes.Status405MethodNotAllowed => (
				                                         ErrorTypes.MethodNotAllowed,
				                                         "Method Not Allowed",
				                                         "The HTTP method is not supported for this endpoint."),
			StatusCodes.Status406NotAcceptable => (
				                                      ErrorTypes.NotAcceptable,
				                                      "Not Acceptable",
				                                      "The server cannot produce a response matching the Accept header."),
			StatusCodes.Status408RequestTimeout => (
				                                       ErrorTypes.RequestTimeout,
				                                       "Request Timeout",
				                                       "The server timed out waiting for the request."),
			StatusCodes.Status409Conflict => (
				                                 ErrorTypes.Conflict,
				                                 "Conflict",
				                                 "The request conflicts with the current state of the resource."),
			StatusCodes.Status410Gone => (
				                             ErrorTypes.Gone,
				                             "Gone",
				                             "The requested resource has been permanently removed."),
			StatusCodes.Status413PayloadTooLarge => (
				                                        ErrorTypes.PayloadTooLarge,
				                                        "Payload Too Large",
				                                        "The request payload exceeds the server's size limit."),
			StatusCodes.Status415UnsupportedMediaType => (
				                                             ErrorTypes.UnsupportedMediaType,
				                                             "Unsupported Media Type",
				                                             "The request content type is not supported."),
			StatusCodes.Status422UnprocessableEntity => (
				                                            ErrorTypes.Validation,
				                                            "Validation Failed",
				                                            "The request data failed validation."),
			StatusCodes.Status426UpgradeRequired => (
				                                        ErrorTypes.UpgradeRequired,
				                                        "Upgrade Required",
				                                        "The client must switch to a different protocol."),
			StatusCodes.Status429TooManyRequests => (
				                                        ErrorTypes.RateLimited,
				                                        "Rate Limit Exceeded",
				                                        "Request rate limit exceeded. Retry after cooldown."),
			StatusCodes.Status431RequestHeaderFieldsTooLarge => (
				                                                    ErrorTypes.HeadersTooLarge,
				                                                    "Request Header Fields Too Large",
				                                                    "The request headers exceed the server's size limit."),

			// 5xx Server Errors
			StatusCodes.Status500InternalServerError => (
				                                            ErrorTypes.Internal,
				                                            "Internal Server Error",
				                                            "An unexpected error occurred."),
			StatusCodes.Status501NotImplemented => (
				                                       ErrorTypes.NotImplemented,
				                                       "Not Implemented",
				                                       "The requested functionality is not supported."),
			StatusCodes.Status503ServiceUnavailable => (
				                                           ErrorTypes.ServiceUnavailable,
				                                           "Service Unavailable",
				                                           "The service is temporarily unavailable."),

			var _ => (null, null, null)
		};
	}
}
