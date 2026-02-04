// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.BackgroundProcessing;

/// <summary>
/// Extension methods for registering <see cref="WorkQueueProcessor"/> with dependency injection.
/// </summary>
public static class WorkQueueProcessorServiceCollectionExtensions
{
	/// <summary>
	/// Registers the <see cref="WorkQueueProcessor"/> as a singleton with default options.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
	/// <remarks>
	///     <para>
	///     This registers:
	///     <list type="bullet">
	///         <item><see cref="WorkQueueProcessor"/> as a singleton</item>
	///         <item><see cref="WorkQueueProcessorHostedService"/> to manage the processor lifecycle</item>
	///     </list>
	///     </para>
	///     <para>
	///     The processor is automatically initialized when the host starts and shut down when the host stops.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// services.AddWorkQueueProcessor();
	/// </code>
	/// </example>
	public static IServiceCollection AddWorkQueueProcessor(this IServiceCollection services)
	{
		return services.AddWorkQueueProcessor(_ => { });
	}

	/// <summary>
	/// Registers the <see cref="WorkQueueProcessor"/> as a singleton with the specified options.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="configure">A delegate to configure the <see cref="WorkQueueProcessorOptions"/>.</param>
	/// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This registers:
	///     <list type="bullet">
	///         <item><see cref="WorkQueueProcessor"/> as a singleton</item>
	///         <item><see cref="WorkQueueProcessorHostedService"/> to manage the processor lifecycle</item>
	///     </list>
	///     </para>
	///     <para>
	///     The processor is automatically initialized when the host starts and shut down when the host stops.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// services.AddWorkQueueProcessor(options =>
	/// {
	///     options.MaxQueueSize = 5000;
	///     options.MaxConcurrency = 4;
	///     options.ShutdownTimeout = TimeSpan.FromMinutes(1);
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddWorkQueueProcessor(
		this IServiceCollection           services,
		Action<WorkQueueProcessorOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		services
			.AddOptions<WorkQueueProcessorOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services.AddWorkQueueProcessorCore();
	}

	/// <summary>
	/// Registers the <see cref="WorkQueueProcessor"/> as a singleton with options from configuration.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="configuration">The configuration instance to read <see cref="WorkQueueProcessorOptions"/> from.</param>
	/// <param name="sectionName">
	/// The name of the configuration section to bind to <see cref="WorkQueueProcessorOptions"/>.
	/// Defaults to <see cref="WorkQueueProcessorOptions.DefaultSectionName"/> ("WorkQueue").
	/// </param>
	/// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configuration"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This registers:
	///     <list type="bullet">
	///         <item><see cref="WorkQueueProcessor"/> as a singleton</item>
	///         <item><see cref="WorkQueueProcessorHostedService"/> to manage the processor lifecycle</item>
	///     </list>
	///     </para>
	///     <para>
	///     The processor is automatically initialized when the host starts and shut down when the host stops.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// // In appsettings.json:
	/// // {
	/// //   "WorkQueue": {
	/// //     "MaxQueueSize": 5000,
	/// //     "MaxConcurrency": 4,
	/// //     "ShutdownTimeout": "00:01:00"
	/// //   }
	/// // }
	/// 
	/// // Use default section name "WorkQueue":
	/// services.AddWorkQueueProcessor(configuration);
	/// 
	/// // Or specify a custom section name:
	/// services.AddWorkQueueProcessor(configuration, "CustomWorkQueue");
	/// </code>
	/// </example>
	public static IServiceCollection AddWorkQueueProcessor(
		this IServiceCollection services,
		IConfiguration          configuration,
		string                  sectionName = WorkQueueProcessorOptions.DefaultSectionName)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		// Use AddFeatureOptions for DataAnnotations validation, ValidateOnStart, and tracking.
		services.AddFeatureOptions<WorkQueueProcessorOptions>(configuration, sectionName);

		return services.AddWorkQueueProcessorCore();
	}

	/// <summary>
	/// Core registration logic shared by all overloads.
	/// </summary>
	private static IServiceCollection AddWorkQueueProcessorCore(this IServiceCollection services)
	{
		// Register the WorkQueueProcessor singleton.
		services.AddSingleton<WorkQueueProcessor>(sp =>
		{
			WorkQueueProcessorOptions options = sp.GetRequiredService<IOptions<WorkQueueProcessorOptions>>().Value;
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

			return new WorkQueueProcessor(
				loggerFactory,
				options.MaxQueueSize,
				options.ShutdownTimeout,
				options.MaxConcurrency);
		});

		// Register interface pointing to the same singleton instance.
		services.AddSingleton<IWorkQueueProcessor>(sp => sp.GetRequiredService<WorkQueueProcessor>());

		// Register the hosted service to manage lifecycle.
		services.AddHostedService<WorkQueueProcessorHostedService>();

		return services;
	}
}
