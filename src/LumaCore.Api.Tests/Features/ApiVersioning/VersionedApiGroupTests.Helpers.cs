// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.ApiVersioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

using Xunit;

namespace LumaCore.Api.Tests.Features.ApiVersioning;

public sealed partial class VersionedApiGroupTests
{
	/// <summary>
	/// Creates a minimal <see cref="WebApplication"/> with API versioning services registered.
	/// Tests call <see cref="VersionedApiGroup.MapVersionedApiGroup"/> on the returned application
	/// to obtain the versioned <see cref="RouteGroupBuilder"/> under test.
	/// </summary>
	/// <returns>A built <see cref="WebApplication"/> ready for endpoint mapping.</returns>
	private static WebApplication CreateApp()
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder();
		builder.Services.AddApiVersioningFeatureCore();
		return builder.Build();
	}

	/// <summary>
	/// Returns the single <see cref="RouteEndpoint"/> registered on the application.
	/// Fails the test if zero or more than one endpoint is found.
	/// </summary>
	/// <param name="app">The application to inspect.</param>
	/// <returns>The single <see cref="RouteEndpoint"/>.</returns>
	private static RouteEndpoint GetSingleRouteEndpoint(WebApplication app)
	{
		return Assert.Single(
			((IEndpointRouteBuilder)app)
			.DataSources
			.SelectMany(ds => ds.Endpoints)
			.OfType<RouteEndpoint>());
	}
}
