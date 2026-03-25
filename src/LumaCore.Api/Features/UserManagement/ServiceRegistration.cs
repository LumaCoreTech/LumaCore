// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

namespace LumaCore.Api.Features.UserManagement;

/// <summary>
/// Provides extension methods for registering user management services with the dependency injection container.
/// </summary>
static class ServiceRegistration
{
	/// <summary>
	/// Registers user management services using the <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <returns>The modified application builder.</returns>
	/// <remarks>
	/// This is a convenience wrapper that forwards to <see cref="AddUserManagementFeatureCore"/>.
	/// </remarks>
	public static WebApplicationBuilder AddUserManagementFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddUserManagementFeatureCore();
		return builder;
	}

	/// <summary>
	/// Registers user management services using the underlying <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register services with.</param>
	/// <returns>The original <see cref="IServiceCollection"/> for fluent chaining.</returns>
	/// <remarks>
	///     <para>
	///     This method registers an <see cref="InMemoryUserAuthenticationService"/> seeded with a single
	///     bootstrap administrator account (<c>admin</c> / <c>changeme</c>). Once a persistent user store
	///     is available, the registration will switch to a database-backed implementation.
	///     </para>
	///     <para>
	///     It is factored to operate on <see cref="IServiceCollection"/> so that it can be reused in
	///     integration tests and other hosting scenarios.
	///     </para>
	/// </remarks>
	public static IServiceCollection AddUserManagementFeatureCore(this IServiceCollection services)
	{
		var userService = new InMemoryUserAuthenticationService();
		userService.AddUser("admin", "changeme", RoleDefinitions.Admin.Name);
		services.AddSingleton<IUserAuthenticationService>(userService);

		return services;
	}
}
