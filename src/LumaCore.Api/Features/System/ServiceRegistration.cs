// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Configuration;

namespace LumaCore.Api.Features.System;

/// <summary>
/// Provides extension methods for registering the System feature with the application.
/// </summary>
/// <remarks>
///     <para>
///     The System feature provides diagnostic endpoints for monitoring and troubleshooting
///     LumaCore instances. It exposes runtime information and configuration values (with
///     secrets automatically masked).
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
	/// This method registers <see cref="OptionsRegistry"/> which uses the <see cref="OptionsTracker"/> to enumerate
	/// all registered Options types. The registry is instantiated lazily on first use, at which point all Options
	/// have already been registered and the tracker finalized.
	/// </remarks>
	public static WebApplicationBuilder AddSystemFeature(this WebApplicationBuilder builder)
	{
		// Register OptionsRegistry as singleton.
		// It gets OptionsTracker (already in DI from AddFeatureOptions calls) and IServiceProvider injected.
		builder.Services.AddSingleton<OptionsRegistry>();

		return builder;
	}
}
