// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data;
using LumaCore.Data.Initialization;

namespace LumaCore.Api.Features.Data;

/// <summary>
/// Provides extension methods for registering the Data feature.
/// </summary>
/// <remarks>
///     <para>
///     The Data feature provides multi-database support via Entity Framework Core.
///     Supported providers: SQLite (default), PostgreSQL, SQL Server, MySQL (when available).
///     </para>
///     <para>
///     Configuration is read from the <c>Database</c> section in <c>appsettings.json</c>.
///     The <see cref="DatabaseInitializer"/> handles automatic migrations and default data seeding.
///     </para>
/// </remarks>
public static class ServiceRegistration
{
	/// <summary>
	/// Registers database services using the <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <returns>
	/// The modified application builder.
	/// </returns>
	/// <remarks>
	/// This is a convenience wrapper that forwards to <see cref="AddDataFeatureCore"/> using the
	/// <see cref="IServiceCollection"/> and <see cref="IConfiguration"/> exposed by the builder.
	/// </remarks>
	public static WebApplicationBuilder AddDataFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddDataFeatureCore(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// Registers database services using the underlying <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register database services with.</param>
	/// <param name="configuration">The application configuration used to bind <see cref="DatabaseOptions"/>.</param>
	/// <returns>The original <see cref="IServiceCollection"/> for fluent chaining.</returns>
	/// <remarks>
	///     <para>
	///     This registers and validates the <see cref="DatabaseOptions"/> configuration from the <c>Database</c> section
	///     in <c>appsettings.json</c>.
	///     </para>
	///     <para>
	///     Validation occurs at startup (<c>ValidateOnStart()</c>), so invalid configuration will cause the application
	///     to fail fast with a clear error message.
	///     </para>
	/// </remarks>
	public static IServiceCollection AddDataFeatureCore(
		this IServiceCollection services,
		IConfiguration          configuration)
	{
		return services.AddLumaCoreData(configuration);
	}
}
