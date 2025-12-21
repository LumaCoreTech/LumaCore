// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.HttpsRedirection;

/// <summary>
/// Provides extension methods for registering HTTPS redirection services with the dependency injection container.
/// </summary>
/// <remarks>
/// This class configures HTTPS redirection based on the <see cref="HttpsRedirectionOptions"/> configuration.
/// </remarks>
static class ServiceRegistration
{
	/// <summary>
	/// Registers HTTPS redirection services using the <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <returns>The modified application builder for fluent chaining.</returns>
	/// <remarks>
	/// This is a convenience wrapper that forwards to <see cref="AddHttpsRedirectionFeatureCore"/> using the
	/// <see cref="IServiceCollection"/> and <see cref="IConfiguration"/> exposed by the builder.
	/// </remarks>
	public static WebApplicationBuilder AddHttpsRedirectionFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddHttpsRedirectionFeatureCore(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// Registers HTTPS redirection services using the underlying <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register services with.</param>
	/// <param name="configuration">
	/// The application configuration used to bind <see cref="HttpsRedirectionOptions"/>.
	/// </param>
	/// <returns>The original <see cref="IServiceCollection"/> for fluent chaining.</returns>
	/// <remarks>
	/// This method binds <see cref="HttpsRedirectionOptions"/> from the <c>HttpsRedirection</c> configuration
	/// section and configures the ASP.NET Core HTTPS redirection middleware.
	/// </remarks>
	public static IServiceCollection AddHttpsRedirectionFeatureCore(
		this IServiceCollection services,
		IConfiguration          configuration)
	{
		// Bind and validate options at startup so misconfiguration fails fast.
		services
			.AddOptions<HttpsRedirectionOptions>()
			.Bind(configuration.GetSection(HttpsRedirectionOptions.SectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Configure HTTPS redirection settings, including the target HTTPS port
		// if specified in configuration.
		services.AddHttpsRedirection(options =>
		{
			int? httpsPort = configuration
				.GetSection(HttpsRedirectionOptions.SectionName)
				.GetValue<int?>(nameof(HttpsRedirectionOptions.HttpsPort));

			if (httpsPort is not null)
			{
				options.HttpsPort = httpsPort.Value;
			}
		});

		return services;
	}
}
