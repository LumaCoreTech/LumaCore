// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

using LumaCore.Core.Threading;

using Microsoft.Extensions.Logging;

namespace LumaCore.Core;

/// <summary>
/// Base class for objects requiring robust lifecycle control with initialization, shutdown, and disposal phases.
/// </summary>
/// <remarks>
///     <para>
///     This class provides a thread-safe, async-first lifecycle management pattern with five distinct phases:
///     </para>
///     <list type="number">
///         <item>
///             <description><b>Uninitialized</b> – Constructed but not yet ready for use.</description>
///         </item>
///         <item>
///             <description><b>Initializing</b> – One-time setup is in progress.</description>
///         </item>
///         <item>
///             <description><b>Initialized</b> – Fully operational and accepting work.</description>
///         </item>
///         <item>
///             <description><b>Shutting Down</b> – Graceful shutdown in progress; new work is rejected.</description>
///         </item>
///         <item>
///             <description><b>Disposed</b> – Resources have been released; object is no longer usable.</description>
///         </item>
///     </list>
///     <para>
///         <b>Key Features:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>Initialize-once pattern with support for re-initialization after shutdown</description>
///         </item>
///         <item>
///             <description>Graceful shutdown that waits for pending operations to complete</description>
///         </item>
///         <item>
///             <description>
///             Operation tracking via <see cref="BeginOperation"/> and <see cref="BeginAsyncOperation"/> to
///             prevent disposal during active work
///             </description>
///         </item>
///         <item>
///             <description>
///             Cooperative cancellation via <see cref="LifecycleState.ShutdownToken"/> for long-running
///             operations
///             </description>
///         </item>
///         <item>
///             <description>Integration with <see cref="FailFast"/> for critical error handling</description>
///         </item>
///     </list>
///     <para>
///         <b>Usage Pattern:</b>
///     </para>
///     <code>
///     public class MyService : LifecycleManagement
///     {
///         public MyService(ILoggerFactory loggerFactory) : base(loggerFactory) { }
///         
///         // Public method to initialize the service
///         public Task InitializeAsync(CancellationToken ct = default)
///         {
///             return InitializeAsync(new LifecycleContext(), ct);
///         }
///         
///         protected override async Task OnInitializingAsync(ILifecycleContext context, CancellationToken ct)
///         {
///             // Initialize resources (e.g., connect to database, load configuration)
///         }
///         
///         public async Task DoWorkAsync()
///         {
///             using var scope = BeginAsyncOperation();
///             // Perform work - disposal is prevented while scope is active
///         }
///         
///         // Public method to gracefully shut down the service
///         public Task ShutdownAsync()
///         {
///             return ShutdownAsync(new LifecycleContext());
///         }
///         
///         protected override Task OnShuttingDownAsync(ILifecycleContext context)
///         {
///             // Clean up resources (e.g., close connections, flush buffers)
///             return Task.CompletedTask;
///         }
///         
///         protected override Task OnDisposingAsync(ILifecycleContext context)
///         {
///             // Release unmanaged resources if any
///             return Task.CompletedTask;
///         }
///         
///         public async ValueTask DisposeAsync()
///         {
///             await DisposeAsync(new LifecycleContext());
///             GC.SuppressFinalize(this);
///         }
///     }
///     
///     // Usage:
///     var service = new MyService(loggerFactory);
///     await service.InitializeAsync();
///     await service.DoWorkAsync();
///     await service.ShutdownAsync();  // Optional - DisposeAsync calls this if needed
///     await service.DisposeAsync();
///     </code>
/// </remarks>
public abstract partial class LifecycleManagement :
	IDisposable,
	IAsyncDisposable
{
	#region Synchronization

	/// <summary>
	/// Gets the <see cref="Lock"/> instance used to synchronize access to members.
	/// </summary>
	/// <remarks>
	/// Derived classes may expose this to external code if composable locking is needed, but should do so deliberately
	/// by providing their own public property.
	/// </remarks>
	protected Lock Sync { get; }

	#endregion

	#region Logging

	/// <summary>
	/// Gets the logger that can be used to write log messages associated with the object.
	/// </summary>
	protected ILogger Log { get; }

	#endregion

	#region Construction

	/// <summary>
	/// Initializes a new instance of the <see cref="LifecycleManagement"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory used to create a logger for this instance.</param>
	/// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is <see langword="null"/>.</exception>
	protected LifecycleManagement(ILoggerFactory loggerFactory)
	{
		ArgumentNullException.ThrowIfNull(loggerFactory);
		Sync = new Lock();
		Log = loggerFactory.CreateLogger(GetType());
		LifecycleState = new LifecycleState(Sync, GetType());
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LifecycleManagement"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory used to create a logger for this instance.</param>
	/// <param name="sync">The <see cref="Lock"/> instance used to synchronize access to members.</param>
	/// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="sync"/> is <see langword="null"/>.</exception>
	protected LifecycleManagement(ILoggerFactory loggerFactory, Lock sync)
	{
		ArgumentNullException.ThrowIfNull(loggerFactory);
		Sync = sync ?? throw new ArgumentNullException(nameof(sync));
		Log = loggerFactory.CreateLogger(GetType());
		LifecycleState = new LifecycleState(Sync, GetType());
	}

	#endregion

	#region LifecycleState

	/// <summary>
	/// Gets the state of the lifecycle controller.
	/// </summary>
	protected LifecycleState LifecycleState { get; }

	/// <summary>
	/// Gets a value indicating whether the object has finished initialization.
	/// </summary>
	public bool IsInitialized
	{
		get
		{
			lock (LifecycleState.Sync)
			{
				return LifecycleState.IsInitialized;
			}
		}
	}

	#endregion

	#region Initialization

	private readonly List<AsyncManualResetEvent> mWaitForInitializationToCompleteEvents = [];

	/// <summary>
	/// Asynchronously initializes the object.<br/>
	/// The actual initialization logic runs only once.<br/>
	/// - If called after the object is successfully initialized, it throws an <see cref="InvalidOperationException"/>.<br/>
	/// - If called while another thread is initializing, the caller waits for that operation to complete.
	/// After waiting, it re-evaluates the state: if the first initialization succeeded, it now throws;
	/// if it failed, this call will attempt a new initialization.
	/// </summary>
	/// <param name="context">
	/// The lifecycle context passed to the initialization logic. It is only used for the very first
	/// call.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="InvalidOperationException">The object is already initialized.</exception>
	/// <exception cref="ObjectDisposedException">The object is disposing or already disposed.</exception>
	protected async Task InitializeAsync(ILifecycleContext context, CancellationToken cancellationToken)
	{
		AsyncManualResetEvent? waitForShutdownToCompleteEvent = null;
		AsyncManualResetEvent? waitForInitializationToCompleteEvent = null;

		lock (LifecycleState.Sync)
		{
			LifecycleState.EnsureNotDisposingOrDisposed();

			if (LifecycleState.IsInitialized)
			{
				throw new InvalidOperationException("The object is already initialized.");
			}

			if (LifecycleState.IsShuttingDown)
			{
				// Another thread is shutting down the object at the moment.
				// => Add event that will be signaled when shutdown completes (waiting is done below).
				// => Do NOT increment pending count - we're just waiting, not actively working.
				waitForShutdownToCompleteEvent = new AsyncManualResetEvent(set: false);
				mWaitForShutdownToCompleteEvents.Add(waitForShutdownToCompleteEvent);
			}
			else if (LifecycleState.IsInitializing)
			{
				// Another thread is already initializing the object.
				// => Add event that will be signaled when initialization completes (waiting is done below).
				// => Do NOT increment pending count - we're just waiting, not actively working.
				waitForInitializationToCompleteEvent = new AsyncManualResetEvent(set: false);
				mWaitForInitializationToCompleteEvents.Add(waitForInitializationToCompleteEvent);
			}
			else
			{
				// The current thread is the first one that is allowed to initialize the object.
				// => Set the flag indicating that initialization is in progress.
				// => Increment pending count to prevent disposal while actively initializing.
				LifecycleState.IsInitializing = true;
				LifecycleState.ResetShutdownSignal();
				LifecycleState.IncrementPendingOperationsCount();
			}
		}

		// Wait paths: just wait and retry, no try-finally needed since count was not incremented
		if (waitForShutdownToCompleteEvent != null)
		{
			await waitForShutdownToCompleteEvent.WaitAsync(cancellationToken).ConfigureAwait(false);
			await InitializeAsync(context, cancellationToken).ConfigureAwait(false);
			return;
		}

		if (waitForInitializationToCompleteEvent != null)
		{
			// Wait for another thread to complete initialization.
			await waitForInitializationToCompleteEvent.WaitAsync(cancellationToken).ConfigureAwait(false);

			// After waiting, re-call the method to correctly handle the state.
			// This will either throw InvalidOperationException if the other thread succeeded,
			// or it will become the new initializing thread if the other one failed, thus propagating the failure correctly.
			await InitializeAsync(context, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Run the actual initialization code provided by the derived class.
		try
		{
			try
			{
				await OnInitializingAsync(context, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception iex)
			{
				// An exception occurred.

				// Log the exception, but do this at Debug level only, as initialization failures
				// are expected to be handled by the caller.
				Log.LogDebug(
					iex,
					"{TypeName}.{MethodName}() threw an unexpected exception. This should never occur",
					GetType().FullName,
					nameof(OnShuttingDownAsync));

				// Shut down to try to bring the instance into a valid state and rethrow the exception.
				try
				{
					await OnShuttingDownAsync(context).ConfigureAwait(false);
				}
				catch (Exception sex)
				{
					Log.LogCritical(
						sex,
						"{TypeName}.{MethodName}() threw an unexpected exception. This should never occur",
						GetType().FullName,
						nameof(OnShuttingDownAsync));

					FailFast.TerminateApplication(sex);
					throw new UnreachableException();
				}

				lock (LifecycleState.Sync)
				{
					// Set the lifecycle state appropriately.
					LifecycleState.IsInitializing = false;

					// Let threads waiting for the initialization proceed.
					mWaitForInitializationToCompleteEvents.ForEach(@event => @event.Set());
					mWaitForInitializationToCompleteEvents.Clear();
				}

				throw;
			}

			// Initialization completed successfully.
			lock (LifecycleState.Sync)
			{
				// Set the lifecycle state appropriately.
				LifecycleState.IsInitializing = false;
				LifecycleState.IsInitialized = true;

				// Let threads waiting for the initialization proceed.
				mWaitForInitializationToCompleteEvents.ForEach(@event => @event.Set());
				mWaitForInitializationToCompleteEvents.Clear();
			}
		}
		finally
		{
			lock (LifecycleState.Sync)
			{
				// The operation has been completed (may have succeeded or failed).
				LifecycleState.DecrementPendingOperationsCount();
			}
		}
	}

	/// <summary>
	/// Runs the actual initialization code of the class. This method is called only once.
	/// </summary>
	/// <param name="context">The lifecycle context passed to <see cref="InitializeAsync"/>.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	protected abstract Task OnInitializingAsync(
		ILifecycleContext context,
		CancellationToken cancellationToken);

	#endregion

	#region Shutdown

	private readonly List<AsyncManualResetEvent> mWaitForShutdownToCompleteEvents = [];

	/// <summary>
	/// Asynchronously shuts the object down.<br/>
	/// - If the object is not initialized, the call is ignored.<br/>
	/// - If called while another thread is shutting down, the caller waits for that operation to complete before returning.
	/// </summary>
	/// <param name="context">The lifecycle context to pass along to handlers.</param>
	/// <exception cref="ObjectDisposedException">The object is disposing or already disposed.</exception>
	/// <remarks>
	/// This method is guaranteed not to throw any exceptions, except <see cref="ObjectDisposedException"/>.
	/// </remarks>
	protected async Task ShutdownAsync(ILifecycleContext context)
	{
		AsyncManualResetEvent? waitForShutdownToCompleteEvent = null;
		AsyncManualResetEvent? waitForInitializationToCompleteEvent = null;

		lock (LifecycleState.Sync)
		{
			LifecycleState.EnsureNotDisposingOrDisposed();

			if (LifecycleState.IsShuttingDown)
			{
				// Another thread is already shutting the object down.
				// => Wait for the shutdown to complete (actual waiting is done below).
				// => Do NOT increment pending count - we're just waiting, not actively working.
				waitForShutdownToCompleteEvent = new AsyncManualResetEvent(set: false);
				mWaitForShutdownToCompleteEvents.Add(waitForShutdownToCompleteEvent);
			}
			else if (LifecycleState.IsInitializing)
			{
				// Another thread is initializing the object at the moment.
				// => Wait for the initialization to complete (actual waiting is done below).
				// => Do NOT increment pending count - we're just waiting, not actively working.
				waitForInitializationToCompleteEvent = new AsyncManualResetEvent(set: false);
				mWaitForInitializationToCompleteEvents.Add(waitForInitializationToCompleteEvent);
			}
			else if (!LifecycleState.IsInitialized)
			{
				// The object is not initialized, not initializing, and not shutting down.
				// => Nothing to do.
				return;
			}
			else
			{
				// The current thread is the first one that is allowed to shut down the object.
				// Set the flag indicating that the shutdown is in progress.
				// Also set IsInitialized to false right away to prevent other threads from starting
				// operations on the object while it is being shut down.
				// Increment pending count to prevent disposal while actively shutting down.
				LifecycleState.IsShuttingDown = true;
				LifecycleState.IsInitialized = false;
				LifecycleState.SignalShutdown();
				LifecycleState.IncrementPendingOperationsCount();
			}
		}

		// Wait paths: just wait and retry, no try-finally needed since count was not incremented
		if (waitForInitializationToCompleteEvent != null)
		{
			await waitForInitializationToCompleteEvent.WaitAsync().ConfigureAwait(false);
			await ShutdownAsync(context).ConfigureAwait(false);
			return;
		}

		if (waitForShutdownToCompleteEvent != null)
		{
			await waitForShutdownToCompleteEvent.WaitAsync().ConfigureAwait(false);
			// Re-call to ensure a consistent state before returning to the caller.
			await ShutdownAsync(context).ConfigureAwait(false);
			return;
		}

		// Active shutdown path
		try
		{
			// Wait for pending operations to complete.
			// There is at least the current shutdown operation pending,
			// so we cannot use the NoPendingOperationsLeftEvent (which waits for count == 0).
			// Instead, we wait for count == 1 (only this shutdown operation remaining).
			// Note: No loop is needed here because:
			// - BeginOperation() throws InvalidOperationException (IsInitialized == false)
			// - Waiting Initialize/Shutdown calls don't increment the count anymore
			await LifecycleState.OnlyOneOperationPendingEvent.WaitAsync().ConfigureAwait(false);

			// There are no pending operations left (except the current shutdown operation).
			// Proceed with shutdown...

			// Run the actual shutdown code provided by the derived class.
			try
			{
				await OnShuttingDownAsync(context).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.LogCritical(
					ex,
					"{TypeName}.{MethodName}() threw an unexpected exception. This should never occur",
					GetType().FullName,
					nameof(OnShuttingDownAsync));

				FailFast.TerminateApplication(ex);
				throw new UnreachableException();
			}

			lock (LifecycleState.Sync)
			{
				// Set lifecycle state appropriately.
				LifecycleState.IsShuttingDown = false;

				// Let threads waiting for the shutdown proceed.
				mWaitForShutdownToCompleteEvents.ForEach(@event => @event.Set());
				mWaitForShutdownToCompleteEvents.Clear();
			}
		}
		finally
		{
			lock (LifecycleState.Sync)
			{
				LifecycleState.DecrementPendingOperationsCount();
			}
		}
	}

	/// <summary>
	/// Runs the actual shutdown code of the class.<br/>
	/// This method is called only once.<br/>
	/// This method can also be called to clean up after a failed initialization.<br/>
	/// It is expected not to throw any exceptions. If it does, the entire application will be terminated.
	/// </summary>
	/// <param name="context">The lifecycle context passed to <see cref="ShutdownAsync"/>.</param>
	protected abstract Task OnShuttingDownAsync(ILifecycleContext context);

	#endregion

	#region Disposal

	private readonly List<AsyncManualResetEvent> mWaitForDisposalToCompleteEvents = [];

	/// <summary>
	/// Disposes the object, shutting it down (if necessary) and freeing resources.<br/>
	/// This method is expected not to throw any exceptions.
	/// </summary>
	public void Dispose()
	{
		DisposeAsync().AsTask().Wait();
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Asynchronously disposes the object, shutting it down (if necessary) and freeing resources.<br/>
	/// This method is expected not to throw any exceptions.
	/// </summary>
	public abstract ValueTask DisposeAsync();

	/// <summary>
	/// Asynchronously disposes the object, shutting it down (if necessary) and freeing resources.<br/>
	/// The method is expected not to throw any exceptions. If it does, the entire application will be terminated.
	/// </summary>
	/// <param name="context">The lifecycle context to pass along to handlers.</param>
	protected async ValueTask DisposeAsync(ILifecycleContext context)
	{
		AsyncManualResetEvent? waitForDisposalToCompleteEvent = null;

		lock (LifecycleState.Sync)
		{
			if (LifecycleState.IsDisposed)
				return;

			if (LifecycleState.IsDisposing)
			{
				// Some other thread is already disposing the object.
				// => Add event that will be signaled when disposal completes (waiting is done below).
				Debug.Assert(LifecycleState.PendingOperationCount == 0);
				waitForDisposalToCompleteEvent = new AsyncManualResetEvent(set: false);
				mWaitForDisposalToCompleteEvents.Add(waitForDisposalToCompleteEvent);
			}
			else
			{
				LifecycleState.IsDisposing = true;
				LifecycleState.SignalShutdown();
			}
		}

		// Wait for other thread to complete disposal, if necessary.
		if (waitForDisposalToCompleteEvent != null)
		{
			await waitForDisposalToCompleteEvent.WaitAsync().ConfigureAwait(false);
			return;
		}

		// Wait for other operations to complete, if necessary, before starting disposal.
		await LifecycleState.NoPendingOperationsLeftEvent.WaitAsync().ConfigureAwait(false);

		// Determine whether the object needs to be shut down before disposal.
		bool performShutdown;
		lock (LifecycleState.Sync)
		{
			Debug.Assert(
				LifecycleState.PendingOperationCount == 0,
				"There should not be any pending operations left here.");
			performShutdown = LifecycleState.IsInitialized;

			// Mark object as not initialized right away to prevent other threads from starting
			// operations on the object while it is being disposed.
			LifecycleState.IsInitialized = false;
		}

		// The current thread is the first one that is allowed to dispose the object
		// (other threads would see LifecycleState.IsDisposing and wait at an event for the current thread to complete).

		// Run the shutdown sequence, if the object is initialized.
		if (performShutdown)
		{
			try
			{
				await OnShuttingDownAsync(context).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.LogCritical(
					ex,
					"{TypeName}.{MethodName}() threw an unexpected exception. This should never occur",
					GetType().FullName,
					nameof(OnShuttingDownAsync));

				FailFast.TerminateApplication(ex);
				throw new UnreachableException();
			}
		}

		// Run the actual disposal code provided by the derived class.
		try
		{
			await OnDisposingAsync(context).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Log.LogCritical(
				ex,
				"{TypeName}.{MethodName}() threw an unexpected exception. This should never occur",
				GetType().FullName,
				nameof(OnDisposingAsync));

			FailFast.TerminateApplication(ex);
			throw new UnreachableException();
		}

		LifecycleState.Dispose();

		// Mark object as 'disposed' and release threads that might be waiting for disposal.
		lock (LifecycleState.Sync)
		{
			// disposal is complete now
			LifecycleState.IsDisposing = false;
			LifecycleState.IsDisposed = true;

			// let threads waiting for disposal to complete proceed
			mWaitForDisposalToCompleteEvents.ForEach(@event => @event.Set());
			mWaitForDisposalToCompleteEvents.Clear();
		}
	}

	/// <summary>
	/// Runs the actual disposal code of the class.
	/// This method is expected not to throw any exceptions. If it does, the entire application will be terminated.
	/// </summary>
	/// <param name="context">The lifecycle context passed to <see cref="DisposeAsync(ILifecycleContext)"/>.</param>
	protected abstract Task OnDisposingAsync(ILifecycleContext context);

	#endregion
}
