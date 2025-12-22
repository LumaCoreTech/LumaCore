// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Configuration;

namespace LumaCore.Api.Features.ProxyHeaders;

/// <summary>
/// Provides extension methods for registering the ProxyHeaders feature services.
/// </summary>
/// <remarks>
/// This class configures processing of forwarded headers from reverse proxies based on the
/// <see cref="ProxyHeadersOptions"/> configuration.
/// </remarks>
static class ServiceRegistration
{
	/// <summary>
	/// Registers the ProxyHeaders feature services using the <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <returns>The web application builder for method chaining.</returns>
	/// <remarks>
	/// This is a convenience wrapper that forwards to <see cref="AddProxyHeadersFeatureCore"/> using the
	/// <see cref="IServiceCollection"/> and <see cref="IConfiguration"/> exposed by the builder.
	/// </remarks>
	public static WebApplicationBuilder AddProxyHeadersFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddProxyHeadersFeatureCore(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// Registers the ProxyHeaders feature services using the underlying <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register services with.</param>
	/// <param name="configuration">
	/// The application configuration used to bind <see cref="ProxyHeadersOptions"/>.
	/// </param>
	/// <returns>The original <see cref="IServiceCollection"/> for fluent chaining.</returns>
	/// <remarks>
	///     <para>
	///     This registers and validates the <see cref="ProxyHeadersOptions"/> configuration from the
	///     <c>ProxyHeaders</c> section in appsettings.json.
	///     </para>
	///     <para>Validation includes:</para>
	///     <list type="bullet">
	///         <item>Ensuring SelfManaged mode has at least one trusted proxy or network configured</item>
	///         <item>Validating that TrustedProxies contain valid IP addresses</item>
	///         <item>Validating that TrustedNetworks use valid CIDR notation</item>
	///     </list>
	///     <para>
	///     Validation occurs at startup (<c>ValidateOnStart()</c>), so invalid
	///     configuration will cause the application to fail fast with a clear error message.
	///     </para>
	/// </remarks>
	public static IServiceCollection AddProxyHeadersFeatureCore(
		this IServiceCollection services,
		IConfiguration          configuration)
	{
		services.AddFeatureOptions<ProxyHeadersOptions>(
			configuration,
			ProxyHeadersOptions.SectionName);

		return services;
	}
}
