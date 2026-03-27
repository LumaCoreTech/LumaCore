// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LumaCore.Api.Features.ErrorHandling;

/// <summary>
/// A centralized exception handler that converts unhandled exceptions into RFC 7807 <see cref="ProblemDetails"/>
/// responses with trace correlation.
/// </summary>
/// <remarks>
///     <para>
///     This handler implements <see cref="IExceptionHandler"/> to provide consistent, structured error responses for
///     all unhandled exceptions in the LumaCore API. Every error response includes a <c>traceId</c> extension field
///     that correlates with server-side logs for debugging.
///     </para>
///     <para>
///         <b>Security considerations:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             Exception details (message, stack trace) are <em>never</em> included in production responses to prevent
///             information disclosure.
///             </description>
///         </item>
///         <item>
///             <description>
///             The <c>traceId</c> allows support teams to locate detailed exception information in server logs without
///             exposing it to clients.
///             </description>
///         </item>
///     </list>
///     <para>Register this handler in the DI container using <see cref="ServiceRegistration.AddErrorHandlingFeature"/>.</para>
/// </remarks>
/// <example>
/// The handler produces responses like:
/// <code>
/// {
///   "type": "urn:lumacore:error:internal",
///   "title": "An unexpected error occurred",
///   "status": 500,
///   "traceId": "00-abc123def456..."
/// }
/// </code>
/// </example>
sealed class LumaCoreExceptionHandler : IExceptionHandler
{
	private readonly ILogger<LumaCoreExceptionHandler> mLogger;

	/// <summary>
	/// Initializes a new instance of the <see cref="LumaCoreExceptionHandler"/> class.
	/// </summary>
	/// <param name="logger">
	/// The <see cref="ILogger{TCategoryName}"/> used to log exception details.
	/// </param>
	public LumaCoreExceptionHandler(ILogger<LumaCoreExceptionHandler> logger)
	{
		mLogger = logger;
	}

	/// <summary>
	/// Attempts to handle the exception by generating a <see cref="ProblemDetails"/> response.
	/// </summary>
	/// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
	/// <param name="exception">The unhandled <see cref="Exception"/> to handle.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> indicating the exception was handled and a response was written;
	/// the exception middleware should not invoke subsequent handlers.
	/// </returns>
	public async ValueTask<bool> TryHandleAsync(
		HttpContext       httpContext,
		Exception         exception,
		CancellationToken cancellationToken)
	{
		// Get the trace ID for correlation with server-side logs.
		// Prefer the distributed tracing Activity ID if available (W3C Trace Context),
		// otherwise fall back to the ASP.NET Core request trace identifier.
		string traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

		// Log the full exception details server-side for debugging.
		// TraceId is automatically included by Serilog's WithSpan enricher.
		mLogger.LogError(
			exception,
			"Unhandled exception occurred while processing request {Method} {Path}",
			httpContext.Request.Method,
			httpContext.Request.Path);

		// Build a ProblemDetails response with our custom error type URN.
		// We intentionally omit exception details to prevent information disclosure.
		var problemDetails = new ProblemDetails
		{
			Type = ErrorTypes.Internal,
			Title = "An unexpected error occurred",
			Status = StatusCodes.Status500InternalServerError,
			// The instance URI uniquely identifies this specific occurrence.
			Instance = httpContext.Request.Path,
			// Add traceId as an extension for client-side correlation.
			Extensions =
			{
				["traceId"] = traceId
			}
		};

		// Set the response status code and content type.
		httpContext.Response.StatusCode = problemDetails.Status.Value;
		httpContext.Response.ContentType = "application/problem+json";

		// Write the ProblemDetails as JSON with the correct content type.
		await httpContext.Response.WriteAsJsonAsync(
				problemDetails,
				options: null,
				contentType: "application/problem+json",
				cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		// Return true to indicate the exception was handled.
		// This prevents the default exception handler from running.
		return true;
	}
}
