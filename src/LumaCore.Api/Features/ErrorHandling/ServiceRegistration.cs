// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LumaCore.Api.Features.ErrorHandling;

/// <summary>
/// Provides extension methods to register the Error Handling feature services.
/// </summary>
/// <remarks>
///     <para>
///     The Error Handling feature provides centralized exception handling with:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="LumaCoreExceptionHandler"/> – Converts unhandled exceptions
///             into RFC 7807 <see cref="ProblemDetails"/> responses with trace correlation.
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="ErrorTypes"/> – URN-based error type identifiers for
///             machine-readable error categorization.
///             </description>
///         </item>
///     </list>
///     <para>
///     This feature works in conjunction with the <c>AddProblemDetails()</c> service
///     registration and the <c>UseExceptionHandler()</c> middleware.
///     </para>
/// </remarks>
static class ServiceRegistration
{
	/// <summary>
	/// Adds the Error Handling feature services to the dependency injection container.
	/// </summary>
	/// <param name="builder">
	/// The <see cref="WebApplicationBuilder"/> to add services to.
	/// </param>
	/// <returns>The <paramref name="builder"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method registers <see cref="LumaCoreExceptionHandler"/> as an
	///     <see cref="IExceptionHandler"/> implementation. When the exception handler
	///     middleware runs, it will invoke this handler to produce consistent
	///     <see cref="ProblemDetails"/> responses.
	///     </para>
	///     <para>
	///     <b>Prerequisites:</b> Ensure <c>AddProblemDetails()</c> is called before
	///     this method to enable the <see cref="ProblemDetails"/> infrastructure.
	///     </para>
	///     <para>
	///     <b>Pipeline configuration:</b> Call <c>UseExceptionHandler()</c> in the
	///     middleware pipeline to activate exception handling.
	///     </para>
	/// </remarks>
	/// <example>
	/// Register in <c>Program.Services.cs</c>:
	/// <code>
	/// builder.Services.AddProblemDetails();
	/// builder.AddErrorHandlingFeature();
	/// </code>
	/// </example>
	public static WebApplicationBuilder AddErrorHandlingFeature(this WebApplicationBuilder builder)
	{
		// Register the custom exception handler.
		// Multiple IExceptionHandler implementations can be registered; they are
		// invoked in registration order until one returns true (handled).
		builder.Services.AddExceptionHandler<LumaCoreExceptionHandler>();

		return builder;
	}
}
