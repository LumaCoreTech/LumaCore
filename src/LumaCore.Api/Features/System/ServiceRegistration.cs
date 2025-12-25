// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Configuration;
using LumaCore.Core.Diagnostics;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LumaCore.Api.Features.System;

/// <summary>
/// Provides extension methods for registering the System feature with the application.
/// </summary>
/// <remarks>
///     <para>
///     The System feature provides diagnostic endpoints for monitoring and troubleshooting
///     LumaCore instances. It exposes runtime information, metrics, and configuration values
///     (with secrets automatically masked).
///     </para>
///     <para>
///     This feature uses the <see cref="OptionsTracker"/> populated by
///     <see cref="OptionsRegistrationExtensions.AddFeatureOptions{TOptions}"/> calls.
///     Properties marked with <see cref="SecretAttribute"/> are automatically masked in the output.
///     </para>
/// </remarks>
static class ServiceRegistration
{
	/// <summary>
	/// Registers the System feature services with the application.
	/// </summary>
	/// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
	/// <returns>The <paramref name="builder"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method registers <see cref="OptionsRegistry"/> for configuration inspection and
	///     <see cref="MetricsAggregator"/> for metrics collection.
	///     </para>
	///     <para>
	///     Core metrics (GC, memory, process, thread pool) are collected directly by the
	///     <see cref="MetricsAggregator"/> via <see cref="SystemMetricsFactory"/>. Features can register
	///     additional metrics contributors using
	///     <see cref="Infrastructure.MetricsRegistrationExtensions.AddMetricsContributor{TContributor}"/>.
	///     </para>
	/// </remarks>
	public static WebApplicationBuilder AddSystemFeature(this WebApplicationBuilder builder)
	{
		// Register OptionsRegistry for configuration inspection endpoint.
		builder.Services.AddSingleton<OptionsRegistry>();

		// Register MetricsContributorRegistry for feature metrics extensions.
		// Use instance registration for consistency with AddMetricsContributor's GetOrCreateRegistry pattern.
		builder.Services.TryAddSingleton(new MetricsContributorRegistry());

		// Register MetricsAggregator for metrics endpoint.
		// Core metrics are collected directly via factories; only feature contributors use the registry.
		builder.Services.AddSingleton<MetricsAggregator>();

		return builder;
	}
}
