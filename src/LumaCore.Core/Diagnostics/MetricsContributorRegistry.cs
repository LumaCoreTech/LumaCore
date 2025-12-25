// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Describes a registered metrics contributor with its section name.
/// </summary>
/// <param name="SectionName">The unique section name for this contributor's metrics.</param>
/// <param name="ImplementationType">The type implementing <see cref="IMetricsContributor"/>.</param>
public sealed record MetricsContributorDescriptor(
	string SectionName,
	Type   ImplementationType);

/// <summary>
/// Registry that tracks registered <see cref="IMetricsContributor"/> implementations and
/// validates for conflicts at registration time.
/// </summary>
/// <remarks>
///     <para>
///     This registry ensures fail-fast behavior by validating section names during registration
///     rather than at runtime when metrics are collected. Consumers (e.g., an API layer) can use
///     this registry to track contributors and resolve them from a DI container.
///     </para>
///     <para>
///     The registry is thread-safe and can be used during concurrent registration scenarios.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// var registry = new MetricsContributorRegistry();
/// registry.Register("memory", typeof(MemoryMetricsContributor));
/// registry.Register("ollama", typeof(OllamaMetricsContributor));
/// 
/// // Descriptors are sorted alphabetically by section name.
/// foreach (var descriptor in registry.Descriptors)
/// {
///     var contributor = serviceProvider.GetRequiredService(descriptor.ImplementationType);
///     // ...
/// }
/// </code>
/// </example>
public sealed class MetricsContributorRegistry
{
	/// <summary>
	/// Section names reserved for internal use by the metrics system.
	/// </summary>
	/// <remarks>
	/// These names cannot be used by feature contributors because they have special meaning in the response:
	/// <list type="bullet">
	///     <item><c>timestamp</c> — Snapshot time (first property)</item>
	///     <item><c>gc</c> — Core garbage collection metrics</item>
	///     <item><c>memory</c> — Core memory metrics</item>
	///     <item><c>process</c> — Core process metrics</item>
	///     <item><c>threadPool</c> — Core thread pool metrics</item>
	///     <item><c>_errors</c> — Error details from failed contributors</item>
	/// </list>
	/// Names starting with <c>_</c> are reserved for future meta-sections.
	/// </remarks>
	private static readonly HashSet<string> sReservedNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"timestamp",
		"gc",
		"memory",
		"process",
		"threadPool",
		"_errors"
	};

	/// <summary>
	/// Registered section names mapped to their descriptors (case-insensitive).
	/// </summary>
	private readonly Dictionary<string, MetricsContributorDescriptor> mDescriptors =
		new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Lock object for thread-safe registration.
	/// </summary>
	private readonly Lock mLock = new();

	/// <summary>
	/// Gets all registered contributor descriptors, sorted alphabetically by section name.
	/// </summary>
	/// <value>A snapshot of all registered descriptors at the time of access.</value>
	public IReadOnlyList<MetricsContributorDescriptor> Descriptors
	{
		get
		{
			lock (mLock)
			{
				return mDescriptors.Values
					.OrderBy(d => d.SectionName, StringComparer.OrdinalIgnoreCase)
					.ToList();
			}
		}
	}

	/// <summary>
	/// Validates and registers a metrics contributor.
	/// </summary>
	/// <param name="sectionName">The unique section name for this contributor's metrics.</param>
	/// <param name="implementationType">The type implementing <see cref="IMetricsContributor"/>.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="implementationType"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="sectionName"/> is <see langword="null"/>, empty, whitespace,
	/// a reserved name (<c>timestamp</c>, <c>gc</c>, <c>memory</c>, <c>process</c>, <c>threadPool</c>,
	/// <c>_errors</c>), or starts with <c>_</c>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown if <paramref name="sectionName"/> is already registered by another contributor.
	/// </exception>
	public void Register(string sectionName, Type implementationType)
	{
		ArgumentNullException.ThrowIfNull(implementationType);

		if (string.IsNullOrWhiteSpace(sectionName))
		{
			throw new ArgumentException(
				$"Metrics contributor '{implementationType.Name}' has an invalid section name. " +
				"Section name cannot be null, empty, or whitespace.",
				nameof(sectionName));
		}

		// Check for reserved names.
		if (sReservedNames.Contains(sectionName))
		{
			throw new ArgumentException(
				$"Metrics contributor '{implementationType.Name}' cannot use section name '{sectionName}' " +
				"because it is reserved for internal use.",
				nameof(sectionName));
		}

		// Block names starting with underscore (reserved for meta-sections).
		if (sectionName.StartsWith('_'))
		{
			throw new ArgumentException(
				$"Metrics contributor '{implementationType.Name}' cannot use section name '{sectionName}' " +
				"because names starting with '_' are reserved for meta-sections.",
				nameof(sectionName));
		}

		lock (mLock)
		{
			if (mDescriptors.TryGetValue(sectionName, out MetricsContributorDescriptor? existing))
			{
				throw new InvalidOperationException(
					$"Metrics contributor '{implementationType.Name}' cannot use section name '{sectionName}' " +
					$"because it is already registered by '{existing.ImplementationType.Name}'.");
			}

			mDescriptors[sectionName] = new MetricsContributorDescriptor(sectionName, implementationType);
		}
	}

	/// <summary>
	/// Registers a metrics contributor using a generic type parameter.
	/// </summary>
	/// <typeparam name="TContributor">The type implementing <see cref="IMetricsContributor"/>.</typeparam>
	/// <param name="sectionName">The unique section name for this contributor's metrics.</param>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="sectionName"/> is <see langword="null"/>, empty, whitespace,
	/// a reserved name (<c>timestamp</c>, <c>gc</c>, <c>memory</c>, <c>process</c>, <c>threadPool</c>,
	/// <c>_errors</c>), or starts with <c>_</c>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown if <paramref name="sectionName"/> is already registered by another contributor.
	/// </exception>
	public void Register<TContributor>(string sectionName)
		where TContributor : class, IMetricsContributor
	{
		Register(sectionName, typeof(TContributor));
	}
}
