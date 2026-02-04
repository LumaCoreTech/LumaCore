// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using System.Transactions;

using LumaCore.Core;

using Microsoft.Extensions.Logging;

namespace LumaCore.BackgroundProcessing;

/// <summary>
/// A work queue processor that executes queued asynchronous work items in the background.
/// </summary>
/// <remarks>
///     <para>
///     This processor provides a thread-safe queue for enqueueing work items (async functions/actions)
///     that will be processed in the background. This is useful for:
///     <list type="bullet">
///         <item>Offloading work from request/caller threads to avoid blocking</item>
///         <item>Sequential or parallel processing of asynchronous operations</item>
///         <item>Fire-and-forget operations with completion guarantees (see shutdown behavior below)</item>
///         <item>Limiting concurrent execution of resource-intensive operations</item>
///     </list>
///     </para>
///     <para>
///     <b>Completion guarantees and shutdown behavior:</b>
///     Work items for which
///     <see cref="QueueWorkItem(Func{CancellationToken,Task})"/> or
///     <see cref="QueueWorkItem(Action{CancellationToken})"/> returned <see langword="true"/> are guaranteed
///     to be started and awaited during normal operation.
///     When shutdown is initiated, the processor stops accepting new items and attempts to drain the queue.
///     If the configured shutdown timeout elapses, remaining queued (not yet started) work items may be discarded.
///     Already running work items are still awaited to completion (or cooperative cancellation), and shutdown may
///     therefore block indefinitely. This is a design choice to ensure that in-flight operations are not abruptly
///     terminated, which could lead to data corruption or inconsistent state. Nevertheless, it is recommended that
///     work items respect the provided <see cref="CancellationToken"/> to allow cooperative cancellation during shutdown.
///     </para>
///     <para>
///         <b>Key Features:</b>
///         <list type="bullet">
///             <item>Thread-safe enqueueing from multiple threads</item>
///             <item>Sequential or parallel processing (FIFO order, configurable concurrency)</item>
///             <item>Graceful shutdown: waits for queued items to complete (with configurable timeout)</item>
///             <item>Exception handling: errors in work items don't crash the processor</item>
///             <item>Configurable queue capacity and shutdown timeout</item>
///         </list>
///     </para>
///     <para>
///         <b>Usage Example:</b>
///         <code>
/// // Create and initialize the processor:
/// var processor = await WorkQueueProcessor.CreateAsync(
///     loggerFactory,
///     maxQueueSize: 1000,
///     shutdownTimeout: TimeSpan.FromSeconds(10));
/// 
/// // Enqueue work:
/// processor.QueueWorkItem(async ct =>
/// {
///     await repository.UpdateAsync(..., ct);
/// });
/// 
/// // Shutdown (or use DisposeAsync):
/// await processor.DisposeAsync();
/// </code>
///     </para>
/// </remarks>
public sealed class WorkQueueProcessor : LifecycleManagement, IWorkQueueProcessor
{
	/// <summary>
	/// Threshold for cleaning up completed tasks in the running tasks list during parallel processing.
	/// Cleanup occurs when the list size exceeds this multiple of <see cref="mMaxConcurrency"/>.
	/// </summary>
	private const int ParallelTaskCleanupThresholdMultiplier = 2;

	/// <summary>
	/// Interval for logging running work items during shutdown waits.
	/// </summary>
	private static readonly TimeSpan sShutdownLoggingInterval = TimeSpan.FromSeconds(10);

	private readonly int                                             mMaxQueueSize;
	private readonly TimeSpan                                        mShutdownTimeout;
	private readonly int                                             mMaxConcurrency;
	private readonly ConcurrentDictionary<long, RunningWorkItemInfo> mRunningWorkItems = new();

	private Channel<QueuedWorkItem>? mWorkChannel;
	private CancellationTokenSource? mShutdownTokenSource;
	private Task?                    mBackgroundTask;
	private long                     mNextWorkItemId;

