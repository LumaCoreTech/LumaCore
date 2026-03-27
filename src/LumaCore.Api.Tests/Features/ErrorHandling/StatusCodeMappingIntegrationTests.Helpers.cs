// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Tests.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LumaCore.Api.Tests.Features.ErrorHandling;

// Disable "Remove unnecessary lambda parameter" (IDE0200) to preserve the readability of the
// dynamic endpoint mappings and bind the lambda parameters to the route template.
#pragma warning disable IDE0200

public sealed partial class StatusCodeMappingIntegrationTests
{
	/// <summary>
	/// The API path prefix for the dynamic status code endpoint. Requests to
	/// <c>/api/status/{code}</c> are subject to the status code mapping middleware.
	/// </summary>
	private const string ApiStatusEndpoint = "/api/status";

	/// <summary>
	/// The non-API path prefix for the dynamic status code endpoint. Requests to
	/// <c>/status/{code}</c> are outside the <c>/api/</c> scope and should not be transformed.
	/// </summary>
	private const string NonApiStatusEndpoint = "/status";

	/// <summary>
	/// Creates a <see cref="MiddlewareTestHarness"/> with <see cref="MiddlewareIntegration.UseErrorHandlingFeature"/>
	/// and two dynamic endpoints that return the requested status code without a body.
	/// </summary>
	/// <returns>A disposable harness ready for HTTP requests.</returns>
	private static Task<MiddlewareTestHarness> CreateHarnessAsync()
	{
		return MiddlewareTestHarness.CreateAsync(
			builder => { builder.Services.AddProblemDetails(); },
			app =>
			{
				app.UseErrorHandlingFeature();
				app.UseRouting();
				app.MapGet(
					$"{ApiStatusEndpoint}/{{code:int}}",
					(int code) => Results.StatusCode(code));
				app.MapGet(
					$"{NonApiStatusEndpoint}/{{code:int}}",
					(int code) => Results.StatusCode(code));
			});
	}
}
