// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

namespace LumaCore.Api.Infrastructure;

/// <summary>
/// Provides extension methods for registering <see cref="IMetricsContributor"/> implementations
/// with the application's dependency injection container.
/// </summary>
/// <remarks>
///     <para>
///     This class bridges the gap between the core <see cref="MetricsContributorRegistry"/> and
///     the ASP.NET Core DI container. Features can use these extensions to register their metrics
///     contributors without depending on other features.
///     </para>
/// </remarks>
public static class MetricsRegistrationExtensions
{
	/// <summary>
	/// Registers a metrics contributor with the specified section name.
	/// </summary>
	/// <typeparam name="TContributor">
	/// The type implementing <see cref="IMetricsContributor"/>.
	/// </typeparam>
	/// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
	/// <param name="sectionName">
	/// The unique section name for this contributor's metrics. Must be unique across all
	/// registered contributors (case-insensitive).
	/// </param>
	/// <returns>The <paramref name="builder"/> for method chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="sectionName"/> is <see langword="null"/>, empty, or whitespace.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown if <paramref name="sectionName"/> is already registered by another contributor.
	/// </exception>
	/// <remarks>
	///     <para>
	///     Validation happens immediately at registration time (fail-fast). If a section name
	///     conflict is detected, the application will fail to start with a clear error message.
	///     </para>
	///     <para>
	///     The contributor is registered as a singleton service and will be resolved when
	///     metrics are collected. In the response, metrics are sorted alphabetically by
	///     section name.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// // In your feature's ServiceRegistration:
	/// builder.AddMetricsContributor&lt;OllamaMetricsContributor&gt;("ollama");
	/// </code>
	/// </example>
	public static WebApplicationBuilder AddMetricsContributor<TContributor>(
		this WebApplicationBuilder builder,
		string                     sectionName)
		where TContributor : class, IMetricsContributor
	{
		// Get or create the registry (shared during registration).
		MetricsContributorRegistry registry = GetOrCreateRegistry(builder.Services);

		// Validate and register the section name (fail-fast).
		registry.Register<TContributor>(sectionName);

		// Register the contributor as a singleton service.
		builder.Services.AddSingleton<TContributor>();

		return builder;
	}

	/// <summary>
	/// Gets the existing <see cref="MetricsContributorRegistry"/> from the service collection,
	/// or creates and registers a new one if none exists.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The <see cref="MetricsContributorRegistry"/> instance.</returns>
	/// <remarks>
	///     <para>
	///     The registry is stored as a singleton in the service collection. This method ensures
	///     that the same registry instance is used across all <c>AddMetricsContributor</c> calls
	///     during application startup, enabling proper duplicate detection.
	///     </para>
	/// </remarks>
	private static MetricsContributorRegistry GetOrCreateRegistry(IServiceCollection services)
	{
		// Check if registry is already registered.
		ServiceDescriptor? existingDescriptor =
			services.FirstOrDefault(d => d.ServiceType == typeof(MetricsContributorRegistry));

		// Return existing registry if found.
		if (existingDescriptor?.ImplementationInstance is MetricsContributorRegistry existingRegistry)
			return existingRegistry;

		// Create new registry and register it.
		var registry = new MetricsContributorRegistry();
		services.AddSingleton(registry);

		return registry;
	}
}
