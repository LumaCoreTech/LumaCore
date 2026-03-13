// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

// ReSharper disable ReplaceAsyncWithTaskReturn

namespace LumaCore.Core.Tests;

public partial class LifecycleManagementTests
{
	/// <summary>
	/// Creates a new <see cref="TestableLifecycleManagement"/> instance with default configuration.
	/// </summary>
	/// <param name="onInitializingCallback">Optional callback invoked during initialization.</param>
	/// <param name="onShuttingDownCallback">Optional callback invoked during shutdown.</param>
	/// <param name="onDisposingCallback">Optional callback invoked during disposal.</param>
	/// <returns>A new testable lifecycle management instance.</returns>
	private static TestableLifecycleManagement CreateSut(
		Func<ILifecycleContext, CancellationToken, Task>? onInitializingCallback = null,
		Func<ILifecycleContext, Task>?                    onShuttingDownCallback = null,
		Func<ILifecycleContext, Task>?                    onDisposingCallback    = null)
	{
		return new TestableLifecycleManagement(
			NullLoggerFactory.Instance,
			onInitializingCallback,
			onShuttingDownCallback,
			onDisposingCallback);
	}

	/// <summary>
	/// A disposable scope that intercepts <see cref="FailFast.BeforeTermination"/> events and prevents actual
	/// process termination during tests.
	/// </summary>
	/// <remarks>
	/// This scope captures a <see cref="FailFast.TerminateApplication(Exception)"/> call and records the
	/// exception that would have caused termination, allowing tests to verify that FailFast was invoked
	/// with the expected exception.
	/// </remarks>
	private sealed class FailFastTestScope : IDisposable
	{
		private FailFastEventArgs? mCapturedEvent;

		/// <summary>
		/// Initializes a new instance of the <see cref="FailFastTestScope"/> class and subscribes to
		/// <see cref="FailFast.BeforeTermination"/>.
		/// </summary>
		public FailFastTestScope()
		{
			FailFast.BeforeTermination += OnBeforeTermination;
		}

		/// <summary>
		/// Unsubscribes from <see cref="FailFast.BeforeTermination"/>.
		/// </summary>
		public void Dispose()
		{
			FailFast.BeforeTermination -= OnBeforeTermination;
		}

		/// <summary>
		/// Gets a value indicating whether <see cref="FailFast.TerminateApplication(string)"/> or
		/// <see cref="FailFast.TerminateApplication(Exception)"/> was invoked.
		/// </summary>
		public bool WasTerminationRequested => mCapturedEvent != null;

		/// <summary>
		/// Gets the exception from the captured termination request, or <see langword="null"/> if none.
		/// </summary>
		public Exception? CapturedException => mCapturedEvent?.Exception;

		private void OnBeforeTermination(object? sender, FailFastEventArgs e)
		{
			mCapturedEvent = e;
			e.Cancel = true;
		}
	}

	/// <summary>
	/// A testable concrete implementation of <see cref="LifecycleManagement"/> that exposes protected members
	/// and tracks lifecycle method invocations for verification.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This class provides:
	///     <list type="bullet">
	///         <item>Public wrappers for protected lifecycle methods (<c>InitializeAsync</c>, <c>ShutdownAsync</c>)</item>
	///         <item>Counters to track how many times each lifecycle callback was invoked</item>
	///         <item>
	///         Configurable behavior for lifecycle callbacks (e.g., throwing exceptions, simulating delays)
	///         </item>
	///         <item>Access to <c>BeginOperation</c> and <c>BeginAsyncOperation</c> for scope testing</item>
	///     </list>
	///     </para>
	/// </remarks>
	private sealed class TestableLifecycleManagement : LifecycleManagement
	{
		private readonly Func<ILifecycleContext, CancellationToken, Task>? mOnInitializingCallback;
		private readonly Func<ILifecycleContext, Task>?                    mOnShuttingDownCallback;
		private readonly Func<ILifecycleContext, Task>?                    mOnDisposingCallback;

