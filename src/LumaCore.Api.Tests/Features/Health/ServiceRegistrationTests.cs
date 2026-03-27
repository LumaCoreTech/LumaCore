// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Health;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Xunit;

namespace LumaCore.Api.Tests.Features.Health;

/// <summary>
/// Tests for <see cref="ServiceRegistration.AddHealthFeature"/> verifying correct DI registration.
/// </summary>
[Trait("Category", "Health")]
public sealed class ServiceRegistrationTests
{
	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddHealthFeature"/> returns the same
	/// <see cref="WebApplicationBuilder"/> instance for fluent chaining.
	/// </summary>
	[Fact]
	public void AddHealthFeature_ReturnsSameBuilder()
	{
		// Arrange
		WebApplicationBuilder builder = WebApplication.CreateBuilder();

		// Act
		WebApplicationBuilder result = builder.AddHealthFeature();

		// Assert
		Assert.Same(builder, result);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddHealthFeature"/> registers the
	/// <see cref="HealthCheckService"/> so that the health check infrastructure is available.
	/// </summary>
	[Fact]
	public void AddHealthFeature_RegistersHealthCheckService()
	{
		// Arrange
		WebApplicationBuilder builder = WebApplication.CreateBuilder();
		builder.AddHealthFeature();

		using ServiceProvider provider = builder.Services.BuildServiceProvider();

		// Act
		HealthCheckService? service = provider.GetService<HealthCheckService>();

		// Assert
		Assert.NotNull(service);
	}
}
