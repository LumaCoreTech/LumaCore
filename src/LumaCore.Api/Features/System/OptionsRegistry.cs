// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Configuration;

using Microsoft.Extensions.Options;

namespace LumaCore.Api.Features.System;

/// <summary>
/// Maintains a registry of discovered Options types and provides access to their sanitized values.
/// </summary>
/// <remarks>
///     <para>
///     This class uses the <see cref="OptionsTracker"/> populated at application startup
///     to enumerate all registered Options types. It uses <see cref="OptionsSanitizer"/> to mask
///     any properties marked with <see cref="SecretAttribute"/> when exposing values through
///     diagnostic endpoints.
///     </para>
/// </remarks>
sealed class OptionsRegistry
{
	/// <summary>
	/// The service provider used to resolve <c>IOptions&lt;T&gt;</c> instances at runtime.
	/// </summary>
	private readonly IServiceProvider mServiceProvider;

	/// <summary>
	/// The tracker containing all registered Options types and their section names.
	/// </summary>
	private readonly OptionsTracker mTracker;

	/// <summary>
	/// Initializes a new instance of the <see cref="OptionsRegistry"/> class.
	/// </summary>
	/// <param name="tracker">The tracker containing registered Options types.</param>
	/// <param name="serviceProvider">The service provider for resolving IOptions instances.</param>
	public OptionsRegistry(OptionsTracker tracker, IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(tracker);
		ArgumentNullException.ThrowIfNull(serviceProvider);

		mTracker = tracker;
		mServiceProvider = serviceProvider;
	}

	/// <summary>
	/// Gets all registered Options types.
	/// </summary>
	public IReadOnlyCollection<Type> OptionsTypes => mTracker.GetTrackedTypes();

	/// <summary>
	/// Gets the configuration section name for an Options type.
	/// </summary>
	/// <param name="optionsType">The Options type.</param>
	/// <returns>
	/// The section name from registration tracking if available;
	/// otherwise the type name with the <c>Options</c> suffix removed (fallback).
	/// </returns>
	public string GetSectionName(Type optionsType)
	{
		ArgumentNullException.ThrowIfNull(optionsType);

		// Priority: Section name from registration tracking (the source of truth).
		string? trackedName = mTracker.GetSectionName(optionsType);

		// Return tracked name if available.
		if (trackedName is not null)
			return trackedName;

		// Fallback: Convention — strip "Options" suffix from type name.
		// This should only be reached for external/framework Options types.
		const string OptionsSuffix = "Options";
		string typeName = optionsType.Name;

		return typeName.EndsWith(OptionsSuffix, StringComparison.Ordinal)
			       ? typeName[..^OptionsSuffix.Length]
			       : typeName;
	}

	/// <summary>
	/// Gets all configuration values, grouped by section name, with secrets masked.
	/// </summary>
	/// <returns>
	/// A dictionary where keys are section names (e.g., <c>Jwt</c>, <c>Cors</c>) and values are dictionaries of
	/// sanitized property values.
	/// </returns>
	public IDictionary<string, IDictionary<string, object?>> GetAllSanitized()
	{
		// Use SortedDictionary for alphabetical ordering of sections.
		var result = new SortedDictionary<string, IDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);

		foreach (Type optionsType in OptionsTypes)
		{
			try
			{
				// Resolve the Options instance from DI.
				object? options = GetOptionsValue(optionsType);

				// Skip if the Options instance is not registered.
				if (options is null)
					continue;

				// Determine section name and sanitize the options object.
				string sectionName = GetSectionName(optionsType);
				IDictionary<string, object?> sanitized = OptionsSanitizer.Sanitize(options, optionsType);

				result[sectionName] = sanitized;
			}
			catch (InvalidOperationException)
			{
				// Options type could not be resolved from DI — skip silently.
				// This can happen if the type was registered but its dependencies are missing.
			}
		}

		return result;
	}

	/// <summary>
	/// Gets a single Options value by section name, with secrets masked.
	/// </summary>
	/// <param name="sectionName">The configuration section name (e.g., <c>Jwt</c>).</param>
	/// <returns>
	/// A dictionary of sanitized property values, or <see langword="null"/> if the section is not found.
	/// </returns>
	public IDictionary<string, object?>? GetSanitized(string sectionName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		// Find the Options type matching the requested section name (case-insensitive).
		Type? optionsType = OptionsTypes
			.FirstOrDefault(t => GetSectionName(t)
				.Equals(sectionName, StringComparison.OrdinalIgnoreCase));

		// Abort if section does not exist.
		if (optionsType is null)
			return null;

		// Resolve and sanitize the Options instance.
		object? options = GetOptionsValue(optionsType);

		return options is null
			       ? null
			       : OptionsSanitizer.Sanitize(options, optionsType);
	}

	/// <summary>
	/// Resolves the Options value from the DI container using reflection.
	/// </summary>
	/// <param name="optionsType">The Options type to resolve (e.g., <c>JwtOptions</c>).</param>
	/// <returns>The resolved Options instance, or <see langword="null"/> if not registered.</returns>
	private object? GetOptionsValue(Type optionsType)
	{
		// Construct IOptions<T> for the given Options type (e.g., IOptions<JwtOptions>).
		Type optionsWrapperType = typeof(IOptions<>).MakeGenericType(optionsType);

		// Resolve from DI container.
		object? optionsWrapper = mServiceProvider.GetService(optionsWrapperType);

		// Abort if the Options type is not registered.
		if (optionsWrapper is null)
			return null;

		// Extract the Value property from IOptions<T> which contains the actual Options instance.
		return optionsWrapperType.GetProperty("Value")?.GetValue(optionsWrapper);
	}
}
