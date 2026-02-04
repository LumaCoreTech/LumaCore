// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.Frozen;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LumaCore.Configuration;

/// <summary>
/// Tracks Options registrations and their configuration section names.
/// </summary>
/// <remarks>
///     <para>
///     This class maintains a registry of all Options types registered via
///     <see cref="OptionsRegistrationExtensions.AddFeatureOptions{TOptions}"/>. It stores the mapping between Options
///     types and their configuration section names, which is used by the System feature for diagnostic endpoints.
///     </para>
///     <para>
///     An instance of this class is created per <see cref="IServiceCollection"/> and registered as a singleton in the
///     DI container.
///     </para>
/// </remarks>
public sealed class OptionsTracker
{
	/// <summary>
	/// Lock object for thread-safe access to tracking collections during startup.
	/// </summary>
	private readonly Lock mLock = new();

	/// <summary>
	/// Maps Options types to their configuration section names.
	/// </summary>
	private readonly Dictionary<Type, string> mSectionNamesByType = new();

	/// <summary>
	/// Tracks which Options types were registered via <see cref="OptionsRegistrationExtensions.AddFeatureOptions{TOptions}"/>.
	/// </summary>
	private readonly HashSet<Type> mTrackedOptionsTypes = [];

	/// <summary>
	/// Frozen lookup for section names, created after finalization for fast runtime access.
	/// </summary>
	private FrozenDictionary<Type, string>? mFrozenSectionNames;

	/// <summary>
	/// Indicates whether registration has been finalized (after validation).
	/// </summary>
	private bool mIsFinalized;

	/// <summary>
	/// Gets the configuration section name for a registered Options type.
	/// </summary>
	/// <param name="optionsType">The Options type.</param>
	/// <returns>
	/// The section name if registered via <see cref="OptionsRegistrationExtensions.AddFeatureOptions{TOptions}"/>;
	/// otherwise, <see langword="null"/>.
	/// </returns>
	public string? GetSectionName(Type optionsType)
	{
		ArgumentNullException.ThrowIfNull(optionsType);

		// Use frozen dictionary if available (after finalization).
		if (mFrozenSectionNames is not null)
			return mFrozenSectionNames.GetValueOrDefault(optionsType);

		// Fall back to regular dictionary during startup.
		lock (mLock)
		{
			return mSectionNamesByType.GetValueOrDefault(optionsType);
		}
	}

	/// <summary>
	/// Gets all tracked Options types.
	/// </summary>
	/// <returns>A read-only collection of tracked Options types.</returns>
	public IReadOnlyCollection<Type> GetTrackedTypes()
	{
		if (mFrozenSectionNames is not null)
			return mFrozenSectionNames.Keys;

		lock (mLock)
		{
			return mTrackedOptionsTypes.ToList().AsReadOnly();
		}
	}

	/// <summary>
	/// Tracks an Options type and its configuration section name.
	/// </summary>
	/// <typeparam name="TOptions">The Options type being registered.</typeparam>
	/// <param name="sectionName">The configuration section name.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown if called after <see cref="Validate"/> has been invoked.
	/// </exception>
	public void Track<TOptions>(string sectionName)
		where TOptions : class
	{
		lock (mLock)
		{
			if (mIsFinalized)
			{
				throw new InvalidOperationException(
					$"Cannot register Options after {nameof(Validate)} has been called. " +
					"Ensure all features are registered before validation.");
			}

			mTrackedOptionsTypes.Add(typeof(TOptions));
			mSectionNamesByType[typeof(TOptions)] = sectionName;
		}
	}

	/// <summary>
	/// Validates that all LumaCore Options types were registered via
	/// <see cref="OptionsRegistrationExtensions.AddFeatureOptions{TOptions}"/>.
	/// </summary>
	/// <param name="services">The service collection to validate.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown if any Options type was registered without using
	/// <see cref="OptionsRegistrationExtensions.AddFeatureOptions{TOptions}"/>.
	/// </exception>
	/// <remarks>
	/// After validation, the section name mappings are frozen for efficient runtime access.
	/// </remarks>
	public void Validate(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Find all IConfigureOptions<T> registrations for LumaCore types.
		List<Type> allRegisteredOptions = services
			.Where(sd => sd.ServiceType.IsGenericType
			             && sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>))
			.Select(sd => sd.ServiceType.GetGenericArguments()[0])
			.Where(t => t.Namespace?.StartsWith("LumaCore", StringComparison.Ordinal) == true)
			.Distinct()
			.ToList();

		lock (mLock)
		{
			// Check for untracked registrations.
			List<Type> untrackedTypes = allRegisteredOptions
				.Where(t => !mTrackedOptionsTypes.Contains(t))
				.ToList();

			if (untrackedTypes.Count > 0)
			{
				string typeList = string.Join(", ", untrackedTypes.Select(t => t.Name));
				throw new InvalidOperationException(
					$"The following Options types were registered without using AddFeatureOptions<T>(): {typeList}. " +
					"Use builder.Services.AddFeatureOptions<T>(configuration, sectionName) " +
					"to ensure proper section tracking for diagnostic endpoints.");
			}

			// Finalize and freeze for runtime access.
			mIsFinalized = true;
			mFrozenSectionNames = mSectionNamesByType.ToFrozenDictionary();
		}
	}
}