	/// <summary>
	/// Creates a new instance of the <see cref="WorkQueueProcessor"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory used to create a logger for this instance.</param>
	/// <param name="maxQueueSize">
	/// The maximum number of items that can be queued.
	/// If the queue is full, <see cref="QueueWorkItem(Action{CancellationToken})"/> and
	/// <see cref="QueueWorkItem(Func{CancellationToken,Task})"/> will return <see langword="false"/>.
	/// Default is 10,000 items.
	/// </param>
	/// <param name="shutdownTimeout">
	/// The maximum time to wait for queued items to complete during shutdown.
	/// After this timeout, remaining queued (not yet started) items may be discarded. Already running work items are still
	/// awaited to completion (or cooperative cancellation), so shutdown may block indefinitely. Default is 30 seconds.
	/// </param>
	/// <param name="maxConcurrency">
	/// The maximum number of work items that can be processed concurrently.
	/// Default is 1 (sequential processing). Set higher for parallel processing.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxQueueSize"/> is less than or equal to zero,
	/// when <paramref name="maxConcurrency"/> is less than or equal to zero,
	/// or when <paramref name="shutdownTimeout"/> is negative or zero.
	/// </exception>
	/// <remarks>
	/// After construction, call <see cref="InitializeAsync"/> to start the background processing.
	/// Alternatively, use <see cref="CreateAsync"/> for a fully initialized instance.
	/// </remarks>
	public WorkQueueProcessor(
		ILoggerFactory loggerFactory,
		int            maxQueueSize    = 10000,
		TimeSpan?      shutdownTimeout = null,
		int            maxConcurrency  = 1) :
		base(loggerFactory)
	{
		if (maxQueueSize <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxQueueSize), "Queue size must be positive.");

		if (maxConcurrency <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Concurrency level must be positive.");

		if (shutdownTimeout.HasValue && shutdownTimeout.Value <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(shutdownTimeout), "Shutdown timeout must be positive.");

		mMaxQueueSize = maxQueueSize;
		mShutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(30);
		mMaxConcurrency = maxConcurrency;
	}

	/// <summary>
	/// Gets an approximate count of work items currently queued.
	/// </summary>
	/// <remarks>
	/// This is an estimate and may not be 100% accurate in highly concurrent scenarios.
	/// Returns 0 if the processor is not initialized.
	/// </remarks>
	public int QueuedItemCount => mWorkChannel?.Reader.Count ?? 0;

	/// <summary>
	/// Creates and initializes a new <see cref="WorkQueueProcessor"/> instance.
	/// </summary>
	/// <param name="loggerFactory">The logger factory used to create a logger for this instance.</param>
	/// <param name="maxQueueSize">
	/// The maximum number of items that can be queued.
	/// If the queue is full, <see cref="QueueWorkItem(Action{CancellationToken})"/> and
	/// <see cref="QueueWorkItem(Func{CancellationToken,Task})"/> will return <see langword="false"/>.
	/// Default is 10,000 items.
	/// </param>
	/// <param name="shutdownTimeout">
	/// The maximum time to wait for queued items to complete during shutdown.
	/// After this timeout, remaining queued (not yet started) items may be discarded. Already running work items are still
	/// awaited to completion (or cooperative cancellation), so shutdown may block indefinitely. Default is 30 seconds.
	/// </param>
	/// <param name="maxConcurrency">
	/// The maximum number of work items that can be processed concurrently.
	/// Default is 1 (sequential processing). Set higher for parallel processing.
	/// </param>
	/// <param name="cancellationToken">A cancellation token that can be used to abort the initialization.</param>
	/// <returns>A fully initialized <see cref="WorkQueueProcessor"/> instance.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxQueueSize"/> is less than or equal to zero,
	/// when <paramref name="maxConcurrency"/> is less than or equal to zero,
	/// or when <paramref name="shutdownTimeout"/> is negative or zero.
	/// </exception>
	public static async Task<WorkQueueProcessor> CreateAsync(
		ILoggerFactory    loggerFactory,
		int               maxQueueSize      = 10000,
		TimeSpan?         shutdownTimeout   = null,
		int               maxConcurrency    = 1,
		CancellationToken cancellationToken = default)
	{
		var service = new WorkQueueProcessor(loggerFactory, maxQueueSize, shutdownTimeout, maxConcurrency);
		await service.InitializeAsync(cancellationToken).ConfigureAwait(false);
		return service;
	}

