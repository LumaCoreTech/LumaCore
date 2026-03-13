// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LumaCore.BackgroundProcessing;

/// <summary>
/// An <see cref="IHostedService"/> that manages the lifecycle of a <see cref="WorkQueueProcessor"/>.
/// </summary>
/// <remarks>
///     <para>
///     This hosted service initializes the <see cref="WorkQueueProcessor"/> when the host starts and
///     gracefully shuts it down when the host stops. This ensures proper integration with the
///     ASP.NET Core / Generic Host lifecycle.
///     </para>
///     <para>
///     The <see cref="WorkQueueProcessor"/> instance is registered as a singleton and can be injected
///     into other services to queue work items.
///     </para>
///     <para>
///         <b>Registration:</b>
///         <code>
/// services.AddWorkQueueProcessor(options =>
/// {
///     options.MaxConcurrency = 4;
/// });
///     </code>
///     </para>
/// </remarks>
public sealed class WorkQueueProcessorHostedService : IHostedService
{
	private readonly WorkQueueProcessor                       mProcessor;
	private readonly ILogger<WorkQueueProcessorHostedService> mLogger;

	/// <summary>
	/// Initializes a new instance of the <see cref="WorkQueueProcessorHostedService"/> class.
	/// </summary>
	/// <param name="processor">The <see cref="WorkQueueProcessor"/> instance to manage.</param>
	/// <param name="logger">The logger for this hosted service.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="processor"/> or <paramref name="logger"/> is <see langword="null"/>.
	/// </exception>
	public WorkQueueProcessorHostedService(
		WorkQueueProcessor                       processor,
		ILogger<WorkQueueProcessorHostedService> logger)
	{
		mProcessor = processor ?? throw new ArgumentNullException(nameof(processor));
		mLogger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Starts the <see cref="WorkQueueProcessor"/> by calling <see cref="WorkQueueProcessor.InitializeAsync"/>.
	/// </summary>
	/// <param name="cancellationToken">A token that signals when the host is stopping.</param>
	/// <returns>A task that completes when the processor has been initialized.</returns>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		mLogger.LogDebug("Starting WorkQueueProcessor...");
		await mProcessor.InitializeAsync(cancellationToken).ConfigureAwait(false);
		mLogger.LogInformation("WorkQueueProcessor started");
	}

	/// <summary>
	/// Stops the <see cref="WorkQueueProcessor"/> by calling <see cref="WorkQueueProcessor.ShutdownAsync"/>.
	/// </summary>
	/// <param name="cancellationToken">
	/// A token that signals when the host is forcing shutdown. Note that the processor has its own
	/// <see cref="WorkQueueProcessorOptions.ShutdownTimeout"/> that controls the graceful shutdown period.
	/// </param>
	/// <returns>A task that completes when the processor has been shut down.</returns>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		mLogger.LogDebug("Stopping WorkQueueProcessor...");
		await mProcessor.ShutdownAsync().ConfigureAwait(false);
		mLogger.LogInformation("WorkQueueProcessor stopped");
	}
}
