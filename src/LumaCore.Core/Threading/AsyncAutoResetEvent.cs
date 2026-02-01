// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace LumaCore.Core.Threading;

/// <summary>
/// An async-compatible auto-reset event that enables coordination between async operations.
/// </summary>
/// <remarks>
///     <para>
///     Unlike <see cref="AutoResetEvent"/>, this class supports asynchronous waiting via <see cref="WaitAsync()"/>,
///     making it suitable for async/await patterns without blocking threads.
///     </para>
///     <para>
///     <b>Behavior:</b> When <see cref="Set"/> is called, exactly one waiter is released and the event
///     automatically resets to the non-signaled state. This "auto-reset" behavior differs from manual-reset
///     events that stay signaled until explicitly reset.
///     </para>
///     <para>
///     <b>Thread-safety:</b> All methods are thread-safe and can be called from multiple threads concurrently.
///     </para>
/// </remarks>
/// <example>
///     <b>Typical usage pattern (producer-consumer with single consumer):</b>
///     <code>
///     private readonly AsyncAutoResetEvent mWorkAvailable = new(false);
///     private readonly Queue&lt;WorkItem&gt; mWorkQueue = new();
///     
///     // Producer thread/task
///     void EnqueueWork(WorkItem item)
///     {
///         lock (mWorkQueue)
///         {
///             mWorkQueue.Enqueue(item);
///         }
///         mWorkAvailable.Set();  // Signal that work is available
///     }
///     
///     // Consumer thread/task
///     async Task ProcessWorkAsync(CancellationToken ct)
///     {
///         while (!ct.IsCancellationRequested)
///         {
///             await mWorkAvailable.WaitAsync(ct);  // Wait for signal (auto-resets)
///             
///             WorkItem? item;
///             lock (mWorkQueue)
///             {
///                 mWorkQueue.TryDequeue(out item);
///             }
///             
///             if (item != null)
///                 await ProcessAsync(item);
///         }
///     }
///     </code>
/// </example>
[DebuggerDisplay("IsSet = {" + nameof(GetStateForDebugger) + "}")]
[DebuggerTypeProxy(typeof(DebugView))]
public sealed class AsyncAutoResetEvent
{
	/// <summary>
	/// The queue of <see cref="TaskCompletionSource{TResult}"/> instances that waiters are awaiting.
	/// </summary>
	/// <remarks>
	/// Uses a FIFO queue by default to ensure fair ordering of waiters.
	/// </remarks>
	private readonly IAsyncWaitQueue<object> mQueue;

	/// <summary>
	/// The synchronization lock used to protect access to <see cref="mSet"/> and <see cref="mQueue"/>.
	/// </summary>
	private readonly Lock mLock;

	/// <summary>
	/// The current signaled state of the event.
	/// </summary>
	/// <remarks>
	/// When <see langword="true"/>, the next call to <see cref="WaitAsync()"/> will complete immediately
	/// and reset this to <see langword="false"/>.
	/// </remarks>
	private bool mSet;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncAutoResetEvent"/> class with the specified initial state.
	/// </summary>
	/// <param name="set">
	/// <see langword="true"/> to create an event that is initially set (signaled);
	/// <see langword="false"/> to create an event that is initially reset (non-signaled).
	/// </param>
	/// <remarks>
	/// If <paramref name="set"/> is <see langword="true"/>, the first call to <see cref="WaitAsync()"/>
	/// will complete immediately and auto-reset the event.
	/// </remarks>
	public AsyncAutoResetEvent(bool set)
		: this(set, null) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncAutoResetEvent"/> class
	/// with the event initially in the reset (non-signaled) state.
	/// </summary>
	/// <remarks>
	/// This is equivalent to calling <c>new AsyncAutoResetEvent(false)</c>.
	/// </remarks>
	public AsyncAutoResetEvent()
		: this(false, null) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncAutoResetEvent"/> class with a custom wait queue.
	/// </summary>
	/// <param name="set">
	/// <see langword="true"/> to create an event that is initially set (signaled);
	/// <see langword="false"/> to create an event that is initially reset (non-signaled).
	/// </param>
	/// <param name="queue">
	/// The wait queue used to manage waiters, or <see langword="null"/> to use the default FIFO queue.
	/// </param>
	/// <remarks>
	/// This constructor is internal to allow testing with custom queue implementations.
	/// </remarks>
	internal AsyncAutoResetEvent(bool set, IAsyncWaitQueue<object>? queue)
	{
		mQueue = queue ?? new DefaultAsyncWaitQueue<object>();
		mSet = set;
		mLock = new Lock();
	}

