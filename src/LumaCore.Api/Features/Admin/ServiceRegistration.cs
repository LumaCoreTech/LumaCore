// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Admin;

/// <summary>
/// Provides extension methods for registering administrative services with the dependency injection container.
/// </summary>
/// <remarks>
///     <para>
///     This class is part of the Admin feature and currently serves as a placeholder
///     for future administrative service registrations while maintaining consistency
///     with the feature pattern used throughout LumaCore.
///     </para>
/// </remarks>
public static class ServiceRegistration
{
	/// <summary>
	/// Registers options binding and supporting services using the <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is a convenience wrapper that forwards to <see cref="AddAdminFeatureCore"/>
	///     using <see cref="WebApplicationBuilder.Services"/> and <see cref="WebApplicationBuilder.Configuration"/>.
	///     It exists to keep the <c>Program</c>-level configuration consistent with other feature registration methods.
	///     </para>
	/// </remarks>
	/// <param name="builder">The application builder.</param>
	/// <returns>The modified application builder.</returns>
	public static WebApplicationBuilder AddAdminFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddAdminFeatureCore(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// Registers options binding and supporting services using the underlying <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register admin services with.</param>
	/// <param name="configuration">The application configuration used to bind options.</param>
	/// <returns>The original <see cref="IServiceCollection"/> for fluent chaining.</returns>
	public static IServiceCollection AddAdminFeatureCore(
		this IServiceCollection services,
		IConfiguration          configuration)
	{
		// Currently, the admin feature does not have any options or services to register.
		// This method exists to keep the registration pattern consistent and to allow
		// for easy extension in the future.
		return services;
	}
}
