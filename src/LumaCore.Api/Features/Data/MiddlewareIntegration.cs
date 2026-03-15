// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Microsoft.AspNetCore.Mvc;

namespace LumaCore.Api.Features.Data;

/// <summary>
/// Provides extension methods for integrating data feature middleware into the request pipeline.
/// </summary>
static class MiddlewareIntegration
{
	/// <summary>
	/// Adds middleware that rejects API requests when the database is not ready.
	/// </summary>
	/// <param name="app">The <see cref="WebApplication"/> to configure.</param>
	/// <returns>The <paramref name="app"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This middleware checks <see cref="DatabaseInitializationStatus"/> and returns HTTP 503
	///     Service Unavailable with RFC 7807 <see cref="ProblemDetails"/> for API requests
	///     if the database initialization failed or is still in progress.
	///     </para>
	///     <para>
	///     <b>Excluded endpoints:</b> Health check endpoints (<c>/health</c>, <c>/api/v{version}/health/*</c>) are
	///     always allowed through so that monitoring systems can query application status even when the database is
	///     unavailable.
	///     </para>
	///     <para>
	///     <b>Pipeline position:</b> This should be called early in the pipeline, after error handling middleware but
	///     before authentication, to prevent database-dependent middleware from failing.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// // In Program.Pipeline.cs
	/// app.UseErrorHandlingFeature();
	/// app.UseDatabaseReadinessCheck();  // Before authentication
	/// app.UseHttpsRedirectionFeature();
	/// </code>
	/// </example>
	public static WebApplication UseDatabaseReadinessCheck(this WebApplication app)
	{
		app.UseMiddleware<DatabaseNotReadyMiddleware>();
		return app;
	}
}
