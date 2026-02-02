// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

using LumaCore.Core.Threading;

namespace LumaCore.Core;

/// <summary>
/// Encapsulates the mutable lifecycle state of an object (or a hierarchy of nested objects) that follows the
/// <see cref="LifecycleManagement"/> pattern.
/// </summary>
/// <remarks>
///     <para>
///     This class tracks five distinct lifecycle phases:
///     </para>
///     <list type="number">
///         <item>
///             <description><b>Uninitialized</b> – object is constructed but not yet ready for use.</description>
///         </item>
///         <item>
///             <description><b>Initializing</b> – one-time setup is in progress.</description>
///         </item>
///         <item>
///             <description><b>Initialized</b> – object is fully operational and can accept work.</description>
///         </item>
///         <item>
///             <description><b>Shutting down</b> – graceful shutdown has been requested; new work should be rejected.</description>
///         </item>
///         <item>
///             <description><b>Disposing / Disposed</b> – resources are being (or have been) released.</description>
///         </item>
///     </list>
///     <para>
///     <b>Thread-Safety:</b> All property accesses and method calls require the caller to hold the monitor lock on
///     <see cref="Sync"/>.
///     </para>
///     <para>
///     The class also provides a <see cref="ShutdownToken"/> that long-running operations can observe to terminate
///     gracefully, as well as a pending-operation counter that delays disposal until all in-flight work completes.
///     </para>
/// </remarks>
public sealed class LifecycleState : IDisposable
{
	private CancellationTokenSource mShutdownCancellationTokenSource = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="LifecycleState"/> class with the specified synchronization object.
	/// </summary>
	/// <param name="sync">
	/// The object that will be used as a monitor lock (via <see cref="Monitor.Enter(object)"/> /
	/// <see cref="Monitor.Exit(object)"/>) to synchronize all state transitions.
	/// This must be the same object used by the owning <see cref="LifecycleManagement"/> instance.
	/// </param>
	/// <remarks>
	/// The constructor immediately captures a <see cref="CancellationToken"/> from a newly created
	/// <see cref="CancellationTokenSource"/> and exposes it via <see cref="ShutdownToken"/>.
	/// </remarks>
	internal LifecycleState(object sync)
	{
		Sync = sync;
		ShutdownToken = mShutdownCancellationTokenSource.Token;
	}

	#region Synchronization

	/// <summary>
	/// Gets the synchronization root used to guard all mutable state in this instance.
	/// </summary>
	/// <value>
	/// An object that must be locked via <c>lock (Sync)</c> or <see cref="Monitor.Enter(object)"/> before
	/// accessing or modifying any property or calling any method on this instance.
	/// </value>
	/// <remarks>
	/// Callers are responsible for acquiring and releasing the lock. Debug builds assert that the lock is held
	/// whenever a guarded member is accessed.
	/// </remarks>
	public object Sync { get; }

	#endregion

	#region Shutdown Signaling

	/// <summary>
	/// Gets a <see cref="CancellationToken"/> that is canceled when a graceful shutdown is requested.
	/// </summary>
	/// <value>
	/// A token that transitions to the canceled state when <see cref="SignalShutdown"/> is invoked.
	/// The token is replaced with a fresh, non-canceled token after <see cref="ResetShutdownSignal"/> is called.
	/// </value>
	/// <remarks>
	///     <para>
	///     Long-running or background operations should pass this token to async APIs
	///     (e.g., <see cref="Task.Delay(int, CancellationToken)"/>) and periodically check
	///     <see cref="CancellationToken.IsCancellationRequested"/> to terminate gracefully.
	///     </para>
	///     <para>
	///     Because the property may be reassigned after a reset, callers that cache the token should re-read
	///     the property after re-initialization.
	///     </para>
	/// </remarks>
	public CancellationToken ShutdownToken { get; private set; }

	/// <summary>
	/// Cancels the <see cref="ShutdownToken"/>, signaling all observers that a graceful shutdown has been requested.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This method is idempotent: calling it multiple times has no additional effect once the token is canceled.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/>.
	///     </para>
	/// </remarks>
	internal void SignalShutdown()
	{
		Debug.Assert(Monitor.IsEntered(Sync));
		if (!mShutdownCancellationTokenSource.IsCancellationRequested)
		{
			mShutdownCancellationTokenSource.Cancel();
		}
	}

