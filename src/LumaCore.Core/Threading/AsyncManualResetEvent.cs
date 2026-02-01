// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace LumaCore.Core.Threading;

/// <summary>
/// An async-compatible manual-reset event that enables coordination between async operations.
/// </summary>
/// <remarks>
///     <para>
///     Unlike <see cref="ManualResetEvent"/>, this class supports asynchronous waiting via <see cref="WaitAsync()"/>,
///     making it suitable for async/await patterns without blocking threads.
///     </para>
///     <para>
///     <b>Behavior:</b> When <see cref="Set"/> is called, all current and future waiters complete immediately
///     until <see cref="Reset"/> is called. This "manual reset" behavior differs from auto-reset events that
///     automatically reset after releasing one waiter.
///     </para>
///     <para>
///     <b>Thread-safety:</b> All methods are thread-safe and can be called from multiple threads concurrently.
///     </para>
/// </remarks>
/// <example>
///     <b>Typical usage pattern:</b>
///     <code>
///     private readonly AsyncManualResetEvent mDataReady = new(false);
///     
///     // Producer thread/task
///     async Task ProduceDataAsync()
///     {
///         await LoadDataAsync();
///         mDataReady.Set();  // Signal that data is ready
///     }
///     
///     // Consumer thread/task
///     async Task ConsumeDataAsync()
///     {
///         await mDataReady.WaitAsync();  // Wait for signal
///         ProcessData();
///     }
///     </code>
/// </example>
[DebuggerDisplay("IsSet = {" + nameof(GetStateForDebugger) + "}")]
[DebuggerTypeProxy(typeof(DebugView))]
public sealed class AsyncManualResetEvent
{
	/// <summary>
	/// The synchronization lock used to protect access to <see cref="mTcs"/>.
	/// </summary>
	private readonly Lock mLock = new();

