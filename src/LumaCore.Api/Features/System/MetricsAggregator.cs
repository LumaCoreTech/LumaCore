// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text.Json;

using LumaCore.Api.Contracts.V1.System;
using LumaCore.Core.Diagnostics;

namespace LumaCore.Api.Features.System;

/// <summary>
/// Aggregates core metrics and feature contributor metrics into a typed <see cref="SystemMetricsResponse"/>.
/// </summary>
/// <remarks>
///     <para>
///     Core metrics (GC, memory, process, thread pool) are collected via <see cref="SystemMetricsFactory"/> and
///     mapped to Contracts types via <see cref="MetricsMapper"/>. Feature contributors registered in the
///     <see cref="MetricsContributorRegistry"/> are collected separately and placed in the response's
///     <see cref="SystemMetricsResponse.Extensions"/> property.
///     </para>
///     <para>
///     If a feature contributor throws an exception, its section is set to <see langword="null"/> and the error
///     details are collected in a separate <c>_errors</c> extension property.
///     </para>
/// </remarks>
sealed partial class MetricsAggregator
{
	private readonly MetricsContributorRegistry mRegistry;
	private readonly IServiceProvider           mServiceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="MetricsAggregator"/> class.
	/// </summary>
	/// <param name="registry">The registry containing feature contributor descriptors.</param>
	/// <param name="serviceProvider">The service provider to resolve feature contributors.</param>
	public MetricsAggregator(MetricsContributorRegistry registry, IServiceProvider serviceProvider)
	{
		mRegistry = registry;
		mServiceProvider = serviceProvider;
	}

	/// <summary>
	/// Collects all metrics and assembles them into a typed response.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A <see cref="SystemMetricsResponse"/> containing core metrics and any feature extensions.
	/// </returns>
	public async Task<SystemMetricsResponse> CollectAllAsync(CancellationToken cancellationToken = default)
	{
		// Collect core metrics via SystemMetricsFactory and map to Contracts type.
		SystemMetricsResponse response = MetricsMapper.ToContract(SystemMetricsFactory.Create());

		// Collect feature extensions (if any contributors are registered).
		IDictionary<string, JsonElement>? extensions = await CollectFeatureExtensionsAsync(cancellationToken)
			                                               .ConfigureAwait(false);

		// Return response with extensions if any were collected.
		return extensions is null
			       ? response
			       : response with { Extensions = extensions };
	}

	/// <summary>
	/// Collects metrics from all registered feature contributors.
	/// </summary>
	/// <returns>
	/// A sorted dictionary with feature metrics and optional <c>_errors</c>, or <see langword="null"/> if no
	/// contributors are registered and no errors occurred.
	/// </returns>
	private async Task<IDictionary<string, JsonElement>?> CollectFeatureExtensionsAsync(
		CancellationToken cancellationToken)
	{
		IReadOnlyList<MetricsContributorDescriptor> descriptors = mRegistry.Descriptors;

		// No feature contributors registered — return null (no Extensions in response).
		if (descriptors.Count == 0)
			return null;

		// Use SortedDictionary with custom comparer: alphabetical, but _errors always last.
		var extensions = new SortedDictionary<string, JsonElement>(ErrorsLastComparer.Instance);
		var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (MetricsContributorDescriptor descriptor in descriptors)
		{
			(object? metrics, string? error) = await CollectFromContributorAsync(descriptor, cancellationToken)
				                                   .ConfigureAwait(false);

			// Serialize the metrics (or null) to JsonElement for extension data.
			// Serialization is wrapped separately because a contributor might return
			// a non-serializable object (circular references, HttpContext, etc.).
			JsonElement element;
			try
			{
				element = JsonSerializer.SerializeToElement(metrics);
			}
			catch (Exception ex)
			{
				// Serialization failed — treat as contributor error.
				element = JsonSerializer.SerializeToElement<object?>(null);
				error = $"SerializationException: {ex.Message}";
			}

			extensions[descriptor.SectionName] = element;

			if (error is not null)
				errors[descriptor.SectionName] = error;
		}

		// Add _errors section if any contributors failed.
		if (errors.Count > 0)
			extensions["_errors"] = JsonSerializer.SerializeToElement(errors);

		return extensions.Count > 0 ? extensions : null;
	}

	/// <summary>
	/// Collects metrics from a single feature contributor, handling errors gracefully.
	/// </summary>
	/// <returns>
	/// A tuple containing the metrics (or <see langword="null"/> on error) and an optional error message.
	/// </returns>
	private async Task<(object? Metrics, string? Error)> CollectFromContributorAsync(
		MetricsContributorDescriptor descriptor,
		CancellationToken            cancellationToken)
	{
		try
		{
			var contributor = (IMetricsContributor)mServiceProvider.GetRequiredService(descriptor.ImplementationType);
			object metrics = await contributor.CollectAsync(cancellationToken).ConfigureAwait(false);
			return (metrics, null);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Don't swallow cancellation — let it propagate so the request aborts cleanly.
			throw;
		}
		catch (Exception ex)
		{
			// Return null for the section and capture the error message.
			return (null, $"{ex.GetType().Name}: {ex.Message}");
		}
	}
}
