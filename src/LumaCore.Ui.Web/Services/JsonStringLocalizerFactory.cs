// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Localization;

namespace LumaCore.Ui.Web.Services;

/// <summary>
/// Factory for creating <see cref="JsonStringLocalizer"/> instances.
/// Implements <see cref="IStringLocalizerFactory"/> to integrate with Blazor's localization system.
/// </summary>
/// <remarks>
///     <para>
///     This factory delegates to the DI container to retrieve the <see cref="JsonStringLocalizer"/>
///     instance instead of creating new instances. This ensures that the same Scoped instance is
///     used both when components inject <see cref="JsonStringLocalizer"/> directly and when
///     Blazor's validation system requests <see cref="IStringLocalizer"/>.
///     </para>
///     <para>
///     Without this delegation, two separate instances would exist, leading to state inconsistencies
///     (e.g., changing locale in one instance wouldn't affect the other).
///     </para>
/// </remarks>
public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
{
	private readonly IServiceProvider mServiceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonStringLocalizerFactory"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for resolving the localizer instance.</param>
	public JsonStringLocalizerFactory(IServiceProvider serviceProvider)
	{
		mServiceProvider = serviceProvider;
	}

	/// <summary>
	/// Creates a <see cref="JsonStringLocalizer"/> by retrieving it from the DI container.
	/// </summary>
	/// <param name="resourceSource">The type representing the resource source (not used in JSON-based implementation).</param>
	/// <returns>The Scoped <see cref="JsonStringLocalizer"/> instance from DI.</returns>
	public IStringLocalizer Create(Type resourceSource)
	{
		return mServiceProvider.GetRequiredService<JsonStringLocalizer>();
	}

	/// <summary>
	/// Creates a <see cref="JsonStringLocalizer"/> by retrieving it from the DI container.
	/// </summary>
	/// <param name="baseName">The base name of the resource (not used in JSON-based implementation).</param>
	/// <param name="location">The location of the resource (not used in JSON-based implementation).</param>
	/// <returns>The Scoped <see cref="JsonStringLocalizer"/> instance from DI.</returns>
	public IStringLocalizer Create(string baseName, string location)
	{
		return mServiceProvider.GetRequiredService<JsonStringLocalizer>();
	}
}
