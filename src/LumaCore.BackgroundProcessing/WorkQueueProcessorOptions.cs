// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.BackgroundProcessing;

/// <summary>
/// Configuration options for <see cref="WorkQueueProcessor"/>.
/// </summary>
/// <remarks>
///     <para>
///     This class is used with the options pattern (<c>IOptions&lt;WorkQueueProcessorOptions&gt;</c>) to configure
///     the <see cref="WorkQueueProcessor"/> when registered via dependency injection.
///     </para>
///     <para>
///         <b>Configuration example (appsettings.json):</b>
///         <code>
/// {
///   "WorkQueue": {
///     "MaxQueueSize": 5000,
///     "MaxConcurrency": 4,
///     "ShutdownTimeout": "00:01:00"
///   }
/// }
///     </code>
///     </para>
/// </remarks>
public sealed class WorkQueueProcessorOptions : IValidatableObject
{
	/// <summary>
	/// The default configuration section name for <see cref="WorkQueueProcessorOptions"/>.
	/// </summary>
	public const string DefaultSectionName = "WorkQueue";

	/// <summary>
	/// The default maximum number of items that can be queued.
	/// </summary>
	public const int DefaultMaxQueueSize = 10000;

	/// <summary>
	/// The default maximum number of work items that can be processed concurrently.
	/// </summary>
	public const int DefaultMaxConcurrency = 1;

	/// <summary>
	/// The default maximum time to wait for queued items to complete during shutdown.
	/// </summary>
	public static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum number of items that can be queued.
	/// </summary>
	/// <remarks>
	/// If the queue is full, <see cref="WorkQueueProcessor.QueueWorkItem(Action{CancellationToken})"/> and
	/// <see cref="WorkQueueProcessor.QueueWorkItem(Func{CancellationToken,Task})"/> will return <see langword="false"/>.
	/// Default is <see cref="DefaultMaxQueueSize"/> (10,000 items).
	/// </remarks>
	/// <value>The maximum queue size. Must be greater than zero. Default is 10,000.</value>
	[Range(1, int.MaxValue, ErrorMessage = "Queue size must be at least 1.")]
	public int MaxQueueSize { get; set; } = DefaultMaxQueueSize;

	/// <summary>
	/// Gets or sets the maximum number of work items that can be processed concurrently.
	/// </summary>
	/// <remarks>
	/// Default is <see cref="DefaultMaxConcurrency"/> (1, sequential processing). Set higher for parallel processing.
	/// </remarks>
	/// <value>The maximum concurrency level. Must be greater than zero. Default is 1 (sequential).</value>
	[Range(1, int.MaxValue, ErrorMessage = "Concurrency must be at least 1.")]
	public int MaxConcurrency { get; set; } = DefaultMaxConcurrency;

	/// <summary>
	/// Gets or sets the maximum time to wait for queued items to complete during shutdown.
	/// </summary>
	/// <remarks>
	///     <para>
	///     After this timeout, remaining queued (not yet started) items may be discarded. Already running work items
	///     are still awaited to completion (or cooperative cancellation), so shutdown may block indefinitely.
	///     </para>
	///     <para>
	///     Default is <see cref="DefaultShutdownTimeout"/> (30 seconds).
	///     </para>
	/// </remarks>
	/// <value>The shutdown timeout. Must be greater than <see cref="TimeSpan.Zero"/>. Default is 30 seconds.</value>
	public TimeSpan ShutdownTimeout { get; set; } = DefaultShutdownTimeout;

	/// <summary>
	/// Validates the options instance.
	/// </summary>
	/// <param name="validationContext">The validation context.</param>
	/// <returns>A collection of validation results. Empty if the options are valid.</returns>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (ShutdownTimeout <= TimeSpan.Zero)
		{
			yield return new ValidationResult(
				"Shutdown timeout must be greater than zero.",
				[nameof(ShutdownTimeout)]);
		}
	}
}