	/// <summary>
	/// Represents a work item in the queue along with its optional completion source.
	/// </summary>
	/// <param name="WorkItem">The async function to execute.</param>
	/// <param name="CompletionSource">
	/// The source used to signal completion to the caller, or <see langword="null"/> for fire-and-forget items.
	/// </param>
	private readonly record struct QueuedWorkItem(
		Func<CancellationToken, Task>  WorkItem,
		TaskCompletionSource<object?>? CompletionSource);

	/// <summary>
	/// Stores metadata about a currently running work item.
	/// </summary>
	private readonly record struct RunningWorkItemInfo(long Id, DateTimeOffset StartedAtUtc);


	#region Lifecycle Methods

	/// <summary>
	/// Asynchronously initializes the processor by starting the background processing task.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token that can be used to abort the initialization.</param>
	/// <returns>A task that completes when the processor is initialized and ready to accept work items.</returns>
	/// <exception cref="InvalidOperationException">The processor is already initialized.</exception>
	/// <exception cref="ObjectDisposedException">The processor is disposing or already disposed.</exception>
	/// <exception cref="OperationCanceledException">The operation was canceled.</exception>
	public Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		return InitializeAsync(new LifecycleContext(), cancellationToken);
	}

	/// <summary>
	/// Shuts down the processor gracefully, waiting for queued items to complete.
	/// </summary>
	/// <returns>A task that completes when the processor has shut down.</returns>
	/// <exception cref="ObjectDisposedException">The processor is disposing or already disposed.</exception>
	/// <remarks>
	///     <para>
	///     This method is thread-safe and idempotent - it can be called multiple times safely.
	///     Concurrent calls will wait for the same shutdown operation to complete.
	///     </para>
	///     <para>
	///     After shutdown, the processor can be re-initialized by calling <see cref="InitializeAsync"/>.
	///     For permanent disposal, use <see cref="DisposeAsync"/> instead.
	///     </para>
	/// </remarks>
	public Task ShutdownAsync()
	{
		return ShutdownAsync(new LifecycleContext());
	}

	/// <inheritdoc/>
	public override ValueTask DisposeAsync()
	{
		return DisposeAsync(new LifecycleContext());
	}

	/// <inheritdoc/>
	protected override Task OnInitializingAsync(ILifecycleContext context, CancellationToken cancellationToken)
	{
		// Create a bounded channel with configurable concurrency.
		// The channel is fully async and doesn't block any threads.
		// We create a new channel on each initialization to support re-initialization after shutdown.
		var channelOptions = new BoundedChannelOptions(mMaxQueueSize)
		{
			// We use TryWrite in QueueWorkItem(), so enqueueing never blocks.
			// With FullMode=Wait, TryWrite() returns false when the channel is full (the behavior we want).
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = mMaxConcurrency == 1, // Optimization when sequential
			SingleWriter = false                 // Multiple threads can enqueue
		};
		mWorkChannel = Channel.CreateBounded<QueuedWorkItem>(channelOptions);

		mShutdownTokenSource = new CancellationTokenSource();
		mNextWorkItemId = 0;
		mRunningWorkItems.Clear();

		// Start the background processing task using Task.Run() (uses thread pool).
		// No need for LongRunning - the async processing won't block threads.
		mBackgroundTask = Task.Run(ProcessQueueAsync, CancellationToken.None);

		// Log initialization complete.
		Log.LogDebug("Processor started with max concurrency: {MaxConcurrency}.", mMaxConcurrency);

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	protected override async Task OnShuttingDownAsync(ILifecycleContext context)
	{
		// This method may be called to clean up after a failed OnInitializingAsync().
		// In that case, mWorkChannel, mShutdownTokenSource and mBackgroundTask may be null or partially initialized.

		if (mWorkChannel == null || mBackgroundTask == null || mShutdownTokenSource == null)
		{
			// Processor was never fully initialized - clean up any partially initialized resources.
			mWorkChannel?.Writer.TryComplete();
			mWorkChannel = null;
			mShutdownTokenSource?.Dispose();
			mShutdownTokenSource = null;
			mBackgroundTask = null; // Don't call Task.Dispose() - it's obsolete and can cause issues
			return;
		}

		bool timeoutReached = false;
		try
		{
			Log.LogDebug("Stopping processor ({QueuedItemCount} items in queue).", mWorkChannel.Reader.Count);

			// Mark the channel as complete - no new items will be accepted.
			mWorkChannel.Writer.TryComplete();

			// Wait for the queue to be drained (with timeout).
			using var timeoutCts = new CancellationTokenSource(mShutdownTimeout);
			try
			{
				// Wait for the background task to process remaining items.
				await mBackgroundTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
				Log.LogDebug("Processor stopped gracefully. All items processed.");
			}
			catch (OperationCanceledException)
			{
				timeoutReached = true;

				// Timeout occurred - first cancel the shutdown token to stop the background task
				// from reading more items, then discard the remaining queued items.
				// This order avoids a race condition where both the background task and
				// DiscardQueuedItems would read from the channel concurrently.
				await mShutdownTokenSource.CancelAsync().ConfigureAwait(false);

				// Now that the background task has stopped reading, we can safely discard remaining items.
				int discarded = DiscardQueuedItems(mWorkChannel);
				Log.LogWarning(
					"Processor shutdown timeout reached. Discarded {DiscardedItemCount} queued item(s). Waiting for running work items to complete...",
					discarded);
			}
		}
		catch (Exception ex)
		{
			// Invariant: LifecycleManagement requires OnShuttingDownAsync() not to throw.
			// Any unexpected exceptions are logged and swallowed to keep the instance in a valid state.
			Log.LogError(ex, "Unexpected error during shutdown.");

			// Ensure running work items get a cancellation signal when the shutdown flow fails unexpectedly.
			// This is best-effort and must not throw.
			try
			{
				await mShutdownTokenSource.CancelAsync().ConfigureAwait(false);
			}
			catch
			{
				// Intentionally swallow - OnShuttingDownAsync() must not throw.
			}
		}
		finally
		{
			// Invariant: After shutdown completes, no background tasks must still be running.
			// This wait ensures we never dispose/null members while work items are still executing.
			try
			{
				await WaitForBackgroundTaskCompletionAsync(mBackgroundTask, timeoutReached).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// LifecycleManagement requires OnShuttingDownAsync() not to throw.
				// Log and swallow unexpected background task failures.
				Log.LogCritical(ex, "Background processing task failed during shutdown.");
			}

			// Clean up references to allow re-initialization and GC collection.
			mWorkChannel = null;
			mBackgroundTask = null;
			mShutdownTokenSource.Dispose();
			mShutdownTokenSource = null;
			mRunningWorkItems.Clear();

			if (timeoutReached)
				Log.LogWarning("Processor shutdown complete (after timeout escalation).");
			else
				Log.LogDebug("Processor shutdown complete.");
		}
	}

	/// <inheritdoc/>
	protected override Task OnDisposingAsync(ILifecycleContext context)
	{
		// Invariant: LifecycleManagement guarantees that OnShuttingDownAsync() always completes
		// before OnDisposingAsync() is called. OnShuttingDownAsync() nulls all resources in its
		// finally block, so this condition can never be true under normal operation.
		if (mWorkChannel != null || mShutdownTokenSource != null || mBackgroundTask != null)
		{
			throw new UnreachableException(
				"OnDisposingAsync() called with non-null resources. " +
				"LifecycleManagement guarantees OnShuttingDownAsync() completes before OnDisposingAsync().");
		}

		return Task.CompletedTask;
	}

	#endregion

	#region Public API - Fire-and-forget

	/// <summary>
	/// Enqueues an asynchronous work item for background processing (fire-and-forget).
	/// </summary>
	/// <param name="workItem">
	/// The async function to execute. It receives a <see cref="CancellationToken"/> that is signaled
	/// during shutdown to allow the work item to abort gracefully.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the work item was successfully queued;
	/// <see langword="false"/> if the queue is full or if the processor is shutting down.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="workItem"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the processor is not initialized.
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// Thrown when the processor is disposing or already disposed.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This is the preferred method for fire-and-forget scenarios where you don't need to track
	///     completion or handle exceptions from the work item.
	///     </para>
	///     <para>
	///     This method is thread-safe and can be called from multiple threads concurrently.
	///     When <c>maxConcurrency</c> is <c>1</c>, work items are <em>started</em> in FIFO order.
	///     When <c>maxConcurrency</c> is greater than <c>1</c>, work items are dequeued in FIFO order,
	///     but the actual start and completion order is not guaranteed.
	///     </para>
	///     <para>
	///     <b>Important:</b> Work items are executed in a clean context without inheriting ambient state
	///     (such as <see cref="Transaction.Current"/>, <c>HttpContext</c>, etc.) from the calling thread.
	///     </para>
	/// </remarks>
	public bool QueueWorkItem(Func<CancellationToken, Task> workItem)
	{
		ArgumentNullException.ThrowIfNull(workItem);

		// Begin an operation scope for lifecycle management.
		// Also ensures that the processor is initialized.
		using OperationScope _ = BeginOperation();

		// Fire-and-forget: no TaskCompletionSource needed
		var queuedItem = new QueuedWorkItem(workItem, CompletionSource: null);

		// Enqueue the work item.
		return mWorkChannel!.Writer.TryWrite(queuedItem);
	}

	/// <summary>
	/// Enqueues a synchronous work item for background processing (fire-and-forget).
	/// </summary>
	/// <param name="workItem">The action to execute.</param>
	/// <returns>
	/// <see langword="true"/> if the work item was successfully queued;
	/// <see langword="false"/> if the queue is full or if the processor is shutting down.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="workItem"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the processor is not initialized.
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// Thrown when the processor is disposing or already disposed.
	/// </exception>
	/// <remarks>
	/// This is a convenience method for synchronous operations.
	/// The action receives the processor's shutdown <see cref="CancellationToken"/>, allowing the work item
	/// to cancel cooperatively during shutdown.
	/// </remarks>
	public bool QueueWorkItem(Action<CancellationToken> workItem)
	{
		ArgumentNullException.ThrowIfNull(workItem);

		// Wrap the action in a Func that returns Task.CompletedTask
		return QueueWorkItem(ct =>
		{
			workItem(ct);
			return Task.CompletedTask;
		});
	}

	#endregion

	#region Public API - Trackable

	/// <summary>
	/// Enqueues an asynchronous work item for background processing and returns a task to track its completion.
	/// </summary>
	/// <param name="workItem">
	/// The async function to execute. It receives a <see cref="CancellationToken"/> that is signaled
	/// during shutdown to allow the work item to abort gracefully.
	/// </param>
	/// <returns>
	/// A <see cref="Task"/> that completes when the work item finishes execution, or <see langword="null"/> if the
	/// queue is full. The returned task propagates any exceptions thrown by the work item.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="workItem"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the processor is not initialized.
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// Thrown when the processor is disposing or already disposed.
	/// </exception>
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
	///     <para>
	///     <b>Important:</b> Work items are executed in a clean context without inheriting ambient state
	///     (such as <see cref="Transaction.Current"/>, <c>HttpContext</c>, etc.) from the calling thread.
	///     </para>
	/// </remarks>
	public Task? QueueAndTrackWorkItem(Func<CancellationToken, Task> workItem)
	{
		ArgumentNullException.ThrowIfNull(workItem);

		// Begin an operation scope for lifecycle management.
		// Also ensures that the processor is initialized.
		using OperationScope _ = BeginOperation();

		// Create a TaskCompletionSource to allow callers to await completion.
		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var queuedItem = new QueuedWorkItem(workItem, tcs);

		// Enqueue the work item.
		if (mWorkChannel!.Writer.TryWrite(queuedItem))
			return tcs.Task;

		return null;
	}

	/// <summary>
	/// Enqueues a synchronous work item for background processing and returns a task to track its completion.
	/// </summary>
	/// <param name="workItem">The action to execute.</param>
	/// <returns>
	/// A <see cref="Task"/> that completes when the work item finishes execution, or <see langword="null"/> if the
	/// queue is full. The returned task propagates any exceptions thrown by the work item.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="workItem"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the processor is not initialized.
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// Thrown when the processor is disposing or already disposed.
	/// </exception>
	/// <remarks>
	/// The action is wrapped in a task internally. For async operations, prefer the
	/// <see cref="QueueAndTrackWorkItem(Func{CancellationToken, Task})"/> overload.
	/// </remarks>
	public Task? QueueAndTrackWorkItem(Action<CancellationToken> workItem)
	{
		ArgumentNullException.ThrowIfNull(workItem);

		// Wrap the action in a Func that returns Task.CompletedTask
		return QueueAndTrackWorkItem(ct =>
		{
			workItem(ct);
			return Task.CompletedTask;
		});
	}

	#endregion

	#region Background Processing

	/// <summary>
	/// The background loop that processes queued work items.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is a fully async loop that uses <see cref="ChannelReader{T}.ReadAllAsync"/> to consume items.
	///     The <see langword="await"/> on <c>ReadAllAsync()</c> does NOT block a thread - it uses async/await efficiently.
	///     When the channel is empty, the <see langword="await"/> yields control back to the ThreadPool.
	///     </para>
	///     <para>
	///     When <c>maxConcurrency</c> is greater than 1, work items are processed in parallel using a
	///     <see cref="SemaphoreSlim"/> to limit the number of concurrent executions.
	///     </para>
	/// </remarks>
	private async Task ProcessQueueAsync()
	{
		Log.LogDebug("Background processing task started.");

		try
		{
			if (mMaxConcurrency == 1)
			{
				// Sequential processing - simpler implementation
				await ProcessSequentiallyAsync().ConfigureAwait(false);
			}
			else
			{
				// Parallel processing with concurrency limit
				await ProcessInParallelAsync().ConfigureAwait(false);
			}

			Log.LogDebug("Background processing task completed normally.");
		}
		catch (OperationCanceledException)
		{
			// Shutdown was requested before channel was drained
			Log.LogDebug("Background processing task cancelled.");
		}
		catch (Exception ex)
		{
			// Unexpected error in the background loop - this should not happen as ExecuteWorkItemAsync()
			// catches all exceptions from work items. This would indicate a bug in the service itself.
			Log.LogError(ex, "Unexpected error in background processing task.");
		}
	}

	/// <summary>
	/// Processes work items sequentially (one at a time).
	/// </summary>
	/// <remarks>
	/// Each work item is executed via <c>Task.Run()</c> with <see cref="ExecutionContext.SuppressFlow"/>
	/// to ensure it does not inherit ambient state (e.g., <see cref="Transaction.Current"/>) from the
	/// enqueueing thread.
	/// </remarks>
	private async Task ProcessSequentiallyAsync()
	{
		// Capture shutdown token at the start to avoid race conditions during shutdown.
		// This ensures we don't access mShutdownTokenSource after it's been disposed.
		CancellationToken shutdownToken = mShutdownTokenSource!.Token;

		// ReadAllAsync is fully async - no thread blocking!
		// When the channel is empty, this awaits asynchronously without blocking a thread
		await foreach (QueuedWorkItem queuedItem in mWorkChannel!.Reader.ReadAllAsync(shutdownToken)
			               .ConfigureAwait(false))
		{
			long workItemId = Interlocked.Increment(ref mNextWorkItemId);

			// Execute via Task.Run to ensure ExecutionContext.SuppressFlow() is effective.
			// SuppressFlow only prevents context flow to *newly started* tasks, so we must use Task.Run.
			Task task;
			using (ExecutionContext.SuppressFlow())
			{
				task = Task.Run(
					async () =>
					{
						TrackRunningWorkItemStart(workItemId);
						try
						{
							await ExecuteWorkItemAsync(queuedItem, shutdownToken).ConfigureAwait(false);
						}
						finally
						{
							TrackRunningWorkItemEnd(workItemId);
						}
					},
					CancellationToken.None);
			}

			// Wait for the work item to complete before processing the next one (sequential)
			await task.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Processes work items in parallel with a configurable concurrency limit.
	/// </summary>
	/// <remarks>
	/// During shutdown, if tasks don't complete within the timeout, the semaphore may be disposed
	/// while tasks are still running. The <see cref="SemaphoreSlim.Release()"/> calls in those tasks
	/// are wrapped in try-catch to handle this gracefully.
	/// </remarks>
	private async Task ProcessInParallelAsync()
	{
		// Capture shutdown token at the start to avoid race conditions during shutdown.
		// This ensures we don't access mShutdownTokenSource after it's been disposed.
		CancellationToken shutdownToken = mShutdownTokenSource!.Token;

		var semaphore = new SemaphoreSlim(mMaxConcurrency, mMaxConcurrency);
		var runningTasks = new List<Task>();

		try
		{
			await foreach (QueuedWorkItem queuedItem in mWorkChannel!.Reader.ReadAllAsync(shutdownToken)
				               .ConfigureAwait(false))
			{
				// Wait for a slot to become available (if at max concurrency)
				await semaphore.WaitAsync(shutdownToken).ConfigureAwait(false);

				// Start processing the work item in the background
				// IMPORTANT: Use ExecutionContext.SuppressFlow() to ensure the work item does NOT inherit
				// ambient state (Transaction.Current, HttpContext, etc.) from the enqueueing thread.
				// This prevents background operations from accidentally participating in transactions
				// or depending on request-scoped state.
				long workItemId = Interlocked.Increment(ref mNextWorkItemId);
				Task task;
				using (ExecutionContext.SuppressFlow())
				{
					task = Task.Run(
						async () =>
						{
							TrackRunningWorkItemStart(workItemId);
							try
							{
								await ExecuteWorkItemAsync(queuedItem, shutdownToken).ConfigureAwait(false);
							}
							finally
							{
								TrackRunningWorkItemEnd(workItemId);

								// Safe to call without try-catch: Task.WhenAll(runningTasks) in the finally block
								// guarantees all tasks complete before semaphore.Dispose() is called.
								// ReSharper disable once AccessToDisposedClosure
								semaphore.Release();
							}
						},
						CancellationToken.None);
				}

				runningTasks.Add(task);

				// Periodically clean up completed tasks to avoid memory buildup.
				// Only clean up when the list grows beyond a threshold to avoid O(n) on every iteration.
				if (runningTasks.Count > mMaxConcurrency * ParallelTaskCleanupThresholdMultiplier)
					runningTasks.RemoveAll(t => t.IsCompleted);
			}
		}
		finally
		{
			// Wait for all remaining tasks to complete before disposing semaphore.
			// Tasks never throw because ExecuteWorkItemAsync catches all exceptions internally.
			if (runningTasks.Count > 0)
				await Task.WhenAll(runningTasks).ConfigureAwait(false);

			semaphore.Dispose();
		}
	}

	/// <summary>
	/// Adds the specified work item to the set of currently running work items.
	/// </summary>
	/// <param name="workItemId">The unique identifier of the work item.</param>
	private void TrackRunningWorkItemStart(long workItemId)
	{
		mRunningWorkItems.TryAdd(workItemId, new RunningWorkItemInfo(workItemId, DateTimeOffset.UtcNow));
	}

	/// <summary>
	/// Removes the specified work item from the set of currently running work items.
	/// </summary>
	/// <param name="workItemId">The unique identifier of the work item.</param>
	private void TrackRunningWorkItemEnd(long workItemId)
	{
		mRunningWorkItems.TryRemove(workItemId, out RunningWorkItemInfo _);
	}

	/// <summary>
	/// Discards all work items that are currently queued in the specified channel.
	/// </summary>
	/// <param name="channel">The channel to read and discard queued work items from.</param>
	/// <returns>The number of work items discarded.</returns>
	/// <remarks>
	/// Discarded tracked work items have their completion source cancelled so that callers awaiting
	/// the returned task receive an <see cref="OperationCanceledException"/>.
	/// Fire-and-forget items are simply discarded.
	/// </remarks>
	private static int DiscardQueuedItems(Channel<QueuedWorkItem> channel)
	{
		int discarded = 0;
		while (channel.Reader.TryRead(out QueuedWorkItem queuedItem))
		{
			// Cancel the completion source so callers know the item was discarded (if tracking is enabled)
			queuedItem.CompletionSource?.TrySetCanceled();
			discarded++;
		}
		return discarded;
	}

	/// <summary>
	/// Waits for the specified background task to complete and logs running work items periodically.
	/// </summary>
	/// <param name="backgroundTask">The background task to wait for.</param>
	/// <param name="shutdownTimeoutReached">Indicates whether the configured shutdown timeout has already elapsed.</param>
	/// <returns>A task that completes when <paramref name="backgroundTask"/> completes.</returns>
	private async Task WaitForBackgroundTaskCompletionAsync(Task backgroundTask, bool shutdownTimeoutReached)
	{
		if (shutdownTimeoutReached)
		{
			Log.LogWarning("Shutdown timeout elapsed. Waiting indefinitely for running work items to finish...");
			LogRunningWorkItems(DateTimeOffset.UtcNow);
		}

		// Use Task.WhenAny with a delay task for periodic logging instead of busy-wait polling.
		// This is more efficient as it doesn't wake up every 100ms when the task completes quickly.
		while (!backgroundTask.IsCompleted)
		{
			Task completedTask = await Task.WhenAny(backgroundTask, Task.Delay(sShutdownLoggingInterval))
				                     .ConfigureAwait(false);
			if (completedTask == backgroundTask)
				break;

			// Logging interval elapsed - log running work items
			LogRunningWorkItems(DateTimeOffset.UtcNow);
		}

		// Observe completion/exception.
		await backgroundTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Logs the currently running work items.
	/// </summary>
	/// <param name="nowUtc">The current time (UTC) used to compute the runtime of each work item.</param>
	private void LogRunningWorkItems(DateTimeOffset nowUtc)
	{
		if (mRunningWorkItems.IsEmpty)
		{
			Log.LogDebug("Shutdown is still waiting, but no running work items are tracked.");
			return;
		}

		RunningWorkItemInfo[] snapshot = mRunningWorkItems.Values
			.OrderBy(x => x.StartedAtUtc)
			.ToArray();

		Log.LogWarning("Shutdown is waiting for {RunningItemCount} running work item(s) to complete:", snapshot.Length);

		foreach (RunningWorkItemInfo info in snapshot)
		{
			TimeSpan runtime = nowUtc - info.StartedAtUtc;
			Log.LogWarning("- Work item #{WorkItemId}, running for {WorkItemRuntime}", info.Id, runtime);
		}
	}

	/// <summary>
	/// Executes a single work item with error handling and optionally signals completion to the caller.
	/// </summary>
	/// <param name="queuedItem">The queued work item containing the delegate and optional completion source.</param>
	/// <param name="shutdownToken">The cancellation token that signals shutdown.</param>
	private async Task ExecuteWorkItemAsync(QueuedWorkItem queuedItem, CancellationToken shutdownToken)
	{
		try
		{
			// Execute the work item with the shutdown token
			// This allows long-running work items to be cancelled during shutdown
			await queuedItem.WorkItem(shutdownToken).ConfigureAwait(false);

			// Signal successful completion (only if tracking is enabled)
			queuedItem.CompletionSource?.TrySetResult(null);
		}
		catch (OperationCanceledException ex) when (shutdownToken.IsCancellationRequested)
		{
			// Shutdown was requested - this is expected and logged at Debug level
			Log.LogDebug("Work item cancelled due to shutdown.");
			queuedItem.CompletionSource?.TrySetCanceled(ex.CancellationToken);
		}
		catch (OperationCanceledException ex)
		{
			// Work item threw OperationCanceledException for a reason other than shutdown.
			// This could be a bug in the work item or an intentional cancellation.
			// Log at Warning level since this is unexpected.
			Log.LogWarning(ex, "Work item cancelled (not due to shutdown).");
			queuedItem.CompletionSource?.TrySetCanceled(ex.CancellationToken);
		}
		catch (Exception ex)
		{
			// Log the error but continue processing other items
			// We don't want one failing work item to crash the entire service
			Log.LogError(ex, "Error executing queued work item.");

			// Propagate the exception to the caller via the completion source (if tracking is enabled)
			queuedItem.CompletionSource?.TrySetException(ex);
		}
	}

	#endregion
}
