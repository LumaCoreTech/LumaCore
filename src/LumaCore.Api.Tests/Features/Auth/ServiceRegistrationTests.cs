// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;

using Microsoft.AspNetCore.Builder;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="ServiceRegistration"/> in the authentication feature.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify that the authentication <see cref="ServiceRegistration"/> correctly registers and configures
///     the complete JWT authentication stack: bearer authentication scheme, token validation parameters derived from
///     <see cref="JwtOptions"/>, and the required service registrations.
///     </para>
///     <para>
///         <b>Reading order:</b>
///     </para>
///     <list type="number">
///         <item>
///         <see cref="AddAuthFeature_WhenCalled_ReturnsOriginalBuilder"/> — convenience wrapper returning the builder
///         for chaining.
///         </item>
///         <item>
///         AddAuthFeatureCore — core registration logic (method chaining, options binding, authentication scheme,
///         token validation parameters, service registrations).
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Auth")]
public sealed partial class ServiceRegistrationTests
{
	#region AddAuthFeature()

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeature"/> returns the original
	/// <see cref="WebApplicationBuilder"/> for method chaining.
	/// </summary>
	[Fact]
	public void AddAuthFeature_WhenCalled_ReturnsOriginalBuilder()
	{
		// Arrange
		WebApplicationBuilder builder = WebApplication.CreateBuilder();

		// Act
		WebApplicationBuilder result = builder.AddAuthFeature();

		// Assert
		Assert.Same(builder, result);
	}

	#endregion
}
