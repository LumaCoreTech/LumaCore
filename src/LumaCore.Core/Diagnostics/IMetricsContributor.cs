// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Defines a contributor that provides diagnostic metrics for aggregation.
/// </summary>
/// <remarks>
///     <para>
///     Features implement this interface to expose their own metrics alongside core system metrics. Contributors
///     are registered via <see cref="MetricsContributorRegistry"/> with a section name, and their metrics are
///     aggregated by a consumer (e.g., an API endpoint, a monitoring agent, or a health check).
///     </para>
///     <para>
///     The section name is specified at registration time, not in the interface, enabling
///     <see cref="MetricsContributorRegistry"/> to perform fail-fast validation for duplicate or reserved names
///     during application startup.
///     </para>
///     <para>
///     <b>Error handling:</b> If <see cref="CollectAsync"/> throws an exception, the consumer should handle it
///     gracefully — typically by setting the contributor's section to <see langword="null"/> and reporting the
///     error separately.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// public sealed class MyFeatureMetricsContributor : IMetricsContributor
/// {
///     public async Task&lt;object&gt; CollectAsync(CancellationToken cancellationToken)
///     {
///         return new { ItemsProcessed = 42, QueueDepth = 7 };
///     }
/// }
/// </code>
/// </example>
public interface IMetricsContributor
{
	/// <summary>
	/// Collects metrics asynchronously.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// An object containing the metrics. Can be an anonymous type, a record, or any serializable object. The
	/// consumer determines how to serialize or present this data.
	/// </returns>
	/// <remarks>
	/// Implementations should be reasonably fast. For expensive operations, consider caching or returning stale
	/// data with a staleness indicator.
	/// </remarks>
	Task<object> CollectAsync(CancellationToken cancellationToken = default);
}
