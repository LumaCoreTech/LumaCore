// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.BackgroundProcessing;

/// <summary>
/// Defines the contract for a work queue processor that executes queued work items in the background.
/// </summary>
/// <remarks>
///     <para>
///     This interface exposes only the enqueueing API. Lifecycle management (initialization, shutdown, disposal)
///     is handled by the infrastructure (e.g., <see cref="WorkQueueProcessorHostedService"/>) and is intentionally
///     not part of this interface.
///     </para>
///     <para>
///     Consumers should inject <see cref="IWorkQueueProcessor"/> to queue work items. This enables easy mocking
///     in unit tests.
///     </para>
///     <para>
///         <b>Usage Examples:</b>
///         <code>
/// public class MyService(IWorkQueueProcessor processor)
/// {
///     // Fire-and-forget:
///     public void DoWorkFireAndForget()
///     {
///         if (!processor.QueueWorkItem(async ct => await SendEmailAsync(ct)))
///             logger.LogWarning("Queue full");
///     }
///     
///     // Track completion:
///     public async Task DoWorkAndWaitAsync()
///     {
///         var task = processor.QueueAndTrackWorkItem(async ct => await ImportDataAsync(ct));
///         if (task != null)
///             await task;
///     }
/// }
///     </code>
///     </para>
/// </remarks>
public interface IWorkQueueProcessor
{
	/// <summary>
	/// Gets an approximate count of work items currently queued.
	/// </summary>
	/// <remarks>
	/// This is an estimate and may not be 100% accurate in highly concurrent scenarios.
	/// Returns 0 if the processor is not initialized.
	/// </remarks>
	int QueuedItemCount { get; }

	#region QueueWorkItem (fire-and-forget)

	/// <summary>
	/// Queues an asynchronous work item for background execution (fire-and-forget).
	/// </summary>
	/// <param name="workItem">
	/// The async work item to execute. Receives a <see cref="CancellationToken"/> that is signaled during shutdown.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the item was successfully queued; <see langword="false"/> if the queue is full.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="workItem"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The processor is not initialized.</exception>
	/// <exception cref="ObjectDisposedException">The processor is disposed.</exception>
	/// <remarks>
	///     <para>
	///     This is the preferred method for fire-and-forget scenarios where you don't need to track completion
	///     or handle exceptions from the work item.
	///     </para>
	///     <para>
	///     Work items are executed in the order they were queued (FIFO) when <c>MaxConcurrency</c> is 1.
	///     With higher concurrency, order is generally preserved but not guaranteed due to parallel execution.
	///     </para>
	///     <para>
	///     The work item should respect the provided <see cref="CancellationToken"/> to allow cooperative
	///     cancellation during shutdown.
	///     </para>
	/// </remarks>
	bool QueueWorkItem(Func<CancellationToken, Task> workItem);

	/// <summary>
	/// Queues a synchronous work item for background execution (fire-and-forget).
	/// </summary>
	/// <param name="workItem">
	/// The synchronous work item to execute. Receives a <see cref="CancellationToken"/> that is signaled during shutdown.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the item was successfully queued; <see langword="false"/> if the queue is full.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="workItem"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The processor is not initialized.</exception>
	/// <exception cref="ObjectDisposedException">The processor is disposed.</exception>
	/// <remarks>
	/// The action is wrapped in a task internally. For async operations, prefer the
	/// <see cref="QueueWorkItem(Func{CancellationToken, Task})"/> overload.
	/// </remarks>
	bool QueueWorkItem(Action<CancellationToken> workItem);

	#endregion

	#region QueueAndTrackWorkItem (trackable)

	/// <summary>
	/// Queues an asynchronous work item for background execution and returns a task to track its completion.
	/// </summary>
	/// <param name="workItem">
	/// The async work item to execute. Receives a <see cref="CancellationToken"/> that is signaled during shutdown.
	/// </param>
	/// <returns>
	/// A <see cref="Task"/> that completes when the work item finishes execution, or <see langword="null"/> if the
	/// queue is full. The returned task propagates any exceptions thrown by the work item.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="workItem"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The processor is not initialized.</exception>
	/// <exception cref="ObjectDisposedException">The processor is disposed.</exception>
	/// <remarks>
	///     <para>
	///     Use this method when you need to:
	///     <list type="bullet">
	///         <item>Await completion of the work item</item>
	///         <item>Handle exceptions thrown by the work item</item>
	///         <item>Chain operations after the work item completes</item>
	///     </list>
	///     </para>
	///     <para>
	///     For simple fire-and-forget scenarios, prefer <see cref="QueueWorkItem(Func{CancellationToken, Task})"/>.
	///     </para>
	/// </remarks>
	Task? QueueAndTrackWorkItem(Func<CancellationToken, Task> workItem);

	/// <summary>
	/// Queues a synchronous work item for background execution and returns a task to track its completion.
	/// </summary>
	/// <param name="workItem">
	/// The synchronous work item to execute. Receives a <see cref="CancellationToken"/> that is signaled during shutdown.
	/// </param>
	/// <returns>
	/// A <see cref="Task"/> that completes when the work item finishes execution, or <see langword="null"/> if the
	/// queue is full. The returned task propagates any exceptions thrown by the work item.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="workItem"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The processor is not initialized.</exception>
	/// <exception cref="ObjectDisposedException">The processor is disposed.</exception>
	/// <remarks>
	/// The action is wrapped in a task internally. For async operations, prefer the
	/// <see cref="QueueAndTrackWorkItem(Func{CancellationToken, Task})"/> overload.
	/// </remarks>
	Task? QueueAndTrackWorkItem(Action<CancellationToken> workItem);

	#endregion
}
