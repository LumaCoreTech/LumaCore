// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using LumaCore.Api.Features.ApiVersioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Api.Tests.Features.ApiVersioning;

/// <summary>
/// Tests for <see cref="ServiceRegistration"/> in the API versioning feature.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify that <see cref="ServiceRegistration.AddApiVersioningFeatureCore"/> configures API versioning
///     correctly: default version, version reporting, URL segment reader, and API explorer settings.
///     </para>
///     <para>
///     Tests that verify resolved options use <see cref="WebApplication.CreateBuilder()"/> to provide the full
///     ASP.NET Core service infrastructure required by the versioning library.
///     </para>
/// </remarks>
[Trait("Category", "ApiVersioning")]
public sealed class ServiceRegistrationTests
{
	#region AddApiVersioningFeature()

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddApiVersioningFeature"/> returns the original
	/// <see cref="WebApplicationBuilder"/> for method chaining.
	/// </summary>
	[Fact]
	public void AddApiVersioningFeature_WhenCalled_ReturnsOriginalBuilder()
	{
		// Arrange
		WebApplicationBuilder builder = WebApplication.CreateBuilder();

		// Act
		WebApplicationBuilder result = builder.AddApiVersioningFeature();

		// Assert
		Assert.Same(builder, result);
	}

	#endregion

	#region AddApiVersioningFeatureCore()

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddApiVersioningFeatureCore"/> returns the original
	/// <see cref="IServiceCollection"/> for method chaining.
	/// </summary>
	[Fact]
	public void AddApiVersioningFeatureCore_WhenCalled_ReturnsOriginalServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		IServiceCollection result = services.AddApiVersioningFeatureCore();

		// Assert
		Assert.Same(services, result);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddApiVersioningFeatureCore"/> configures
	/// <see cref="ApiVersioningOptions"/> with <see cref="ApiVersions.V1"/> as the default version, version
	/// reporting enabled, and <see cref="UrlSegmentApiVersionReader"/> as the version reader.
	/// </summary>
	[Fact]
	public void AddApiVersioningFeatureCore_WhenCalled_ConfiguresApiVersioningOptions()
	{
		// Arrange
		WebApplicationBuilder builder = WebApplication.CreateBuilder();

		// Act
		builder.Services.AddApiVersioningFeatureCore();
		WebApplication app = builder.Build();
		ApiVersioningOptions options = app.Services
			.GetRequiredService<IOptions<ApiVersioningOptions>>()
			.Value;

		// Assert
		Assert.Equal(ApiVersions.V1, options.DefaultApiVersion);
		Assert.True(options.ReportApiVersions);
		Assert.IsType<UrlSegmentApiVersionReader>(options.ApiVersionReader);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddApiVersioningFeatureCore"/> configures
	/// <see cref="ApiExplorerOptions"/> with the <c>"'v'VVV"</c> group name format and URL version substitution
	/// enabled.
	/// </summary>
	[Fact]
	public void AddApiVersioningFeatureCore_WhenCalled_ConfiguresApiExplorerOptions()
	{
		// Arrange
		WebApplicationBuilder builder = WebApplication.CreateBuilder();

		// Act
		builder.Services.AddApiVersioningFeatureCore();
		WebApplication app = builder.Build();
		ApiExplorerOptions options = app.Services
			.GetRequiredService<IOptions<ApiExplorerOptions>>()
			.Value;

		// Assert
		// 'v' = literal prefix, VVV = Major[.Minor][-Status] → produces OpenAPI document group names
		// like "v1", "v2.1", "v3-beta", served at /openapi/v1.json etc.
		Assert.Equal("'v'VVV", options.GroupNameFormat);

		// Replaces the {version:apiVersion} route placeholder with the actual version in OpenAPI paths,
		// so documents show /api/v1/items instead of /api/{version:apiVersion}/items.
		Assert.True(options.SubstituteApiVersionInUrl);
	}

	#endregion
}
