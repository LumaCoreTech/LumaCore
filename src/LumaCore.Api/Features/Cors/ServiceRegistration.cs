// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Cors;

/// <summary>
/// Provides extension methods for registering the CORS feature services.
/// </summary>
/// <remarks>
///     <para>
///     This class is part of the CORS feature and configures Cross-Origin Resource Sharing
///     based on the <see cref="CorsOptions"/> configuration.
///     </para>
/// </remarks>
public static class ServiceRegistration
{
	/// <summary>
	/// Registers the CORS feature services using the <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <returns>The web application builder for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This is a convenience wrapper that forwards to <see cref="AddCorsFeatureCore"/>
	///     using the <see cref="IServiceCollection"/> and <see cref="IConfiguration"/>
	///     exposed by the builder.
	///     </para>
	/// </remarks>
	public static WebApplicationBuilder AddCorsFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddCorsFeatureCore(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// Registers the CORS feature services using the underlying <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register services with.</param>
	/// <param name="configuration">The application configuration used to bind <see cref="CorsOptions"/>.</param>
	/// <returns>The original <see cref="IServiceCollection"/> for fluent chaining.</returns>
	/// <remarks>
	///     <para>
	///     This registers and validates the <see cref="CorsOptions"/> configuration from
	///     the <c>Cors</c> section in appsettings.json.
	///     </para>
	///     <para>
	///     Validation includes:
	///     </para>
	///     <list type="bullet">
	///         <item>Ensuring AllowedOrigins is not empty when CORS is enabled</item>
	///         <item>Preventing AllowCredentials=true with AllowedOrigins=["*"] (security violation)</item>
	///         <item>Validating PreflightMaxAge is non-negative when specified</item>
	///     </list>
	///     <para>
	///     Validation occurs at startup (<see cref="OptionsBuilderExtensions.ValidateOnStart{TOptions}"/>),
	///     so invalid configuration will cause the application to fail fast with a clear error message.
	///     </para>
	/// </remarks>
	public static IServiceCollection AddCorsFeatureCore(
		this IServiceCollection services,
		IConfiguration          configuration)
	{
		// Register and validate CorsOptions.
		services
			.AddOptions<CorsOptions>()
			.Bind(configuration.GetSection(CorsOptions.SectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Add CORS services to the DI container.
		// The actual policy is configured in UseCorsFeature() based on CorsOptions.
		services.AddCors();

		return services;
	}
}
