// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Configuration;
using LumaCore.Core.IO;

namespace LumaCore.Api.Features.StreamBufferPool;

static class ServiceRegistration
{
	/// <summary>
	/// Registers the system's global stream buffer pool and its options binding
	/// using the <see cref="WebApplicationBuilder"/> facade.
	/// </summary>
	/// <param name="builder">The web application builder.</param>
	/// <returns>The modified application builder.</returns>
	/// <remarks>
	/// This is a convenience wrapper that forwards to <see cref="AddStreamBufferPoolFeatureCore"/> using the
	/// <see cref="IServiceCollection"/> and <see cref="IConfiguration"/> exposed by the builder.
	/// </remarks>
	public static WebApplicationBuilder AddStreamBufferPoolFeature(this WebApplicationBuilder builder)
	{
		builder.Services.AddStreamBufferPoolFeatureCore(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// Registers the system's global stream buffer pool and its options binding
	/// using the underlying <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The service collection to register authentication services with.</param>
	/// <param name="configuration">The application configuration used to bind <see cref="StreamBufferPoolOptions"/>.</param>
	/// <returns>The original <see cref="IServiceCollection"/> for fluent chaining.</returns>
	public static IServiceCollection AddStreamBufferPoolFeatureCore(
		this IServiceCollection services,
		IConfiguration          configuration)
	{
		// Bind and validate stream buffer pool options at startup so misconfiguration fails fast.
		services.AddFeatureOptions<StreamBufferPoolOptions>(configuration, StreamBufferPoolOptions.SectionName);

		// Register the stream buffer pool as a singleton since it manages its own internal pooling and is thread-safe.
		services.AddSingleton<IStreamBufferPool, Core.IO.StreamBufferPool>();

		return services;
	}
}
