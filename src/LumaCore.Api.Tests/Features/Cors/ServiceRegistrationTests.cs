// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Cors;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Xunit;

using CorsOptions = LumaCore.Api.Features.Cors.CorsOptions;

namespace LumaCore.Api.Tests.Features.Cors;

/// <summary>
/// Tests for <see cref="ServiceRegistration"/> in the CORS feature.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify that the CORS <see cref="ServiceRegistration"/> correctly registers options binding,
///     options validation, and ASP.NET Core CORS services. The actual CORS policy configuration is tested
///     separately in <see cref="CorsIntegrationTests"/>.
///     </para>
///     <para>
///     Tests that resolve options use a <see cref="WebApplicationBuilder"/> with
///     <see cref="Environments.Production"/> to avoid <c>ValidateOnBuild</c> / <c>ValidateScopes</c> issues
///     that some test runners trigger by injecting <c>ASPNETCORE_ENVIRONMENT=Development</c>.
///     </para>
/// </remarks>
[Trait("Category", "Cors")]
public sealed class ServiceRegistrationTests
{
	#region AddCorsFeature()

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddCorsFeature"/> returns the original
	/// <see cref="WebApplicationBuilder"/> for method chaining.
	/// </summary>
	[Fact]
	public void AddCorsFeature_WhenCalled_ReturnsOriginalBuilder()
	{
		// Arrange
		WebApplicationBuilder builder = WebApplication.CreateBuilder();

		// Act
		WebApplicationBuilder result = builder.AddCorsFeature();

		// Assert
		Assert.Same(builder, result);
	}

	#endregion

	#region AddCorsFeatureCore()

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddCorsFeatureCore"/> returns the original
	/// <see cref="IServiceCollection"/> for method chaining.
	/// </summary>
	[Fact]
	public void AddCorsFeatureCore_WhenCalled_ReturnsOriginalServiceCollection()
	{
		// Arrange
		IConfiguration config = new ConfigurationBuilder().Build();
		var services = new ServiceCollection();

		// Act
		IServiceCollection result = services.AddCorsFeatureCore(config);

		// Assert
		Assert.Same(services, result);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddCorsFeatureCore"/> binds <see cref="Api.Features.Cors.CorsOptions"/>
	/// from
	/// the <c>Cors</c> configuration section.
	/// </summary>
	[Fact]
	public void AddCorsFeatureCore_WhenCalled_BindsCorsOptionsFromConfiguration()
	{
		// Arrange
		var config = new Dictionary<string, string?>
		{
			["Cors:Enabled"] = "true",
			["Cors:AllowedOrigins:0"] = "https://example.com",
			["Cors:AllowCredentials"] = "true",
			["Cors:AllowedMethods:0"] = "GET",
			["Cors:AllowedHeaders:0"] = "Authorization",
			["Cors:ExposedHeaders:0"] = "X-Request-Id",
			["Cors:PreflightMaxAge"] = "3600"
		};
		WebApplicationBuilder builder = CreateTestBuilder();
		builder.Configuration.AddInMemoryCollection(config);
		builder.Services.AddCorsFeatureCore(builder.Configuration);
		WebApplication app = builder.Build();

		// Act
		CorsOptions options = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;

		// Assert
		Assert.True(options.Enabled);
		Assert.Equal("https://example.com", Assert.Single(options.AllowedOrigins));
		Assert.True(options.AllowCredentials);
		Assert.Equal("GET", Assert.Single(options.AllowedMethods));
		Assert.Equal("Authorization", Assert.Single(options.AllowedHeaders));
		Assert.Equal("X-Request-Id", Assert.Single(options.ExposedHeaders));
		Assert.Equal(3600, options.PreflightMaxAge);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddCorsFeatureCore"/> registers ASP.NET Core's CORS
	/// infrastructure so that <see cref="Microsoft.AspNetCore.Cors.Infrastructure.ICorsService"/> is resolvable
	/// from the DI container.
	/// </summary>
	[Fact]
	public void AddCorsFeatureCore_WhenCalled_RegistersCorsServices()
	{
		// Arrange
		WebApplicationBuilder builder = CreateTestBuilder();
		builder.Services.AddCorsFeatureCore(builder.Configuration);
		WebApplication app = builder.Build();

		// Act
		var corsService = app.Services.GetService<ICorsService>();

		// Assert
		Assert.NotNull(corsService);
	}

	#endregion

	/// <summary>
	/// Creates a <see cref="WebApplicationBuilder"/> with <see cref="Environments.Production"/> to avoid
	/// DI validation issues in test runners that inject <c>ASPNETCORE_ENVIRONMENT=Development</c>.
	/// </summary>
	/// <returns>A builder suitable for tests that call <see cref="WebApplicationBuilder.Build"/>.</returns>
	private static WebApplicationBuilder CreateTestBuilder() => WebApplication.CreateBuilder(
		new WebApplicationOptions
		{
			EnvironmentName = Environments.Production
		});
}
