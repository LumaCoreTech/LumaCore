// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Text.Json;
using System.Text.Json.Serialization;

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Response containing runtime metrics for the LumaCore instance.
/// </summary>
/// <param name="Timestamp">UTC time when this metrics snapshot was captured.</param>
/// <param name="Gc">Garbage collection metrics including collection counts and configuration.</param>
/// <param name="Memory">Memory usage metrics including managed heap, process memory, and system memory.</param>
/// <param name="Process">Process-level metrics including thread count, handle count, and uptime.</param>
/// <param name="ThreadPool">.NET thread pool metrics including available threads and pending work items.</param>
/// <remarks>
///     <para>
///     This response provides a comprehensive snapshot of runtime diagnostics including memory usage, garbage
///     collection activity, process resources, and thread pool state. All values represent a point-in-time snapshot
///     taken at <see cref="Timestamp"/>.
///     </para>
///     <para>
///     Use these metrics for operational monitoring, performance debugging, and capacity planning. For time-series
///     analysis, capture snapshots at regular intervals and track changes over time.
///     </para>
///     <para>
///     <b>Feature Extensions:</b> Additional metrics from registered feature contributors appear as top-level
///     properties in the JSON response via <see cref="Extensions"/>. These are dynamically contributed and not
///     documented in OpenAPI. See each feature's documentation for its metrics schema.
///     </para>
///     <para>
///     <b>Error Handling:</b> If any contributor fails, its section is set to <see langword="null"/> and error
///     details appear in an <c>_errors</c> property (also via <see cref="Extensions"/>).
///     </para>
/// </remarks>
public sealed record SystemMetricsResponse(
	DateTime          Timestamp,
	GcMetrics         Gc,
	MemoryMetrics     Memory,
	ProcessMetrics    Process,
	ThreadPoolMetrics ThreadPool)
{
	/// <summary>
	/// Additional metrics from registered feature contributors and error information.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Feature-contributed metrics appear as top-level JSON properties (e.g., <c>"ollama": { ... }</c>).
	///     If any contributor fails, an <c>_errors</c> property contains error details.
	///     </para>
	///     <para>
	///     This property is <see langword="null"/> when no feature contributors are registered and no errors occurred.
	///     </para>
	/// </remarks>
	[JsonExtensionData]
	public IDictionary<string, JsonElement>? Extensions { get; init; }
}
