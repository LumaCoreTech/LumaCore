// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Configuration;

using Microsoft.AspNetCore.HttpsPolicy;

namespace LumaCore.Api.Features.SecurityHeaders;

/// <summary>
/// Provides extension methods for registering the Security Headers feature services.
/// </summary>
/// <remarks>
/// Configures HTTP security headers based on the <see cref="SecurityHeadersOptions"/> configuration.
/// </remarks>
static class ServiceRegistration
{
	/// <summary>
	/// Registers the Security Headers feature services using the <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <returns>The web application builder for method chaining.</returns>
	/// <remarks>
	/// This is a convenience wrapper that forwards to <see cref="AddSecurityHeadersFeatureCore"/> using the
	/// <see cref="IServiceCollection"/> and <see cref="IConfiguration"/> exposed by the builder.
	/// </remarks>
	public static WebApplicationBuilder AddSecurityHeadersFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddSecurityHeadersFeatureCore(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// Registers the Security Headers feature services using the underlying <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register services with.</param>
	/// <param name="configuration">
	/// The application configuration used to bind <see cref="SecurityHeadersOptions"/>.
	/// </param>
	/// <returns>The original <see cref="IServiceCollection"/> for fluent chaining.</returns>
	/// <remarks>
	///     <para>
	///     This registers and validates the <see cref="SecurityHeadersOptions"/> configuration from the
	///     <c>SecurityHeaders</c> section in appsettings.json.
	///     </para>
	///     <para>HSTS options are also configured based on the <see cref="SecurityHeadersOptions"/> settings.</para>
	/// </remarks>
	public static IServiceCollection AddSecurityHeadersFeatureCore(
		this IServiceCollection services,
		IConfiguration          configuration)
	{
		// Bind configuration to get values for HSTS setup.
		IConfigurationSection section = configuration.GetSection(SecurityHeadersOptions.SectionName);

		// Register and validate SecurityHeadersOptions.
		services.AddFeatureOptions<SecurityHeadersOptions>(configuration, SecurityHeadersOptions.SectionName);

		// Configure HSTS options based on SecurityHeadersOptions.
		SecurityHeadersOptions options = section.Get<SecurityHeadersOptions>() ?? new SecurityHeadersOptions();

		services.Configure<HstsOptions>(hstsOptions =>
		{
			hstsOptions.MaxAge = TimeSpan.FromSeconds(options.HstsMaxAgeSeconds);
			hstsOptions.IncludeSubDomains = options.HstsIncludeSubDomains;
		});

		return services;
	}
}
