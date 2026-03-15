// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Data.Initialization;

using Microsoft.AspNetCore.Mvc;

namespace LumaCore.Api.Features.Data;

/// <summary>
/// Middleware that rejects API requests when the database initialization has not completed successfully.
/// </summary>
/// <remarks>
///     <para>
///     This middleware checks the <see cref="DatabaseInitializationStatus"/> before allowing requests to proceed. If
///     the database initialization failed or is still in progress, requests to API endpoints are rejected with an
///     RFC 7807 <see cref="ProblemDetails"/> response (HTTP 503 Service Unavailable).
///     </para>
///     <para>
///     <b>Excluded paths:</b> Health check endpoints (<c>/health</c> and <c>/api/v1/health/live</c>) are always
///     allowed through so that monitoring systems can still query the application status.
///     </para>
///     <para>
///     <b>Pipeline position:</b> This middleware should be placed early in the pipeline, after error handling but
///     before authentication/authorization, so that database-dependent middleware does not attempt to access an
///     unavailable database.
///     </para>
/// </remarks>
sealed class DatabaseNotReadyMiddleware
{
	/// <summary>
	/// The <c>Retry-After</c> header value (in seconds) included in responses for transient failures.
	/// </summary>
	/// <remarks>
	/// This tells clients and proxies how long to wait before retrying. The value is a reasonable
	/// default that balances responsiveness with avoiding unnecessary load during recovery.
	/// </remarks>
	private const string RetryAfterSeconds = "10";

	private readonly RequestDelegate mNext;

	/// <summary>
	/// Initializes a new instance of the <see cref="DatabaseNotReadyMiddleware"/> class.
	/// </summary>
	/// <param name="next">The next middleware in the pipeline.</param>
	public DatabaseNotReadyMiddleware(RequestDelegate next)
	{
		mNext = next;
	}

