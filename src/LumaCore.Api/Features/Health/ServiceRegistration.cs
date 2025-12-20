// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Health;

/// <summary>
/// Provides extension methods for registering health check services with the dependency injection container.
/// </summary>
/// <remarks>
///     <para>
///     This class is part of the Health feature and configures the ASP.NET Core
///     health check infrastructure for liveness and readiness probes.
///     </para>
/// </remarks>
static class ServiceRegistration
{
	/// <summary>
	/// Registers the health feature with the given <see cref="WebApplicationBuilder"/>.
	/// </summary>
	/// <param name="builder">
	/// The <see cref="WebApplicationBuilder"/> used to configure services and application
	/// behavior during startup.
	/// </param>
	/// <returns>
	/// The same <see cref="WebApplicationBuilder"/> instance to enable a fluent
	/// configuration style.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method is the primary entry point for registering the health feature. It
	///     wires up the internal service registration by delegating to
	///     <see cref="AddHealthFeatureCore(IServiceCollection, IConfiguration)"/> and uses
	///     the builder's configuration as the source of health-related settings.
	///     </para>
	///     <para>
	///     The method is intended to be called once during application startup, typically
	///     from the central service configuration in <c>Program.Services.cs</c>.
	///     </para>
	/// </remarks>
	public static WebApplicationBuilder AddHealthFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddHealthFeatureCore(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// Registers the core services required by the health feature.
	/// </summary>
	/// <param name="services">
	/// The <see cref="IServiceCollection"/> into which the health-related services will
	/// be registered.
	/// </param>
	/// <param name="configuration">
	/// The application configuration. It is currently not used directly but is accepted
	/// to keep the signature aligned with other feature registration methods and to
	/// allow future, configuration-driven health check settings without breaking
	/// existing callers.
	/// </param>
	/// <returns>
	/// The same <see cref="IServiceCollection"/> instance to enable fluent service
	/// registration.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This method registers the standard ASP.NET Core health check infrastructure
	///     using <see cref="HealthCheckServiceCollectionExtensions.AddHealthChecks(IServiceCollection)"/>.
	///     The resulting configuration enables a dedicated health endpoint (for example
	///     <c>/health</c>) that can be mapped in the application's request pipeline.
	///     </para>
	///     <para>
	///     At this stage, only the health check infrastructure itself is registered.
	///     Component-specific checks (database, storage, vector database, LLM backend, and
	///     similar subsystems) can be attached here in the future by extending the returned
	///     <see cref="IHealthChecksBuilder"/> instance.
	///     </para>
	/// </remarks>
	public static IServiceCollection AddHealthFeatureCore(this IServiceCollection services, IConfiguration configuration)
	{
		// Register the standard Microsoft health-check infrastructure.
		// This makes /health usable as an aggregated readiness endpoint once mapped
		// in the application pipeline.
		services.AddHealthChecks();

		return services;
	}
}
