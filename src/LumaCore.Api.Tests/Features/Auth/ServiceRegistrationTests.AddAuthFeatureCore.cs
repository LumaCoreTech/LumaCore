// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text;

using LumaCore.Api.Features.Auth;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

// AddAuthFeatureCore(): core registration of the JWT authentication stack.
//
// These tests verify the service registrations and options configuration performed by
// AddAuthFeatureCore(). The tests progress from basic plumbing to security-critical
// configuration:
//
//   1. Method chaining: AddAuthFeatureCore() returns the original IServiceCollection.
//
//   2. Options binding: JwtOptions, AuthCookieOptions, and TokenRevocationOptions are
//      bound from their respective configuration sections.
//
//   3. Authentication scheme: the default authenticate scheme is set to "Bearer".
//
//   4. Token validation parameters: issuer, audience, signing key, lifetime validation,
//      and clock skew are derived from JwtOptions.
//
//   5. Service registrations: IJwtTokenFactory (singleton),
//      ITokenRevocationService (scoped), IMemoryCache (singleton), and
//      IAuthorizationService are wired into the container.
public sealed partial class ServiceRegistrationTests
{
	// --- 1. Method chaining ---

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> returns the original
	/// <see cref="IServiceCollection"/> for method chaining.
	/// </summary>
	[Fact]
	public void AddAuthFeatureCore_WhenCalled_ReturnsOriginalServiceCollection()
	{
		// Arrange
		IConfiguration config = new ConfigurationBuilder().Build();
		var services = new ServiceCollection();

		// Act
		IServiceCollection result = services.AddAuthFeatureCore(config);

		// Assert
		Assert.Same(services, result);
	}

