// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Data;

/// <summary>
/// Provides configuration settings for the resource garbage collector background service.
/// </summary>
/// <remarks>
///     <para>
///     This configuration is typically loaded from <c>appsettings.json</c> under the section specified by
///     <see cref="SectionName"/>. It controls the frequency, grace period, and batch size of the resource
///     cleanup cycle that reclaims orphaned files.
///     </para>
///     <para>
///     Values are bound via the options pattern and validated during startup.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     "ResourceCleanup": {
///         "Enabled": true,
///         "IntervalMinutes": 60,
///         "GracePeriodMinutes": 30,
///         "SweepBatchSize": 100
///     }
///     </code>
/// </example>
public sealed class ResourceCleanupOptions : IValidatableObject
{
	/// <summary>
	/// The configuration section name for resource cleanup options.
	/// </summary>
	public const string SectionName = "ResourceCleanup";

	/// <summary>
	/// Gets or sets a value indicating whether the resource garbage collector is enabled.
	/// </summary>
	/// <remarks>
	/// When <see langword="false"/>, the background service starts but immediately returns without
	/// executing any GC cycles. Orphaned resources accumulate until the service is re-enabled.
	/// </remarks>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the minimum interval in minutes between consecutive GC runs.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The actual interval may be longer if the previous cycle is still running or if the database throttle
	///     (<see cref="Entities.ResourceGcStateEntity.LastRunAtUtc"/>) indicates another instance completed a
	///     cycle recently (horizontal scaling safety).
	///     </para>
	///     <para>
	///     Must be at least <c>1</c> minute. Default: <c>60</c> minutes.
	///     </para>
	/// </remarks>
	[Range(1, int.MaxValue, ErrorMessage = "ResourceCleanup:IntervalMinutes must be at least 1.")]
	public int IntervalMinutes { get; set; } = 60;

	/// <summary>
	/// Gets or sets the grace period in minutes before orphaned resources become eligible for marking.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Resources with zero references that are younger than this grace period are not marked for deletion.
	///     This protects resources that have been uploaded but whose reference has not been attached yet
	///     (e.g., the upload succeeded but the owning entity creation is still in progress).
	///     </para>
	///     <para>
	///     Must be at least <c>5</c> minutes. Default: <c>30</c> minutes.
	///     </para>
	/// </remarks>
	[Range(5, int.MaxValue, ErrorMessage = "ResourceCleanup:GracePeriodMinutes must be at least 5.")]
	public int GracePeriodMinutes { get; set; } = 30;

	/// <summary>
	/// Gets or sets the maximum number of <see cref="Entities.ResourceDeletionState.PendingDeletion"/> resources
	/// to process per sweep cycle.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Limits I/O and database load per cycle. Resources beyond this limit are processed in the next cycle.
	///     </para>
	///     <para>
	///     Must be at least <c>1</c>. Default: <c>100</c>.
	///     </para>
	/// </remarks>
	[Range(1, int.MaxValue, ErrorMessage = "ResourceCleanup:SweepBatchSize must be at least 1.")]
	public int SweepBatchSize { get; set; } = 100;

	/// <inheritdoc/>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		// All constraints are handled by [Range] attributes. This method exists to satisfy
		// IValidatableObject for consistency with other options classes in the project.
		yield break;
	}
}
