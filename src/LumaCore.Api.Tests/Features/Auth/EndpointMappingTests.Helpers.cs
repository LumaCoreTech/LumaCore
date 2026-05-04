// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.Auth;
using LumaCore.Api.Features.UserManagement;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
using Xunit.Internal;

namespace LumaCore.Api.Tests.Features.Auth;

public sealed partial class EndpointMappingTests
{
	/// <summary>
	/// Creates a minimal <see cref="WebApplication"/> with API versioning services registered and null stubs for
	/// auth-specific services. Returns both the application and the versioned <see cref="RouteGroupBuilder"/> that
	/// <see cref="EndpointMapping.MapAuthFeature"/> should be called on.
	/// </summary>
	/// <returns>
	/// A tuple of the built <see cref="WebApplication"/> and the versioned <see cref="RouteGroupBuilder"/> ready
	/// for <see cref="EndpointMapping.MapAuthFeature"/>.
	/// </returns>
	/// <remarks>
	/// The null stubs for <see cref="IJwtTokenFactory"/>, <see cref="ITokenRevocationService"/>,
	/// <see cref="IUserAuthenticationService"/>, and <see cref="TimeProvider"/> are required because
	/// <c>RequestDelegateFactory</c> checks
	/// <c>IServiceProviderIsService.IsService()</c> at endpoint build time. Since tests only inspect endpoint
	/// metadata and never invoke handlers, the stubs are never called.
	/// </remarks>
	private static (WebApplication App, RouteGroupBuilder ApiGroup) CreateApp()
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder();
		builder.Services.AddApiVersioningFeatureCore();

		// Null stubs — tests inspect metadata only, handlers are never invoked.
		builder.Services.AddSingleton<IJwtTokenFactory>(_ => null!);
		builder.Services.AddSingleton<ITokenRevocationService>(_ => null!);
		builder.Services.AddSingleton<IUserAuthenticationService>(_ => null!);
		builder.Services.AddSingleton(TimeProvider.System);

		WebApplication app = builder.Build();
		RouteGroupBuilder apiGroup = app.MapVersionedApiGroup();

		return (app, apiGroup);
	}

	/// <summary>
	/// Returns all <see cref="RouteEndpoint"/> instances whose route pattern contains <c>/auth/</c>.
	/// </summary>
	/// <param name="app">The application to inspect.</param>
	/// <returns>A read-only list of authentication-related route endpoints.</returns>
	private static IReadOnlyList<RouteEndpoint> GetAuthEndpoints(WebApplication app)
	{
		return ((IEndpointRouteBuilder)app)
			.DataSources
			.SelectMany(ds => ds.Endpoints)
			.OfType<RouteEndpoint>()
			.Where(e => e.RoutePattern.RawText?.Contains("/auth/") == true)
			.CastOrToReadOnlyList();
	}

	/// <summary>
	/// Finds a single <see cref="RouteEndpoint"/> by route suffix and HTTP method.
	/// Fails the test with a descriptive message if no matching endpoint is found.
	/// </summary>
	/// <param name="app">The application to inspect.</param>
	/// <param name="routeSuffix">
	/// The expected route suffix (e.g., <c>/auth/login</c>). Matched via <see cref="string.EndsWith(string)"/>
	/// on the route pattern's raw text.
	/// </param>
	/// <param name="httpMethod">
	/// The expected HTTP method (e.g., <c>POST</c>). Matched via
	/// <see cref="IHttpMethodMetadata.HttpMethods"/>.
	/// </param>
	/// <returns>The matching <see cref="RouteEndpoint"/>.</returns>
	private static RouteEndpoint FindEndpoint(WebApplication app, string routeSuffix, string httpMethod)
	{
		RouteEndpoint? match = ((IEndpointRouteBuilder)app)
			.DataSources
			.SelectMany(ds => ds.Endpoints)
			.OfType<RouteEndpoint>()
			.FirstOrDefault(e =>
				e.RoutePattern.RawText?.EndsWith(routeSuffix, StringComparison.Ordinal) == true &&
				e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(httpMethod) == true);

		Assert.NotNull(match);
		return match;
	}
}