	// --- 2. Options binding ---

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> binds <see cref="JwtOptions"/> from the
	/// <c>Jwt</c> configuration section.
	/// </summary>
	[Fact]
	public void AddAuthFeatureCore_WhenCalled_BindsJwtOptionsFromConfiguration()
	{
		// Arrange
		Dictionary<string, string?> config = CreateValidJwtConfiguration();
		WebApplicationBuilder builder = CreateTestBuilder();
		builder.Configuration.AddInMemoryCollection(config);
		builder.Services.AddAuthFeatureCore(builder.Configuration);
		WebApplication app = builder.Build();

		// Act
		JwtOptions options = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

		// Assert
		Assert.Equal(config["Jwt:Issuer"], options.Issuer);
		Assert.Equal(config["Jwt:Audience"], options.Audience);
		Assert.Equal(config["Jwt:SigningKey"], options.SigningKey);
		Assert.Equal(30, options.AccessTokenLifetimeMinutes);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> binds <see cref="AuthCookieOptions"/>
	/// from the <c>Jwt:Cookie</c> configuration section.
	/// </summary>
	[Fact]
	public void AddAuthFeatureCore_WhenCalled_BindsAuthCookieOptionsFromConfiguration()
	{
		// Arrange
		Dictionary<string, string?> config = CreateValidJwtConfiguration();
		WebApplicationBuilder builder = CreateTestBuilder();
		builder.Configuration.AddInMemoryCollection(config);
		builder.Services.AddAuthFeatureCore(builder.Configuration);
		WebApplication app = builder.Build();

		// Act
		AuthCookieOptions options = app.Services
			.GetRequiredService<IOptions<AuthCookieOptions>>()
			.Value;

		// Assert
		Assert.Equal(config["Jwt:Cookie:Name"], options.Name);
		Assert.Equal(config["Jwt:Cookie:Path"], options.Path);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> binds
	/// <see cref="TokenRevocationOptions"/> from the <c>Jwt:TokenRevocation</c> configuration section.
	/// </summary>
	[Fact]
	public void AddAuthFeatureCore_WhenCalled_BindsTokenRevocationOptionsFromConfiguration()
	{
		// Arrange — CacheDurationSeconds is set to 15 (default is 5) to prove binding actually works.
		Dictionary<string, string?> config = CreateValidJwtConfiguration();
		WebApplicationBuilder builder = CreateTestBuilder();
		builder.Configuration.AddInMemoryCollection(config);
		builder.Services.AddAuthFeatureCore(builder.Configuration);
		WebApplication app = builder.Build();

		// Act
		TokenRevocationOptions options = app.Services
			.GetRequiredService<IOptions<TokenRevocationOptions>>()
			.Value;

		// Assert
		Assert.Equal(15, options.CacheDurationSeconds);
	}

	// --- 3. Authentication scheme ---

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> sets the default authentication scheme
	/// to <see cref="JwtBearerDefaults.AuthenticationScheme"/> (<c>"Bearer"</c>).
	/// </summary>
	/// <remarks>
	/// <see cref="IAuthenticationSchemeProvider"/> is not registered by our code — it is an internal infrastructure
	/// service registered implicitly by <c>AddAuthentication()</c>. It is used here purely as a verification
	/// mechanism to inspect the configured default scheme.
	/// </remarks>
	[Fact]
	public async Task AddAuthFeatureCore_WhenCalled_SetsDefaultAuthenticationSchemeToBearer()
	{
		// Arrange
		WebApplicationBuilder builder = CreateTestBuilder();
		builder.Services.AddAuthFeatureCore(builder.Configuration);
		WebApplication app = builder.Build();

		// Act — IAuthenticationSchemeProvider is implicitly registered by AddAuthentication();
		// we resolve it here only to verify the default scheme configuration.
		var schemeProvider = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();
		AuthenticationScheme? scheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();

		// Assert
		Assert.NotNull(scheme);
		Assert.Equal(JwtBearerDefaults.AuthenticationScheme, scheme.Name);
	}

	// --- 4. Token validation parameters ---

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> configures
	/// <see cref="TokenValidationParameters"/> from <see cref="JwtOptions"/>: issuer, audience, signing key,
	/// lifetime validation, and clock skew.
	/// </summary>
	[Fact]
	public void AddAuthFeatureCore_WhenCalled_ConfiguresTokenValidationFromJwtOptions()
	{
		// Arrange
		Dictionary<string, string?> config = CreateValidJwtConfiguration();
		WebApplicationBuilder builder = CreateTestBuilder();
		builder.Configuration.AddInMemoryCollection(config);
		builder.Services.AddAuthFeatureCore(builder.Configuration);
		WebApplication app = builder.Build();

		// Act — IOptionsMonitor is required here because JwtBearerOptions is a named options instance
		// (name = "Bearer"). IOptions<T>.Value would return the unnamed default with empty TVP.
		JwtBearerOptions bearerOptions = app.Services
			.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
			.Get(JwtBearerDefaults.AuthenticationScheme);
		TokenValidationParameters tvp = bearerOptions.TokenValidationParameters;

		// Assert — issuer
		Assert.True(tvp.ValidateIssuer);
		Assert.Equal(config["Jwt:Issuer"], tvp.ValidIssuer);

		// Assert — audience
		Assert.True(tvp.ValidateAudience);
		Assert.Equal(config["Jwt:Audience"], tvp.ValidAudience);

		// Assert — signing key
		Assert.True(tvp.ValidateIssuerSigningKey);
		var symmetricKey = Assert.IsType<SymmetricSecurityKey>(tvp.IssuerSigningKey);
		Assert.Equal(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!), symmetricKey.Key);

		// Assert — lifetime and clock skew
		Assert.True(tvp.ValidateLifetime);
		Assert.Equal(TimeSpan.FromSeconds(30), tvp.ClockSkew);
	}

	// --- 5. Service registrations ---

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> registers
	/// <see cref="IJwtTokenFactory"/> with <see cref="ServiceLifetime.Singleton"/> lifetime and
	/// <see cref="JwtTokenFactory"/> as the implementation type.
	/// </summary>
	[Fact]
	public void AddAuthFeatureCore_WhenCalled_RegistersJwtTokenFactoryAsSingleton()
	{
		// Arrange
		IConfiguration config = new ConfigurationBuilder().Build();
		var services = new ServiceCollection();

		// Act
		services.AddAuthFeatureCore(config);

		// Assert
		ServiceDescriptor? descriptor =
			services.FirstOrDefault(sd => sd.ServiceType == typeof(IJwtTokenFactory));
		Assert.NotNull(descriptor);
		Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
		Assert.Equal(typeof(JwtTokenFactory), descriptor.ImplementationType);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> registers
	/// <see cref="ITokenRevocationService"/> with <see cref="ServiceLifetime.Scoped"/> lifetime and
	/// <see cref="TokenRevocationService"/> as the implementation type.
	/// </summary>
	[Fact]
	public void AddAuthFeatureCore_WhenCalled_RegistersTokenRevocationServiceAsScoped()
	{
		// Arrange
		IConfiguration config = new ConfigurationBuilder().Build();
		var services = new ServiceCollection();

		// Act
		services.AddAuthFeatureCore(config);

		// Assert
		ServiceDescriptor? descriptor =
			services.FirstOrDefault(sd => sd.ServiceType == typeof(ITokenRevocationService));
		Assert.NotNull(descriptor);
		Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
		Assert.Equal(typeof(TokenRevocationService), descriptor.ImplementationType);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> registers <see cref="IMemoryCache"/>
	/// with <see cref="ServiceLifetime.Singleton"/> lifetime and <see cref="MemoryCache"/> as the implementation
	/// type. The revocation cache relies on a shared in-memory cache to store negative lookups across requests.
	/// </summary>
	[Fact]
	public void AddAuthFeatureCore_WhenCalled_RegistersMemoryCacheAsSingleton()
	{
		// Arrange
		IConfiguration config = new ConfigurationBuilder().Build();
		var services = new ServiceCollection();

		// Act
		services.AddAuthFeatureCore(config);

		// Assert
		ServiceDescriptor? descriptor =
			services.FirstOrDefault(sd => sd.ServiceType == typeof(IMemoryCache));
		Assert.NotNull(descriptor);
		Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
		Assert.Equal(typeof(MemoryCache), descriptor.ImplementationType);
	}

	/// <summary>
	/// Verifies that <see cref="ServiceRegistration.AddAuthFeatureCore"/> registers authorization services
	/// (<see cref="IAuthorizationService"/>) via <c>AddAuthorization()</c>.
	/// </summary>
	[Fact]
	public void AddAuthFeatureCore_WhenCalled_RegistersAuthorizationServices()
	{
		// Arrange
		IConfiguration config = new ConfigurationBuilder().Build();
		var services = new ServiceCollection();

		// Act
		services.AddAuthFeatureCore(config);

		// Assert — only presence check (no Lifetime/ImplementationType) because AddAuthorization()
		// is a framework method that registers IAuthorizationService via internal factories whose
		// shape may change between .NET versions.
		Assert.Contains(services, sd => sd.ServiceType == typeof(IAuthorizationService));
	}
}
