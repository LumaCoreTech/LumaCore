// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace LumaCore.Api.Tests.Features.Auth;

public sealed partial class ServiceRegistrationTests
{
	/// <summary>
	/// Creates a <see cref="WebApplicationBuilder"/> with DI service validation disabled.
	/// </summary>
	/// <returns>A builder suitable for tests that call <see cref="WebApplicationBuilder.Build"/>.</returns>
	/// <remarks>
	/// <see cref="WebApplicationBuilder"/> enables <c>ValidateOnBuild</c> and <c>ValidateScopes</c> when the
	/// environment is <c>Development</c>. Some test runners (notably ReSharper) inject
	/// <c>ASPNETCORE_ENVIRONMENT=Development</c> into the process environment, which can override a post-creation
	/// <c>builder.Environment.EnvironmentName</c> assignment. By passing <see cref="Environments.Production"/>
	/// via <see cref="WebApplicationOptions"/>, the environment is locked in <b>before</b> the builder reads any
	/// environment variables, making the behavior deterministic across all test runners.
	/// </remarks>
	private static WebApplicationBuilder CreateTestBuilder() => WebApplication.CreateBuilder(
		new WebApplicationOptions
		{
			EnvironmentName = Environments.Production
		});

	/// <summary>
	/// Creates a configuration dictionary containing valid JWT settings that pass
	/// <see cref="JwtOptions"/> validation. Includes required values for all three
	/// options classes bound by the authentication feature: <c>Jwt</c>, <c>Jwt:Cookie</c>, and
	/// <c>Jwt:TokenRevocation</c>.
	/// </summary>
	/// <returns>
	/// A dictionary suitable for <c>MemoryConfigurationBuilderExtensions.AddInMemoryCollection()</c>.
	/// </returns>
	private static Dictionary<string, string?> CreateValidJwtConfiguration() => new()
	{
		["Jwt:Issuer"] = "test-issuer",
		["Jwt:Audience"] = "test-audience",
		["Jwt:SigningKey"] = "a-test-signing-key-that-is-at-least-32-characters-long!!!",
		["Jwt:AccessTokenLifetimeMinutes"] = "30",
		["Jwt:Cookie:Name"] = "test-cookie",
		["Jwt:Cookie:Path"] = "/api",
		["Jwt:TokenRevocation:CacheDurationSeconds"] = "15"
	};
}