		/// <summary>
		/// Initializes a new instance of the <see cref="TestableLifecycleManagement"/> class.
		/// </summary>
		/// <param name="loggerFactory">The logger factory used to create a logger for this instance.</param>
		/// <param name="onInitializingCallback">Optional callback invoked during initialization.</param>
		/// <param name="onShuttingDownCallback">Optional callback invoked during shutdown.</param>
		/// <param name="onDisposingCallback">Optional callback invoked during disposal.</param>
		public TestableLifecycleManagement(
			ILoggerFactory                                    loggerFactory,
			Func<ILifecycleContext, CancellationToken, Task>? onInitializingCallback = null,
			Func<ILifecycleContext, Task>?                    onShuttingDownCallback = null,
			Func<ILifecycleContext, Task>?                    onDisposingCallback    = null)
			: base(loggerFactory)
		{
			mOnInitializingCallback = onInitializingCallback;
			mOnShuttingDownCallback = onShuttingDownCallback;
			mOnDisposingCallback = onDisposingCallback;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TestableLifecycleManagement"/> class with a custom
		/// synchronization object.
		/// </summary>
		/// <param name="loggerFactory">The logger factory used to create a logger for this instance.</param>
		/// <param name="sync">The <see cref="Lock"/> instance used to synchronize access to members.</param>
		/// <param name="onInitializingCallback">Optional callback invoked during initialization.</param>
		/// <param name="onShuttingDownCallback">Optional callback invoked during shutdown.</param>
		/// <param name="onDisposingCallback">Optional callback invoked during disposal.</param>
		public TestableLifecycleManagement(
			ILoggerFactory                                    loggerFactory,
			Lock                                              sync,
			Func<ILifecycleContext, CancellationToken, Task>? onInitializingCallback = null,
			Func<ILifecycleContext, Task>?                    onShuttingDownCallback = null,
			Func<ILifecycleContext, Task>?                    onDisposingCallback    = null)
			: base(loggerFactory, sync)
		{
			mOnInitializingCallback = onInitializingCallback;
			mOnShuttingDownCallback = onShuttingDownCallback;
			mOnDisposingCallback = onDisposingCallback;
		}

		/// <inheritdoc/>
		public override async ValueTask DisposeAsync()
		{
			await DisposeAsync(new LifecycleContext()).ConfigureAwait(false);
			// GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Gets the number of times <see cref="OnInitializingAsync"/> was called.
		/// </summary>
		public int OnInitializingCallCount { get; private set; }

		/// <summary>
		/// Gets the number of times <see cref="OnShuttingDownAsync"/> was called.
		/// </summary>
		public int OnShuttingDownCallCount { get; private set; }

		/// <summary>
		/// Gets the number of times <see cref="OnDisposingAsync"/> was called.
		/// </summary>
		public int OnDisposingCallCount { get; private set; }

		/// <summary>
		/// Gets the protected <see cref="LifecycleManagement.LifecycleState"/> for test verification.
		/// </summary>
		public new LifecycleState LifecycleState => base.LifecycleState;

		/// <summary>
		/// Gets the protected <see cref="LifecycleManagement.Sync"/> object for test verification.
		/// </summary>
		public new Lock Sync => base.Sync;

		/// <summary>
		/// Gets the protected <see cref="LifecycleManagement.Log"/> for test verification.
		/// </summary>
		public new ILogger Log => base.Log;

		/// <summary>
		/// Public wrapper for the protected <see cref="LifecycleManagement.InitializeAsync"/> method.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public Task InitializeAsync(CancellationToken cancellationToken = default)
		{
			return InitializeAsync(new LifecycleContext(), cancellationToken);
		}

		/// <summary>
		/// Public wrapper for the protected <see cref="LifecycleManagement.ShutdownAsync"/> method.
		/// </summary>
		public Task ShutdownAsync()
		{
			return ShutdownAsync(new LifecycleContext());
		}

		/// <summary>
		/// Public wrapper for the protected <see cref="LifecycleManagement.BeginOperation"/> method.
		/// </summary>
		/// <returns>An <see cref="LifecycleManagement.OperationScope"/> that must be disposed when the operation completes.</returns>
		public new OperationScope BeginOperation() => base.BeginOperation();

		/// <summary>
		/// Public wrapper for the protected <see cref="LifecycleManagement.BeginAsyncOperation"/> method.
		/// </summary>
		/// <returns>An <see cref="LifecycleManagement.AsyncOperationScope"/> that must be disposed when the operation completes.</returns>
		public new AsyncOperationScope BeginAsyncOperation() => base.BeginAsyncOperation();

		/// <inheritdoc/>
		protected override async Task OnInitializingAsync(
			ILifecycleContext context,
			CancellationToken cancellationToken)
		{
			OnInitializingCallCount++;
			if (mOnInitializingCallback != null)
			{
				await mOnInitializingCallback(context, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <inheritdoc/>
		protected override async Task OnShuttingDownAsync(ILifecycleContext context)
		{
			OnShuttingDownCallCount++;
			if (mOnShuttingDownCallback != null)
			{
				await mOnShuttingDownCallback(context).ConfigureAwait(false);
			}
		}

		/// <inheritdoc/>
		protected override async Task OnDisposingAsync(ILifecycleContext context)
		{
			OnDisposingCallCount++;
			if (mOnDisposingCallback != null)
			{
				await mOnDisposingCallback(context).ConfigureAwait(false);
			}
		}
	}

	#region State Verification Helpers

	/// <summary>
	/// Asserts that the <see cref="TestableLifecycleManagement"/> is in the expected uninitialized state
	/// immediately after construction.
	/// </summary>
	/// <param name="sut">The instance to verify.</param>
	/// <param name="expectedSync">
	/// The expected sync object, or <see langword="null"/> to verify that a default sync object was created.
	/// </param>
	private static void AssertFreshlyConstructedState(TestableLifecycleManagement sut, Lock? expectedSync = null)
	{
		Assert.NotNull(sut);
		Assert.NotNull(sut.Log);
		Assert.NotNull(sut.LifecycleState);

		// Sync object
#pragma warning disable CS9216 // Lock-to-object conversion is safe — used in assertions, not locking
		if (expectedSync != null)
		{
			Assert.Same(expectedSync, sut.Sync);
		}
		else
		{
			Assert.NotNull(sut.Sync);
		}
#pragma warning restore CS9216

		// Lifecycle state
		Assert.False(sut.IsInitialized);

		// LifecycleState internals
		lock (sut.LifecycleState.Sync)
		{
			Assert.Equal(0, sut.LifecycleState.PendingOperationCount);
			Assert.False(sut.LifecycleState.ShutdownToken.IsCancellationRequested);
		}

		// Callback counters
		Assert.Equal(0, sut.OnInitializingCallCount);
		Assert.Equal(0, sut.OnShuttingDownCallCount);
		Assert.Equal(0, sut.OnDisposingCallCount);
	}

	/// <summary>
	/// Asserts that the <see cref="TestableLifecycleManagement"/> is in the expected initialized state.
	/// </summary>
	/// <param name="sut">The instance to verify.</param>
	/// <param name="expectedInitCount">Expected number of times initialization was called.</param>
	/// <param name="expectedShutdownCount">Expected number of times shutdown was called.</param>
	private static void AssertInitializedState(
		TestableLifecycleManagement sut,
		int                         expectedInitCount     = 1,
		int                         expectedShutdownCount = 0)
	{
		Assert.True(sut.IsInitialized);

		lock (sut.LifecycleState.Sync)
		{
			Assert.False(sut.LifecycleState.ShutdownToken.IsCancellationRequested);
		}

		Assert.Equal(expectedInitCount, sut.OnInitializingCallCount);
		Assert.Equal(expectedShutdownCount, sut.OnShuttingDownCallCount);
		Assert.Equal(0, sut.OnDisposingCallCount);
	}

	/// <summary>
	/// Asserts that the <see cref="TestableLifecycleManagement"/> is in the expected shutdown (but not disposed) state.
	/// </summary>
	/// <param name="sut">The instance to verify.</param>
	/// <param name="expectedInitCount">Expected number of times initialization was called.</param>
	/// <param name="expectedShutdownCount">Expected number of times shutdown was called.</param>
	private static void AssertShutdownState(
		TestableLifecycleManagement sut,
		int                         expectedInitCount     = 1,
		int                         expectedShutdownCount = 1)
	{
		Assert.False(sut.IsInitialized);

		lock (sut.LifecycleState.Sync)
		{
			Assert.Equal(0, sut.LifecycleState.PendingOperationCount);
			Assert.True(sut.LifecycleState.ShutdownToken.IsCancellationRequested);
		}

		Assert.Equal(expectedInitCount, sut.OnInitializingCallCount);
		Assert.Equal(expectedShutdownCount, sut.OnShuttingDownCallCount);
		Assert.Equal(0, sut.OnDisposingCallCount);
	}

	/// <summary>
	/// Asserts that the <see cref="TestableLifecycleManagement"/> is in the expected disposed state.
	/// </summary>
	/// <param name="sut">The instance to verify.</param>
	/// <param name="expectedInitCount">Expected number of times initialization was called.</param>
	/// <param name="expectedShutdownCount">Expected number of times shutdown was called.</param>
	/// <param name="expectedDisposeCount">Expected number of times dispose was called.</param>
	private static void AssertDisposedState(
		TestableLifecycleManagement sut,
		int                         expectedInitCount     = 0,
		int                         expectedShutdownCount = 0,
		int                         expectedDisposeCount  = 1)
	{
		Assert.False(sut.IsInitialized);

		lock (sut.LifecycleState.Sync)
		{
			Assert.Equal(0, sut.LifecycleState.PendingOperationCount);
		}

		Assert.Equal(expectedInitCount, sut.OnInitializingCallCount);
		Assert.Equal(expectedShutdownCount, sut.OnShuttingDownCallCount);
		Assert.Equal(expectedDisposeCount, sut.OnDisposingCallCount);
	}

	#endregion
}