	/// <summary>
	/// Disposes the current <see cref="CancellationTokenSource"/> and creates a fresh one, thereby
	/// providing a new, non-canceled <see cref="ShutdownToken"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Call this method when the object is being re-initialized after a previous shutdown cycle.
	///     If the token has not been canceled, this method is a no-op.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/>.
	///     </para>
	/// </remarks>
	internal void ResetShutdownSignal()
	{
		Debug.Assert(Monitor.IsEntered(Sync));
		if (mShutdownCancellationTokenSource.IsCancellationRequested)
		{
			mShutdownCancellationTokenSource.Dispose();
			mShutdownCancellationTokenSource = new CancellationTokenSource();
			ShutdownToken = mShutdownCancellationTokenSource.Token;
		}
	}

	/// <summary>
	/// Releases all resources used by the <see cref="LifecycleState"/> instance.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This method disposes the internal <see cref="CancellationTokenSource"/>. After disposal, the
	///     <see cref="ShutdownToken"/> should no longer be used.
	///     </para>
	///     <para>
	///     The method does <b>not</b> require a lock because it is typically called after all operations have
	///     completed and no further concurrent access is expected.
	///     </para>
	/// </remarks>
	public void Dispose()
	{
		mShutdownCancellationTokenSource.Dispose();
	}

	#endregion

	#region Object State

	private bool mIsInitializing;
	private bool mIsInitialized;
	private bool mIsShuttingDown;
	private bool mIsDisposing;
	private bool mIsDisposed;

