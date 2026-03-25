// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Asp.Versioning;
using Asp.Versioning.Builder;

using LumaCore.Api.Features.ApiVersioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Xunit;

namespace LumaCore.Api.Tests.Features.ApiVersioning;

/// <summary>
/// Integration tests for <see cref="VersionedApiGroup"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify that <see cref="VersionedApiGroup.MapVersionedApiGroup"/> creates a
///     <see cref="RouteGroupBuilder"/> with the correct versioned route prefix and
///     <see cref="ApiVersionSet"/> configuration. Each test maps a probe endpoint on the group and
///     inspects the resulting <see cref="RouteEndpoint"/> metadata.
///     </para>
///     <para>
///     <b>Not covered by metadata inspection:</b> The <c>ReportApiVersions()</c> header behavior and
///     <c>WithValidation()</c> endpoint filter are runtime concerns that require HTTP-level integration tests.
///     </para>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>MapVersionedApiGroup — route prefix and version set verification.</item>
///     </list>
/// </remarks>
[Trait("Category", "ApiVersioning")]
public sealed partial class VersionedApiGroupTests
{
	/// <summary>
	/// Verifies that endpoints mapped on the <see cref="RouteGroupBuilder"/> returned by
	/// <see cref="VersionedApiGroup.MapVersionedApiGroup"/> have the versioned route prefix
	/// <c>/api/v{version:apiVersion}</c> prepended to their route pattern.
	/// </summary>
	[Fact]
	public void MapVersionedApiGroup_EndpointMappedViaGroup_HasVersionedRoutePrefix()
	{
		// Arrange
		WebApplication app = CreateApp();
		RouteGroupBuilder api = app.MapVersionedApiGroup();
		api.MapGet("/items", () => Results.Ok())
			.MapToApiVersion(ApiVersions.V1);

		// Act
		RouteEndpoint endpoint = GetSingleRouteEndpoint(app);

		// Assert
		Assert.Equal("/api/v{version:apiVersion}/items", endpoint.RoutePattern.RawText);
	}

	/// <summary>
	/// Verifies that endpoints mapped on the group inherit the <see cref="ApiVersionSet"/> configured by
	/// <see cref="VersionedApiGroup.MapVersionedApiGroup"/>, with <see cref="ApiVersions.V1"/> as the only
	/// explicitly declared version.
	/// </summary>
	[Fact]
	public void MapVersionedApiGroup_EndpointMappedViaGroup_InheritsApiVersionSet()
	{
		// Arrange
		WebApplication app = CreateApp();
		RouteGroupBuilder api = app.MapVersionedApiGroup();
		api.MapGet("/items", () => Results.Ok())
			.MapToApiVersion(ApiVersions.V1);

		// Act
		RouteEndpoint endpoint = GetSingleRouteEndpoint(app);
		var metadata = endpoint.Metadata.GetMetadata<ApiVersionMetadata>();

		// Assert
		Assert.NotNull(metadata);
		ApiVersionModel model = metadata.Map(ApiVersionMapping.Explicit);
		ApiVersion declaredVersion = Assert.Single(model.DeclaredApiVersions);
		Assert.Equal(ApiVersions.V1, declaredVersion);
	}
}
