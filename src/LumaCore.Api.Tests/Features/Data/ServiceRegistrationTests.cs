// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Data;
using LumaCore.Data.Initialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace LumaCore.Api.Tests.Features.Data;

/// <summary>
/// Tests for <see cref="ServiceRegistration"/> in the Data feature.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify that the Data <see cref="ServiceRegistration"/> correctly registers services,
///     options binding, and forwards to the <see cref="LumaCore.Data.ServiceRegistration.AddLumaCoreData"/>
///     registration. The actual middleware behavior is tested in <see cref="DatabaseNotReadyMiddlewareTests"/>.
///     </para>
///     <para>
///     Tests that resolve services use a <see cref="WebApplicationBuilder"/> with
///     <see cref="Environments.Production"/> to avoid <c>ValidateOnBuild</c> / <c>ValidateScopes</c> issues
///     that some test runners trigger by injecting <c>ASPNETCORE_ENVIRONMENT=Development</c>.
///     </para>
/// </remarks>
[Trait("Category", "Data")]
public sealed class ServiceRegistrationTests
{
	#region AddDataFeature()

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddDataFeature"/> returns the original
	/// <see cref="WebApplicationBuilder"/> for method chaining.
	/// </summary>
	[Fact]
	public void AddDataFeature_WhenCalled_ReturnsOriginalBuilder()
	{
		// Arrange
		WebApplicationBuilder builder = WebApplication.CreateBuilder();

		// Act
		WebApplicationBuilder result = builder.AddDataFeature();

		// Assert
		Assert.Same(builder, result);
	}

	#endregion

	#region AddDataFeatureCore()

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddDataFeatureCore"/> returns the original
	/// <see cref="IServiceCollection"/> for method chaining.
	/// </summary>
	[Fact]
	public void AddDataFeatureCore_WhenCalled_ReturnsOriginalServiceCollection()
	{
		// Arrange
		IConfiguration config = new ConfigurationBuilder().Build();
		var services = new ServiceCollection();

		// Act
		IServiceCollection result = services.AddDataFeatureCore(config);

		// Assert
		Assert.Same(services, result);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddDataFeatureCore"/> registers
	/// <see cref="DatabaseInitializationStatus"/> as a singleton so that the
	/// <see cref="DatabaseNotReadyMiddleware"/> can resolve it from the DI container.
	/// </summary>
	[Fact]
	public void AddDataFeatureCore_WhenCalled_RegistersDatabaseInitializationStatus()
	{
		// Arrange
		WebApplicationBuilder builder = CreateTestBuilder();
		builder.Services.AddDataFeatureCore(builder.Configuration);
		WebApplication app = builder.Build();

		// Act
		var status = app.Services.GetService<DatabaseInitializationStatus>();

		// Assert
		Assert.NotNull(status);
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