	/// <summary>
	/// Gets or sets a value indicating whether the object is currently in the process of initializing.
	/// </summary>
	/// <value>
	/// <see langword="true"/> while one-time initialization logic is executing; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/> when reading or writing this property.
	/// </remarks>
	public bool IsInitializing
	{
		get
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			return mIsInitializing;
		}
		internal set
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			mIsInitializing = value;
		}
	}

	/// <summary>
	/// Gets or sets a value indicating whether the object has completed initialization and is ready for use.
	/// </summary>
	/// <value>
	/// <see langword="true"/> after initialization has completed successfully; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	///     <para>
	///     When <see langword="true"/>, the object is fully operational and may accept work.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/> when reading or writing this
	///     property.
	///     </para>
	/// </remarks>
	public bool IsInitialized
	{
		get
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			return mIsInitialized;
		}
		internal set
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			mIsInitialized = value;
		}
	}

	/// <summary>
	/// Gets or sets a value indicating whether a graceful shutdown is in progress.
	/// </summary>
	/// <value>
	/// <see langword="true"/> after shutdown has been initiated but before disposal; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	///     <para>
	///     During shutdown, new work should be rejected, but in-flight operations may continue until they observe
	///     <see cref="ShutdownToken"/> or complete naturally.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/> when reading or writing this
	///     property.
	///     </para>
	/// </remarks>
	public bool IsShuttingDown
	{
		get
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			return mIsShuttingDown;
		}
		internal set
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			mIsShuttingDown = value;
		}
	}

	/// <summary>
	/// Gets or sets a value indicating whether the object is currently releasing its resources.
	/// </summary>
	/// <value>
	/// <see langword="true"/> while <see cref="IDisposable.Dispose"/> or <see cref="IAsyncDisposable.DisposeAsync"/>
	/// is executing; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	///     <para>
	///     Once this flag is set, no new operations should be started, and any guards (e.g.,
	///     <see cref="EnsureNotDisposingOrDisposed"/>) will throw.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/> when reading or writing this
	///     property.
	///     </para>
	/// </remarks>
	public bool IsDisposing
	{
		get
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			return mIsDisposing;
		}
		internal set
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			mIsDisposing = value;
		}
	}

	/// <summary>
	/// Gets or sets a value indicating whether the object has been fully disposed.
	/// </summary>
	/// <value>
	/// <see langword="true"/> after disposal has completed; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	///     <para>
	///     A disposed object must not be used. Attempts to perform operations will result in
	///     <see cref="ObjectDisposedException"/>.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/> when reading or writing this
	///     property.
	///     </para>
	/// </remarks>
	public bool IsDisposed
	{
		get
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			return mIsDisposed;
		}
		internal set
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			mIsDisposed = value;
		}
	}

	#endregion

	#region Tracking Pending Operations

	private int mPendingOperationCount;

	/// <summary>
	/// An asynchronous event that signals when all pending operations have completed.
	/// </summary>
	/// <value>
	/// An <see cref="AsyncManualResetEvent"/> that is in the <b>set</b> (signaled) state when
	/// <see cref="PendingOperationCount"/> equals zero, and in the <b>reset</b> state otherwise.
	/// </value>
	/// <remarks>
	///     <para>
	///     The <see cref="LifecycleManagement.DisposeAsync"/> method awaits this event before releasing resources,
	///     ensuring that all in-flight work has completed.
	///     </para>
	///     <para>
	///     The event is automatically managed by <see cref="IncrementPendingOperationsCount"/> and
	///     <see cref="DecrementPendingOperationsCount"/>.
	///     </para>
	/// </remarks>
	public readonly AsyncManualResetEvent NoPendingOperationsLeftEvent = new(true);

	/// <summary>
	/// An asynchronous event that signals when exactly one pending operation remains.
	/// </summary>
	/// <value>
	/// An <see cref="AsyncManualResetEvent"/> that is in the <b>set</b> (signaled) state when
	/// <see cref="PendingOperationCount"/> equals one, and in the <b>reset</b> state otherwise.
	/// </value>
	/// <remarks>
	///     <para>
	///     This event is used by <see cref="LifecycleManagement.ShutdownAsync"/> to wait for all operations
	///     except itself (the shutdown operation) to complete. Since the shutdown operation increments the
	///     pending count before waiting, it waits for count == 1 instead of count == 0.
	///     </para>
	///     <para>
	///     The event is automatically managed by <see cref="IncrementPendingOperationsCount"/> and
	///     <see cref="DecrementPendingOperationsCount"/>.
	///     </para>
	/// </remarks>
	internal readonly AsyncManualResetEvent OnlyOneOperationPendingEvent = new(false);

	/// <summary>
	/// Gets the current number of pending (in-flight) operations.
	/// </summary>
	/// <value>
	/// A non-negative integer representing the number of operations that have started but not yet completed.
	/// </value>
	/// <remarks>
	///     <para>
	///     While this value is greater than zero, disposal will be delayed to allow running operations to finish.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/> when reading this property.
	///     </para>
	/// </remarks>
	public int PendingOperationCount
	{
		get
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			return mPendingOperationCount;
		}
	}

	/// <summary>
	/// Increments the pending operation counter, indicating that a new operation has started.
	/// </summary>
	/// <remarks>
	///     <para>
	///     When the counter transitions from 0 to 1, <see cref="NoPendingOperationsLeftEvent"/> is reset,
	///     blocking any disposal waiting on that event.
	///     </para>
	///     <para>
	///     When the counter becomes exactly 1, <see cref="OnlyOneOperationPendingEvent"/> is set.
	///     When the counter becomes 2 or more, <see cref="OnlyOneOperationPendingEvent"/> is reset.
	///     </para>
	///     <para>
	///     Each call to this method must eventually be paired with a call to <see cref="DecrementPendingOperationsCount"/>
	///     to avoid leaking the counter and preventing disposal.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/>.
	///     </para>
	/// </remarks>
	public void IncrementPendingOperationsCount()
	{
		Debug.Assert(Monitor.IsEntered(Sync));
		int oldCount = mPendingOperationCount++;
		int newCount = mPendingOperationCount;

		if (oldCount == 0)
			NoPendingOperationsLeftEvent.Reset();

		// Manage the "only one pending" event for shutdown waiting
		if (newCount == 1)
			OnlyOneOperationPendingEvent.Set();
		else if (newCount == 2)
			OnlyOneOperationPendingEvent.Reset();
	}

	/// <summary>
	/// Decrements the pending operation counter, indicating that an operation has finished.
	/// </summary>
	/// <remarks>
	///     <para>
	///     When the counter transitions from 1 to 0, <see cref="NoPendingOperationsLeftEvent"/> is set,
	///     unblocking any disposal waiting on that event.
	///     </para>
	///     <para>
	///     When the counter transitions to 1, <see cref="OnlyOneOperationPendingEvent"/> is set,
	///     unblocking any shutdown operation waiting for other operations to complete.
	///     </para>
	///     <para>
	///     <b>Debug assertion:</b> The counter must not drop below zero; a debug assertion verifies this.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/>.
	///     </para>
	/// </remarks>
	public void DecrementPendingOperationsCount()
	{
		Debug.Assert(Monitor.IsEntered(Sync));
		mPendingOperationCount--;
		Debug.Assert(mPendingOperationCount >= 0);

		if (mPendingOperationCount == 0)
			NoPendingOperationsLeftEvent.Set();

		if (mPendingOperationCount == 1)
			OnlyOneOperationPendingEvent.Set();
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Throws an <see cref="ObjectDisposedException"/> if the object is currently disposing or has already been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException">
	/// Thrown when <see cref="IsDisposing"/> or <see cref="IsDisposed"/> is <see langword="true"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     Use this guard at the start of public operations to reject calls on a disposed object.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/>.
	///     </para>
	/// </remarks>
	public void EnsureNotDisposingOrDisposed()
	{
		Debug.Assert(Monitor.IsEntered(Sync));
		if (mIsDisposed || mIsDisposing)
			throw new ObjectDisposedException(GetType().FullName, "The object is disposing or already disposed.");
	}

	/// <summary>
	/// Throws an exception if the object is disposed, disposing, or not yet initialized.
	/// </summary>
	/// <exception cref="ObjectDisposedException">
	/// Thrown when <see cref="IsDisposing"/> or <see cref="IsDisposed"/> is <see langword="true"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="IsInitialized"/> is <see langword="false"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     Use this guard at the start of operations that require a fully initialized object.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/>.
	///     </para>
	/// </remarks>
	public void EnsureIsInitialized()
	{
		Debug.Assert(Monitor.IsEntered(Sync));
		if (mIsDisposed || mIsDisposing)
			throw new ObjectDisposedException(GetType().FullName, "The object is disposing or already disposed.");
		if (!mIsInitialized)
			throw new InvalidOperationException("The object is not initialized.");
	}

	/// <summary>
	/// Throws an exception if the object's configuration is locked (i.e., it is no longer in the uninitialized state).
	/// </summary>
	/// <exception cref="ObjectDisposedException">
	/// Thrown when <see cref="IsDisposing"/> or <see cref="IsDisposed"/> is <see langword="true"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the object is in any of the following states:
	/// <list type="bullet">
	///     <item>
	///         <description><see cref="IsInitializing"/> is <see langword="true"/>.</description>
	///     </item>
	///     <item>
	///         <description><see cref="IsInitialized"/> is <see langword="true"/>.</description>
	///     </item>
	///     <item>
	///         <description><see cref="IsShuttingDown"/> is <see langword="true"/>.</description>
	///     </item>
	/// </list>
	/// </exception>
	/// <remarks>
	///     <para>
	///     Use this guard when modifying configuration properties that must be set before initialization begins.
	///     </para>
	///     <para>
	///     <b>Caller requirement:</b> The caller must hold the lock on <see cref="Sync"/>.
	///     </para>
	/// </remarks>
	public void EnsureCanChangeConfiguration()
	{
		Debug.Assert(Monitor.IsEntered(Sync));
		if (mIsDisposed || mIsDisposing)
			throw new ObjectDisposedException(GetType().FullName, "The object is disposing or already disposed.");
		if (mIsInitializing || mIsInitialized)
			throw new InvalidOperationException("The object is initializing or already initialized.");
		if (mIsShuttingDown)
			throw new InvalidOperationException("The object is shutting down, but has not finished, yet.");
	}

	#endregion
}
