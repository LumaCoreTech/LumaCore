// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Asp.Versioning;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

/// <summary>
/// Integration tests for <see cref="EndpointMapping"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify that <see cref="EndpointMapping.MapAuthFeature"/> registers the expected set of
///     authentication endpoints with correct HTTP methods, route patterns, API version mappings, and
///     authorization requirements. Tests inspect endpoint metadata only — handlers are never invoked.
///     </para>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>MapAuthFeature — chaining, endpoint count, per-endpoint auth, API version mapping.</item>
///     </list>
/// </remarks>
[Trait("Category", "Auth")]
public sealed partial class EndpointMappingTests
{
	/// <summary>
	/// Verifies that <see cref="EndpointMapping.MapAuthFeature"/> returns the original
	/// <see cref="IEndpointRouteBuilder"/> for method chaining.
	/// </summary>
	[Fact]
	public void MapAuthFeature_WhenCalled_ReturnsOriginalEndpointRouteBuilder()
	{
		// Arrange
		(WebApplication _, RouteGroupBuilder apiGroup) = CreateApp();

		// Act
		IEndpointRouteBuilder result = apiGroup.MapAuthFeature();

		// Assert
		Assert.Same(apiGroup, result);
	}

	/// <summary>
	/// Verifies that <see cref="EndpointMapping.MapAuthFeature"/> registers exactly four authentication
	/// endpoints: login, logout, whoami, and introspect.
	/// </summary>
	[Fact]
	public void MapAuthFeature_WhenCalled_RegistersExactlyFourAuthEndpoints()
	{
		// Arrange
		(WebApplication app, RouteGroupBuilder apiGroup) = CreateApp();
		apiGroup.MapAuthFeature();

		// Act
		IReadOnlyList<RouteEndpoint> endpoints = GetAuthEndpoints(app);

		// Assert — count + identity: exactly these 4 endpoints and no others.
		Assert.Equal(4, endpoints.Count);
		Assert.Contains(
			endpoints,
			e => e.RoutePattern.RawText!.EndsWith("/auth/login", StringComparison.Ordinal));
		Assert.Contains(
			endpoints,
			e => e.RoutePattern.RawText!.EndsWith("/auth/logout", StringComparison.Ordinal));
		Assert.Contains(
			endpoints,
			e => e.RoutePattern.RawText!.EndsWith("/auth/whoami", StringComparison.Ordinal));
		Assert.Contains(
			endpoints,
			e => e.RoutePattern.RawText!.EndsWith("/auth/introspect", StringComparison.Ordinal));
	}

	/// <summary>
	/// Provides test data for <see cref="MapAuthFeature_RegisteredEndpoint_HasExpectedAuthorizationRequirement"/>.
	/// Each row describes one of the four authentication endpoints and whether it allows anonymous access.
	/// </summary>
	public static TheoryData<string, string, string, bool> AuthEndpointData => new()
	{
		// caseName, routeSuffix, httpMethod, allowAnonymous
		{ "Login", "/auth/login", "POST", true },          // Only anonymous — issues tokens
		{ "Logout", "/auth/logout", "POST", false },       // Requires auth — revokes token
		{ "WhoAmI", "/auth/whoami", "GET", false },        // Requires auth — identity info
		{ "Introspect", "/auth/introspect", "GET", false } // Requires auth — token diagnostics
	};

	/// <summary>
	/// Verifies that each authentication endpoint has the correct authorization requirement: login allows
	/// anonymous access, while logout, whoami, and introspect require authorization.
	/// </summary>
	/// <param name="caseName">A descriptive name for the test case.</param>
	/// <param name="routeSuffix">The route suffix used to locate the endpoint (e.g., <c>/auth/login</c>).</param>
	/// <param name="httpMethod">The HTTP method of the endpoint (e.g., <c>POST</c>).</param>
	/// <param name="allowAnonymous">
	/// <see langword="true"/> if the endpoint allows anonymous access; <see langword="false"/> if it requires
	/// authorization.
	/// </param>
	[Theory]
	[MemberData(nameof(AuthEndpointData))]
	public void MapAuthFeature_RegisteredEndpoint_HasExpectedAuthorizationRequirement(
		string caseName,
		string routeSuffix,
		string httpMethod,
		bool   allowAnonymous)
	{
		// Arrange
		_ = caseName;
		(WebApplication app, RouteGroupBuilder apiGroup) = CreateApp();
		apiGroup.MapAuthFeature();

		// Act
		RouteEndpoint endpoint = FindEndpoint(app, routeSuffix, httpMethod);

		// Assert — verify the expected metadata is present AND the opposite is absent,
		// ruling out accidental dual-declaration (e.g., .AllowAnonymous().RequireAuthorization()).
		if (allowAnonymous)
		{
			Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
			Assert.Null(endpoint.Metadata.GetMetadata<IAuthorizeData>());
		}
		else
		{
			Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
			Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
		}
	}

	/// <summary>
	/// Verifies that all authentication endpoints are mapped to <see cref="ApiVersions.V1"/>.
	/// </summary>
	[Fact]
	public void MapAuthFeature_AllRegisteredEndpoints_AreMappedToApiVersionV1()
	{
		// Arrange
		(WebApplication app, RouteGroupBuilder apiGroup) = CreateApp();
		apiGroup.MapAuthFeature();

		// Act
		IReadOnlyList<RouteEndpoint> endpoints = GetAuthEndpoints(app);

		// Assert
		foreach (RouteEndpoint endpoint in endpoints)
		{
			var metadata = endpoint.Metadata.GetMetadata<ApiVersionMetadata>();
			Assert.NotNull(metadata);
			ApiVersionModel model = metadata.Map(ApiVersionMapping.Explicit);
			ApiVersion declaredVersion = Assert.Single(model.DeclaredApiVersions);
			Assert.Equal(ApiVersions.V1, declaredVersion);
		}
	}
}