	/// <summary>
	/// Gets a value indicating whether the event is currently in the set (signaled) state.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the event is set; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	///     <para>
	///     <b>⚠️ Warning:</b> This property is rarely useful in production code due to inherent race conditions.
	///     The state may change immediately after reading this property, making decisions based on it unreliable.
	///     </para>
	///     <para>
	///     <b>Prefer:</b> Use <see cref="WaitAsync()"/> to react to state changes rather than polling this property.
	///     </para>
	/// </remarks>
	public bool IsSet
	{
		get
		{
			lock (mLock) return mSet;
		}
	}

	/// <summary>
	/// Gets the signaled state for debugger display purposes.
	/// </summary>
	[DebuggerNonUserCode]
	[ExcludeFromCodeCoverage]
	private bool GetStateForDebugger => mSet;

	/// <summary>
	/// Asynchronously waits for the event to be set.
	/// </summary>
	/// <returns>
	/// A task that completes when the event is signaled. If the event is already set,
	/// returns a completed task immediately and auto-resets the event.
	/// </returns>
	/// <remarks>
	///     <para>
	///     Unlike <see cref="AsyncManualResetEvent.WaitAsync()"/>, only one waiter is released per
	///     <see cref="Set"/> call, and the event automatically resets.
	///     </para>
	///     <para>
	///     <b>Fairness:</b> Waiters are released in FIFO order by default.
	///     </para>
	/// </remarks>
	public Task WaitAsync()
	{
		return WaitAsync(CancellationToken.None);
	}

	/// <summary>
	/// Asynchronously waits for the event to be set, with cancellation support.
	/// </summary>
	/// <param name="cancellationToken">
	/// A cancellation token that can be used to cancel the wait operation.
	/// </param>
	/// <returns>
	/// A task that completes when the event is signaled or when <paramref name="cancellationToken"/> is canceled.
	/// </returns>
	/// <exception cref="OperationCanceledException">
	/// Thrown if <paramref name="cancellationToken"/> is canceled before the event is signaled.
	/// </exception>
	/// <remarks>
	///     <para>
	///     If the event is already set when this method is called, it completes immediately and auto-resets,
	///     even if <paramref name="cancellationToken"/> is already canceled.
	///     </para>
	///     <para>
	///     If the wait is canceled, the event is <b>not</b> auto-reset, preserving the signal for
	///     another waiter.
	///     </para>
	///     <para>
	///     <b>Optimization:</b> If <paramref name="cancellationToken"/> cannot be canceled
	///     (<see cref="CancellationToken.CanBeCanceled"/> is <see langword="false"/>),
	///     no cancellation registration overhead is incurred.
	///     </para>
	/// </remarks>
	public Task WaitAsync(CancellationToken cancellationToken)
	{
		Task<object> task;
		lock (mLock)
		{
			if (mSet)
			{
				mSet = false;
				return Task.CompletedTask;
			}

			task = mQueue.Enqueue();
		}

		// Register cancellation callback outside the lock to avoid deadlocks
		if (cancellationToken.CanBeCanceled)
		{
			return WaitWithCancellationAsync(task, cancellationToken);
		}

		return task;
	}

