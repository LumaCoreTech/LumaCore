// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Runtime.CompilerServices;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LumaCore.Configuration;

/// <summary>
/// Provides extension methods for registering feature Options with section tracking.
/// </summary>
/// <remarks>
///     <para>
///     This class provides the standard way to register Options classes in LumaCore features.
///     Using <see cref="AddFeatureOptions{TOptions}"/> ensures that the configuration section
///     name is tracked and available for diagnostic endpoints.
///     </para>
///     <para>
///     At application startup, <see cref="ValidateOptionsRegistrations"/> verifies that all
///     Options classes were registered through this extension, preventing silent misconfiguration.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// // In a feature's ServiceRegistration.cs:
/// public static WebApplicationBuilder AddAuthFeature(this WebApplicationBuilder builder)
/// {
///     builder.Services.AddFeatureOptions&lt;JwtOptions&gt;(
///         builder.Configuration,
///         JwtOptions.SectionName);
///     
///     return builder;
/// }
/// </code>
/// </example>
public static class OptionsRegistrationExtensions
{
	/// <summary>
	/// Associates each <see cref="IServiceCollection"/> with its own <see cref="OptionsTracker"/> instance.
	/// Uses weak references so the tracker is garbage collected along with the service collection.
	/// </summary>
	private static readonly ConditionalWeakTable<IServiceCollection, OptionsTracker> sTrackers = new();

	/// <summary>
	/// Registers an Options class with configuration binding and section tracking.
	/// </summary>
	/// <typeparam name="TOptions">The Options type to register.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration root.</param>
	/// <param name="sectionName">
	/// The configuration section name (e.g., <c>"Jwt"</c>).
	/// Should match the section in <c>appsettings.json</c>.
	/// </param>
	/// <returns>
	/// An <see cref="OptionsBuilder{TOptions}"/> for further configuration (e.g., adding validators).
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// Called after <see cref="ValidateOptionsRegistrations"/> has been invoked.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method performs the following:
	///     <list type="bullet">
	///         <item>Binds the Options type to the specified configuration section</item>
	///         <item>Enables DataAnnotations validation</item>
	///         <item>Enables validation on startup (fail-fast)</item>
	///         <item>Tracks the registration for later verification</item>
	///         <item>Records the section name for diagnostic endpoints</item>
	///     </list>
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// builder.Services.AddFeatureOptions&lt;JwtOptions&gt;(
	///     builder.Configuration,
	///     JwtOptions.SectionName);
	/// </code>
	/// </example>
	public static OptionsBuilder<TOptions> AddFeatureOptions<TOptions>(
		this IServiceCollection services,
		IConfiguration          configuration,
		string                  sectionName)
		where TOptions : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		// Track this Options type and its section name.
		OptionsTracker tracker = GetOrCreateTracker(services);
		tracker.Track<TOptions>(sectionName);

		return services
			.AddOptions<TOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
	}

	/// <summary>
	/// Validates that all LumaCore Options types were registered via <see cref="AddFeatureOptions{TOptions}"/>.
	/// </summary>
	/// <param name="services">The service collection to validate.</param>
	/// <exception cref="InvalidOperationException">
	/// Any Options type was registered without using <see cref="AddFeatureOptions{TOptions}"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method should be called at the end of service registration, before <c>builder.Build()</c>.
	///     It ensures that all Options classes follow the standard registration pattern, preventing
	///     silent misconfiguration where section names might not match.
	///     </para>
	///     <para>
	///     After validation, the section name mappings are frozen for efficient runtime access.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// // At the end of Program.cs service registration:
	/// builder.Services.ValidateOptionsRegistrations();
	/// var app = builder.Build();
	/// </code>
	/// </example>
	public static void ValidateOptionsRegistrations(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		OptionsTracker tracker = GetOrCreateTracker(services);
		tracker.Validate(services);
	}

	/// <summary>
	/// Gets or creates an <see cref="OptionsTracker"/> for the specified service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The <see cref="OptionsTracker"/> associated with this service collection.</returns>
	private static OptionsTracker GetOrCreateTracker(IServiceCollection services)
	{
		// GetValue() is atomic — the factory delegate executes exactly once per key,
		// even if multiple threads call this method concurrently.
		return sTrackers.GetValue(
			services,
			static svc =>
			{
				var tracker = new OptionsTracker();
				svc.AddSingleton(tracker);
				return tracker;
			});
	}
}