	/// <summary>
	/// Invokes the middleware, checking database readiness before proceeding.
	/// </summary>
	/// <param name="context">The HTTP context for the current request.</param>
	/// <param name="initializationStatus">The database initialization status service.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task InvokeAsync(HttpContext context, DatabaseInitializationStatus initializationStatus)
	{
		// Always allow health endpoints through so monitoring can report the failure.
		if (IsHealthEndpoint(context.Request.Path))
		{
			await mNext(context).ConfigureAwait(false);
			return;
		}

		// Only check database readiness for API requests.
		// Non-API requests (e.g., static files, MVC views) are allowed through regardless of database status.
		if (!context.Request.Path.StartsWithSegments("/api"))
		{
			await mNext(context).ConfigureAwait(false);
			return;
		}

		// If database is ready, proceed normally.
		if (initializationStatus.IsReady)
		{
			await mNext(context).ConfigureAwait(false);
			return;
		}

		// Database not ready — determine the appropriate ProblemDetails response.
		// The error type and Retry-After header differ based on whether the issue is transient
		// (will resolve on its own) or requires operator intervention.
		(string type, string title, string detail, bool isRetryable) = GetProblemInfo(initializationStatus);

		string traceId = Activity.Current?.Id ?? context.TraceIdentifier;

		// Signal to clients/proxies that retrying makes sense and when to try again.
		// Omitted for non-transient failures to avoid misleading retry loops.
		if (isRetryable)
			context.Response.Headers.RetryAfter = RetryAfterSeconds;

		await Results
			.Problem(
				statusCode: StatusCodes.Status503ServiceUnavailable,
				type: type,
				title: title,
				detail: detail,
				extensions: new Dictionary<string, object?> { ["traceId"] = traceId })
			.ExecuteAsync(context)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Determines the ProblemDetails fields based on the current database state and failure category.
	/// </summary>
	/// <param name="status">The current database initialization status.</param>
	/// <returns>
	/// A tuple containing the RFC 7807 <c>type</c> URN, the <c>title</c>, the <c>detail</c> message,
	/// and whether the failure is retryable (i.e., a <c>Retry-After</c> header should be included).
	/// </returns>
	/// <remarks>
	///     <para>
	///     The method maps database states and failure categories to differentiated ProblemDetails responses:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <b>Transient</b> (<see cref="DatabaseInitializationState.NotStarted"/>,
	///             <see cref="DatabaseInitializationState.InProgress"/>,
	///             <see cref="DatabaseInitializationState.Disconnected"/>,
	///             <see cref="DatabaseFailureCategory.Transient"/>): Uses <see cref="ErrorTypes.ServiceUnavailable"/>
	///             with <c>Retry-After</c> — clients should retry.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <b>Configuration required</b> (<see cref="DatabaseFailureCategory.ConfigurationRequired"/>):
	///             Uses <see cref="ErrorTypes.DatabaseConfigurationRequired"/> without <c>Retry-After</c> —
	///             operator action needed.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <b>Manual intervention</b> (<see cref="DatabaseFailureCategory.ManualInterventionRequired"/>):
	///             Uses <see cref="ErrorTypes.DatabaseFailed"/> without <c>Retry-After</c> — manual repair needed.
	///             </description>
	///         </item>
	///     </list>
	/// </remarks>
	private static (string Type, string Title, string Detail, bool IsRetryable) GetProblemInfo(
		DatabaseInitializationStatus status)
	{
		return status.State switch
		{
			DatabaseInitializationState.NotStarted => (
				                                          ErrorTypes.ServiceUnavailable,
				                                          "Service Starting",
				                                          "Database initialization has not started. The service is starting up.",
				                                          true),

			DatabaseInitializationState.InProgress => (
				                                          ErrorTypes.ServiceUnavailable,
				                                          "Service Starting",
				                                          "Database initialization is in progress. Please retry shortly.",
				                                          true),

			DatabaseInitializationState.Disconnected => (
				                                            ErrorTypes.ServiceUnavailable,
				                                            "Service Temporarily Unavailable",
				                                            status.FailureMessage ??
				                                            "Database connection lost. The service will automatically recover.",
				                                            true),

			DatabaseInitializationState.Failed => GetFailedProblemInfo(status),

			// All enum values handled above.
			var _ => throw new UnreachableException()
		};
	}

	/// <summary>
	/// Determines the ProblemDetails fields for the <see cref="DatabaseInitializationState.Failed"/> state
	/// based on the <see cref="DatabaseInitializationStatus.FailureCategory"/>.
	/// </summary>
	/// <param name="status">The current database initialization status (must be in <c>Failed</c> state).</param>
	/// <returns>
	/// A tuple containing the RFC 7807 <c>type</c> URN, the <c>title</c>, the <c>detail</c> message,
	/// and whether the failure is retryable.
	/// </returns>
	private static (string Type, string Title, string Detail, bool IsRetryable) GetFailedProblemInfo(
		DatabaseInitializationStatus status)
	{
		return status.FailureCategory switch
		{
			DatabaseFailureCategory.ConfigurationRequired => (
				                                                 ErrorTypes.DatabaseConfigurationRequired,
				                                                 "Database Configuration Required",
				                                                 status.FailureMessage ??
				                                                 "Database configuration is incomplete. Manual action is required.",
				                                                 false),

			DatabaseFailureCategory.ManualInterventionRequired => (
				                                                      ErrorTypes.DatabaseFailed,
				                                                      "Database Error",
				                                                      status.FailureMessage ??
				                                                      "Database initialization failed. Manual intervention is required.",
				                                                      false),

			// Transient or null — the recovery service will retry automatically.
			var _ => (
				         ErrorTypes.ServiceUnavailable,
				         "Service Temporarily Unavailable",
				         status.FailureMessage ??
				         "Database initialization failed. The service will retry automatically.",
				         true)
		};
	}

	/// <summary>
	/// Determines whether the specified path is a health check endpoint that should bypass the database readiness check.
	/// </summary>
	/// <param name="path">The request path to check.</param>
	/// <returns>
	/// <see langword="true"/> if the path is a health endpoint; otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method recognizes two categories of health endpoints:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             Infrastructure probes: <c>/health</c>, <c>/health/ready</c>, <c>/health/live</c>
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             Versioned API endpoints: <c>/api/v1/health</c>, <c>/api/v2/health/live</c>, etc.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     For versioned API paths, the method checks that "health" is the <b>third</b> path segment
	///     (after "api" and the version). This prevents false positives such as
	///     <c>/api/v1/users/health-records</c> being incorrectly identified as a health endpoint.
	///     </para>
	/// </remarks>
	private static bool IsHealthEndpoint(PathString path)
	{
		// Infrastructure health probe (readiness check): /health, /health/ready, /health/live
		if (path.StartsWithSegments("/health"))
			return true;

		// Versioned API health endpoints: /api/v1/health, /api/v2/health/live, etc.
		// We check if "health" is the third path segment (after "api" and version).
		// This avoids false positives like /api/v1/users/health-records.
		if (path.Value is { Length: > 0 } pathValue &&
		    pathValue.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
		{
			ReadOnlySpan<char> remaining = pathValue.AsSpan()[5..]; // Skip "/api/"

			// Skip the version segment (e.g., "v1/")
			int slashIndex = remaining.IndexOf('/');
			if (slashIndex > 0 && remaining.Length > slashIndex + 1)
			{
				ReadOnlySpan<char> afterVersion = remaining[(slashIndex + 1)..];

				// Check if the next segment is exactly "health" (with or without trailing path)
				int nextSlash = afterVersion.IndexOf('/');
				ReadOnlySpan<char> segment = nextSlash >= 0 ? afterVersion[..nextSlash] : afterVersion;

				if (segment.Equals("health", StringComparison.OrdinalIgnoreCase))
					return true;
			}
		}

		return false;
	}
}