	/// <summary>
	/// Synchronously waits for the event to be set.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>⚠️ Warning:</b> This method blocks the calling thread and should be avoided in async code.
	///     Prefer <see cref="WaitAsync()"/> in async methods.
	///     </para>
	///     <para>
	///     Blocking on async code can cause deadlocks in single-threaded synchronization contexts
	///     (e.g., UI threads, legacy ASP.NET, Blazor WebAssembly).
	///     </para>
	/// </remarks>
	public void Wait()
	{
		Wait(CancellationToken.None);
	}

	/// <summary>
	/// Synchronously waits for the event to be set, with cancellation support.
	/// </summary>
	/// <param name="cancellationToken">
	/// A cancellation token that can be used to cancel the wait operation.
	/// </param>
	/// <exception cref="OperationCanceledException">
	/// Thrown if <paramref name="cancellationToken"/> is canceled before the event is signaled.
	/// </exception>
	/// <remarks>
	///     <para>
	///     If the event is already set when this method is called, it returns immediately and auto-resets,
	///     even if <paramref name="cancellationToken"/> is already canceled.
	///     </para>
	///     <para>
	///     <b>⚠️ Warning:</b> This method blocks the calling thread and should be avoided in async code.
	///     Prefer <see cref="WaitAsync(CancellationToken)"/> in async methods.
	///     </para>
	/// </remarks>
	public void Wait(CancellationToken cancellationToken)
	{
		WaitAsync(cancellationToken).WaitAndUnwrapException(cancellationToken);
	}

	/// <summary>
	/// Sets the event, releasing exactly one waiting task.
	/// </summary>
	/// <remarks>
	///     <para>
	///     If there are waiters in the queue, exactly one waiter is released (in FIFO order)
	///     and the event remains in the non-signaled state.
	///     </para>
	///     <para>
	///     If there are no waiters, the event transitions to the signaled state. The next call to
	///     <see cref="WaitAsync()"/> will complete immediately and auto-reset the event.
	///     </para>
	///     <para>
	///     <b>Multiple calls:</b> Calling <see cref="Set"/> multiple times when there are no waiters
	///     does not "stack" signals—the event simply remains in the signaled state.
	///     </para>
	/// </remarks>
	public void Set()
	{
		lock (mLock)
		{
			if (mQueue.IsEmpty)
			{
				mSet = true;
			}
			else
			{
				mQueue.Dequeue();
			}
		}
	}

	/// <summary>
	/// Waits for the enqueued task to complete while supporting cancellation.
	/// </summary>
	/// <param name="task">The task representing the wait operation.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>A task that completes when the original task completes or cancellation is requested.</returns>
	/// <remarks>
	/// The cancellation callback removes the waiter from the queue if possible, ensuring that
	/// a canceled wait does not consume a signal intended for another waiter.
	/// </remarks>
	private async Task WaitWithCancellationAsync(Task<object> task, CancellationToken cancellationToken)
	{
		await using CancellationTokenRegistration registration = cancellationToken.Register(
			() =>
			{
				lock (mLock)
				{
					mQueue.TryCancel(task, cancellationToken);
				}
			},
			useSynchronizationContext: false);

		await task.ConfigureAwait(false);
	}

	/// <summary>
	/// Provides a debugger-friendly view of the event state.
	/// </summary>
	[DebuggerNonUserCode]
	[ExcludeFromCodeCoverage]
	private sealed class DebugView(AsyncAutoResetEvent are)
	{
		/// <summary>
		/// Gets a value indicating whether the event is currently set.
		/// </summary>
		public bool IsSet => are.mSet;

		/// <summary>
		/// Gets the wait queue for inspecting pending waiters.
		/// </summary>
		public IAsyncWaitQueue<object> WaitQueue => are.mQueue;

		/// <summary>
		/// Gets the hash code of the event instance for identification.
		/// </summary>
		public int HashCode => are.GetHashCode();
	}
}
