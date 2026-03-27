// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.ErrorHandling;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace LumaCore.Api.Tests.Features.ErrorHandling;

/// <summary>
/// Tests for <see cref="ServiceRegistration"/> in the ErrorHandling feature.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify that <see cref="ServiceRegistration.AddErrorHandlingFeature"/> correctly registers
///     <see cref="LumaCoreExceptionHandler"/> as an <see cref="IExceptionHandler"/> implementation. The actual
///     exception handling behavior is tested in <see cref="LumaCoreExceptionHandlerTests"/>; status code
///     mapping is tested in <see cref="StatusCodeMappingIntegrationTests"/>.
///     </para>
///     <para>
///     Tests that resolve services use a <see cref="WebApplicationBuilder"/> with
///     <see cref="Environments.Production"/> to avoid <c>ValidateOnBuild</c> / <c>ValidateScopes</c> issues
///     that some test runners trigger by injecting <c>ASPNETCORE_ENVIRONMENT=Development</c>.
///     </para>
/// </remarks>
[Trait("Category", "ErrorHandling")]
public sealed class ServiceRegistrationTests
{
	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddErrorHandlingFeature"/> returns the original
	/// <see cref="WebApplicationBuilder"/> for method chaining.
	/// </summary>
	[Fact]
	public void AddErrorHandlingFeature_WhenCalled_ReturnsOriginalBuilder()
	{
		// Arrange
		WebApplicationBuilder builder = WebApplication.CreateBuilder();

		// Act
		WebApplicationBuilder result = builder.AddErrorHandlingFeature();

		// Assert
		Assert.Same(builder, result);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddErrorHandlingFeature"/> registers
	/// <see cref="LumaCoreExceptionHandler"/> as an <see cref="IExceptionHandler"/> so that the
	/// exception handler middleware can invoke it.
	/// </summary>
	[Fact]
	public void AddErrorHandlingFeature_WhenCalled_RegistersExceptionHandler()
	{
		// Arrange
		WebApplicationBuilder builder = CreateTestBuilder();
		builder.AddErrorHandlingFeature();
		WebApplication app = builder.Build();

		// Act
		IEnumerable<IExceptionHandler> handlers = app.Services.GetServices<IExceptionHandler>();

		// Assert
		IExceptionHandler handler = Assert.Single(handlers);
		Assert.IsType<LumaCoreExceptionHandler>(handler);
	}

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