	/// <summary>
	/// The current state of the event, represented as a task that completes when the event is set.
	/// </summary>
	/// <remarks>
	/// When the event is set, this task is completed. When <see cref="Reset"/> is called,
	/// this is replaced with a new incomplete task. The <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>
	/// option prevents synchronous continuations from running on the thread that calls <see cref="Set"/>.
	/// </remarks>
	private TaskCompletionSource<object?> mTcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<object?>();

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncManualResetEvent"/> class with the specified initial state.
	/// </summary>
	/// <param name="set">
	/// <see langword="true"/> to create an event that is initially set (signaled);
	/// <see langword="false"/> to create an event that is initially reset (non-signaled).
	/// </param>
	/// <remarks>
	/// If <paramref name="set"/> is <see langword="true"/>, any immediate call to <see cref="WaitAsync()"/>
	/// will return a completed task.
	/// </remarks>
	public AsyncManualResetEvent(bool set)
	{
		if (set)
			mTcs.TrySetResult(null);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncManualResetEvent"/> class
	/// with the event initially in the reset (non-signaled) state.
	/// </summary>
	/// <remarks>
	/// This is equivalent to calling <c>new AsyncManualResetEvent(false)</c>.
	/// </remarks>
	public AsyncManualResetEvent()
		: this(false) { }

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
			lock (mLock)
			{
				return mTcs.Task.IsCompleted;
			}
		}
	}

	/// <summary>
	/// Gets a value indicating whether the underlying task has completed (for debugger display).
	/// </summary>
	/// <remarks>
	/// No need for synchronization as <see cref="mTcs"/> is always initialized and replaced atomically.
	/// </remarks>
	[DebuggerNonUserCode]
	[ExcludeFromCodeCoverage]
	// ReSharper disable once InconsistentlySynchronizedField
	private bool GetStateForDebugger => mTcs.Task.IsCompleted;

	/// <summary>
	/// Asynchronously waits for the event to be set.
	/// </summary>
	/// <returns>
	/// A task that completes when the event is set. If the event is already set, returns a completed task immediately.
	/// </returns>
	/// <remarks>
	///     <para>
	///     Multiple tasks can await the same event simultaneously. When <see cref="Set"/> is called,
	///     all waiting tasks complete.
	///     </para>
	///     <para>
	///     <b>Performance note:</b> Returned tasks are cached when the event is set, avoiding allocations
	///     for subsequent waiters until <see cref="Reset"/> is called.
	///     </para>
	/// </remarks>
	public Task WaitAsync()
	{
		lock (mLock)
		{
			return mTcs.Task;
		}
	}

	/// <summary>
	/// Asynchronously waits for the event to be set, with cancellation support.
	/// </summary>
	/// <param name="cancellationToken">
	/// A cancellation token that can be used to cancel the wait operation.
	/// </param>
	/// <returns>
	/// A task that completes when the event is set or when <paramref name="cancellationToken"/> is canceled.
	/// </returns>
	/// <exception cref="OperationCanceledException">
	/// Thrown if <paramref name="cancellationToken"/> is canceled before the event is set.
	/// </exception>
	/// <remarks>
	///     <para>
	///     If the event is already set when this method is called, the cancellation token is ignored
	///     and a completed task is returned immediately.
	///     </para>
	///     <para>
	///     <b>Optimization:</b> If <paramref name="cancellationToken"/> cannot be canceled
	///     (<see cref="CancellationToken.CanBeCanceled"/> is <see langword="false"/>),
	///     this method behaves identically to <see cref="WaitAsync()"/>.
	///     </para>
	/// </remarks>
	public Task WaitAsync(CancellationToken cancellationToken)
	{
		Task waitTask;
		lock (mLock)
		{
			waitTask = mTcs.Task;
		}

		return waitTask.IsCompleted ? waitTask : waitTask.WaitAsync(cancellationToken);
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
	///     (e.g., UI threads, legacy ASP.NET).
	///     </para>
	/// </remarks>
	public void Wait()
	{
		WaitAsync().WaitAndUnwrapException();
	}

	/// <summary>
	/// Synchronously waits for the event to be set, with cancellation support.
	/// </summary>
	/// <param name="cancellationToken">
	/// A cancellation token that can be used to cancel the wait operation.
	/// </param>
	/// <exception cref="OperationCanceledException">
	/// Thrown if <paramref name="cancellationToken"/> is canceled before the event is set.
	/// </exception>
	/// <remarks>
	///     <para>
	///     <b>⚠️ Warning:</b> This method blocks the calling thread. Prefer <see cref="WaitAsync(CancellationToken)"/>
	///     in async methods.
	///     </para>
	///     <para>
	///     If the event is already set, this method returns immediately without observing the cancellation token.
	///     </para>
	/// </remarks>
	public void Wait(CancellationToken cancellationToken)
	{
		Task waitTask;
		lock (mLock)
		{
			waitTask = mTcs.Task;
		}

		if (waitTask.IsCompleted)
			return;

		waitTask.WaitAndUnwrapException(cancellationToken);
	}

	/// <summary>
	/// Sets the event to the signaled state, completing all current and future waiters.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This method is idempotent: calling it multiple times has no additional effect until
	///     <see cref="Reset"/> is called.
	///     </para>
	///     <para>
	///     All tasks returned by <see cref="WaitAsync()"/> (both current and future) will complete
	///     immediately until <see cref="Reset"/> is called.
	///     </para>
	///     <para>
	///     <b>Thread-safety:</b> This method is thread-safe and can be called concurrently with
	///     <see cref="WaitAsync()"/> and <see cref="Reset"/>.
	///     </para>
	/// </remarks>
	public void Set()
	{
		lock (mLock)
		{
			mTcs.TrySetResult(null);
		}
	}

	/// <summary>
	/// Resets the event to the non-signaled state.
	/// </summary>
	/// <remarks>
	///     <para>
	///     After calling this method, subsequent calls to <see cref="WaitAsync()"/> will return incomplete tasks
	///     that complete only when <see cref="Set"/> is called.
	///     </para>
	///     <para>
	///     This method is idempotent: calling it multiple times on an already-reset event has no additional effect.
	///     </para>
	///     <para>
	///     <b>Important:</b> Tasks returned by <see cref="WaitAsync()"/> <em>before</em> calling <see cref="Reset"/>
	///     remain completed. Only new waiters will block.
	///     </para>
	/// </remarks>
	public void Reset()
	{
		lock (mLock)
		{
			if (mTcs.Task.IsCompleted)
				mTcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<object?>();
		}
	}

	/// <summary>
	/// Debugger type proxy for <see cref="AsyncManualResetEvent"/> that provides a more readable view in the debugger.
	/// </summary>
	[DebuggerNonUserCode]
	[ExcludeFromCodeCoverage]
	private sealed class DebugView(AsyncManualResetEvent manualResetEvent)
	{
		/// <summary>
		/// Gets a value indicating whether the event is currently set.
		/// </summary>
		public bool IsSet => manualResetEvent.GetStateForDebugger;

		/// <summary>
		/// Gets the current underlying task that represents the event state.
		/// </summary>
		public Task CurrentTask => manualResetEvent.mTcs.Task;

		/// <summary>
		/// Gets the hash code of the event instance for identification in the debugger.
		/// </summary>
		public int HashCode => manualResetEvent.GetHashCode();
	}
}
