// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;
using LumaCore.TestUtilities.Logging;

using Microsoft.Extensions.Logging;

using Xunit;

// ReSharper disable MethodSupportsCancellation
// ReSharper disable ReplaceAsyncWithTaskReturn

namespace LumaCore.Core.Tests;

public partial class LifecycleManagementTests
{
	#region Invalid State Transitions

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.InitializeAsync"/> on an already initialized object throws
	/// <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public async Task Initialize_WhenAlreadyInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.InitializeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAsync());
		Assert.Equal("The object is already initialized.", ex.Message);

		// Object should still be initialized after the failed second call
		AssertInitializedState(sut, expectedInitCount: 1, expectedShutdownCount: 0);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.InitializeAsync"/> on a disposed object throws
	/// <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public async Task Initialize_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.InitializeAsync());

		// The exception contains the owner type name (the derived class)
		Assert.Equal(typeof(TestableLifecycleManagement).FullName, ex.ObjectName);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.ShutdownAsync"/> on a disposed object throws
	/// <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public async Task Shutdown_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.DisposeAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.ShutdownAsync());

		// The exception contains the owner type name (the derived class)
		Assert.Equal(typeof(TestableLifecycleManagement).FullName, ex.ObjectName);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.BeginOperation"/> on an uninitialized object throws
	/// <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public void BeginOperation_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => sut.BeginOperation());
		Assert.Equal("The object is not initialized.", ex.Message);

		// Object should still be in freshly constructed state
		AssertFreshlyConstructedState(sut);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.BeginAsyncOperation"/> on an uninitialized object throws
	/// <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public void BeginAsyncOperation_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => sut.BeginAsyncOperation());
		Assert.Equal("The object is not initialized.", ex.Message);

		// Object should still be in freshly constructed state
		AssertFreshlyConstructedState(sut);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.BeginOperation"/> on a disposed object throws
	/// <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public async Task BeginOperation_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.DisposeAsync();

		// Act + Assert
		var ex = Assert.Throws<ObjectDisposedException>(() => sut.BeginOperation());

		// The exception contains the owner type name (the derived class)
		Assert.Equal(typeof(TestableLifecycleManagement).FullName, ex.ObjectName);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.BeginAsyncOperation"/> on a disposed object throws
	/// <see cref="ObjectDisposedException"/>.
	/// </summary>
	[Fact]
	public async Task BeginAsyncOperation_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.DisposeAsync();

		// Act + Assert
		var ex = Assert.Throws<ObjectDisposedException>(() => sut.BeginAsyncOperation());

		// The exception contains the owner type name (the derived class)
		Assert.Equal(typeof(TestableLifecycleManagement).FullName, ex.ObjectName);
	}

	#endregion

	#region EnsureCanChangeConfiguration

	/// <summary>
	/// Verifies that <see cref="LifecycleState.EnsureCanChangeConfiguration"/> does not throw
	/// when the object is in uninitialized state.
	/// </summary>
	[Fact]
	public void EnsureCanChangeConfiguration_WhenUninitialized_DoesNotThrow()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();

		// Act + Assert
		lock (sut.LifecycleState.Sync)
		{
			sut.LifecycleState.EnsureCanChangeConfiguration();
		}
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleState.EnsureCanChangeConfiguration"/> throws
	/// <see cref="ObjectDisposedException"/> when the object is disposed.
	/// </summary>
	[Fact]
	public async Task EnsureCanChangeConfiguration_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.DisposeAsync();

		// Act + Assert
		lock (sut.LifecycleState.Sync)
		{
			var ex = Assert.Throws<ObjectDisposedException>(() => sut.LifecycleState.EnsureCanChangeConfiguration());
			// The exception must report the owning type (the derived class), not the LifecycleState helper itself.
			Assert.Equal(typeof(TestableLifecycleManagement).FullName, ex.ObjectName);
		}
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleState.EnsureCanChangeConfiguration"/> throws
	/// <see cref="ObjectDisposedException"/> when the object is disposing.
	/// </summary>
	[Fact]
	public async Task EnsureCanChangeConfiguration_WhenDisposing_ThrowsObjectDisposedException()
	{
		// Arrange
		var disposingStarted = new AsyncManualResetEvent(false);
		var allowDisposingToComplete = new AsyncManualResetEvent(false);

		TestableLifecycleManagement sut = CreateSut(
			onDisposingCallback: async _ =>
			{
				disposingStarted.Set();
				await allowDisposingToComplete.WaitAsync();
			});

		Task disposeTask = sut.DisposeAsync().AsTask();
		await disposingStarted.WaitAsync();

		// Act + Assert
		lock (sut.LifecycleState.Sync)
		{
			var ex = Assert.Throws<ObjectDisposedException>(() => sut.LifecycleState.EnsureCanChangeConfiguration());
			// The exception must report the owning type (the derived class), not the LifecycleState helper itself.
			Assert.Equal(typeof(TestableLifecycleManagement).FullName, ex.ObjectName);
		}

		// Cleanup
		allowDisposingToComplete.Set();
		await disposeTask;
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleState.EnsureCanChangeConfiguration"/> throws
	/// <see cref="InvalidOperationException"/> when the object is initialized.
	/// </summary>
	[Fact]
	public async Task EnsureCanChangeConfiguration_WhenInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.InitializeAsync();

		// Act + Assert
		lock (sut.LifecycleState.Sync)
		{
			var ex = Assert.Throws<InvalidOperationException>(() => sut.LifecycleState.EnsureCanChangeConfiguration());
			Assert.Equal("The object is initializing or already initialized.", ex.Message);
		}
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleState.EnsureCanChangeConfiguration"/> throws
	/// <see cref="InvalidOperationException"/> when the object is shutting down.
	/// </summary>
	[Fact]
	public async Task EnsureCanChangeConfiguration_WhenShuttingDown_ThrowsInvalidOperationException()
	{
		// Arrange
		var shutdownStarted = new AsyncManualResetEvent(false);
		var allowShutdownToComplete = new AsyncManualResetEvent(false);

		TestableLifecycleManagement sut = CreateSut(
			onShuttingDownCallback: async _ =>
			{
				shutdownStarted.Set();
				await allowShutdownToComplete.WaitAsync();
			});

		await sut.InitializeAsync();
		Task shutdownTask = sut.ShutdownAsync();
		await shutdownStarted.WaitAsync();

		// Act + Assert
		lock (sut.LifecycleState.Sync)
		{
			var ex = Assert.Throws<InvalidOperationException>(() => sut.LifecycleState.EnsureCanChangeConfiguration());
			Assert.Equal("The object is shutting down, but has not finished, yet.", ex.Message);
		}

		// Cleanup
		allowShutdownToComplete.Set();
		await shutdownTask;
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleState.EnsureCanChangeConfiguration"/> throws
	/// <see cref="InvalidOperationException"/> when the object is initializing.
	/// </summary>
	[Fact]
	public async Task EnsureCanChangeConfiguration_WhenInitializing_ThrowsInvalidOperationException()
	{
		// Arrange
		var initializingStarted = new AsyncManualResetEvent(false);
		var allowInitializationToComplete = new AsyncManualResetEvent(false);

		TestableLifecycleManagement sut = CreateSut(
			onInitializingCallback: async (_, ct) =>
			{
				initializingStarted.Set();
				await allowInitializationToComplete.WaitAsync(ct);
			});

		Task initTask = sut.InitializeAsync();
		await initializingStarted.WaitAsync();

		// Act + Assert
		lock (sut.LifecycleState.Sync)
		{
			var ex = Assert.Throws<InvalidOperationException>(() => sut.LifecycleState.EnsureCanChangeConfiguration());
			Assert.Equal("The object is initializing or already initialized.", ex.Message);
		}

		// Cleanup
		allowInitializationToComplete.Set();
		await initTask;
	}

	#endregion

	#region Initialization Failure

	/// <summary>
	/// Verifies that when <see cref="LifecycleManagement.OnInitializingAsync"/> throws, the object remains
	/// uninitialized and <see cref="LifecycleManagement.OnShuttingDownAsync"/> is called for cleanup.
	/// </summary>
	[Fact]
	public async Task Initialize_WhenOnInitializingThrows_CallsShutdownForCleanupAndRethrows()
	{
		// Arrange
		var expectedException = new InvalidOperationException("Initialization failed");
		TestableLifecycleManagement sut = CreateSut(onInitializingCallback: (_, _) => throw expectedException);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAsync());
		Assert.Same(expectedException, ex);

		// After a failed initialization with cleanup:
		// - IsInitialized is false (never became true)
		// - OnInitializingAsync was called once (and threw)
		// - OnShuttingDownAsync was called once (for cleanup)
		// - ShutdownToken is NOT signaled (cleanup is not a "real" shutdown)
		Assert.False(sut.IsInitialized);
		Assert.Equal(1, sut.OnInitializingCallCount);
		Assert.Equal(1, sut.OnShuttingDownCallCount);
		Assert.Equal(0, sut.OnDisposingCallCount);
	}

	/// <summary>
	/// Verifies that the diagnostic log entry emitted when <see cref="LifecycleManagement.OnInitializingAsync"/>
	/// throws correctly identifies <c>OnInitializingAsync</c> as the failing method (regression test for the
	/// previous copy-paste bug that mis-named the method as <c>OnShuttingDownAsync</c>).
	/// </summary>
	[Fact]
	public async Task Initialize_WhenOnInitializingThrows_LogsOnInitializingAsyncAsFailingMethod()
	{
		// Arrange
		var loggerFactory = new ListLoggerFactory();
		var initException = new InvalidOperationException("Initialization failed");
		var sut = new TestableLifecycleManagement(
			loggerFactory,
			onInitializingCallback: (_, _) => throw initException);

		// Act
		await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeAsync());

		// Assert: a Debug-level entry that references the failing OnInitializingAsync method exists,
		// and no entry references OnShuttingDownAsync as the *failing* method (cleanup succeeded).
		LogEntry diagnostic = Assert.Single(
			loggerFactory.Entries,
			e => e.Level == LogLevel.Debug && e.Exception == initException);

		Assert.Contains("OnInitializingAsync", diagnostic.Message);
		Assert.DoesNotContain("OnShuttingDownAsync", diagnostic.Message);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.InitializeAsync"/> respects <see cref="CancellationToken"/>
	/// and throws <see cref="OperationCanceledException"/> with the correct token when cancellation is requested.
	/// </summary>
	[Fact]
	public async Task Initialize_WhenCancellationRequested_ThrowsOperationCanceledException()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		TestableLifecycleManagement sut = CreateSut(
			onInitializingCallback: (_, ct) =>
			{
				ct.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			});

		await cts.CancelAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAsync<OperationCanceledException>(() => sut.InitializeAsync(cts.Token));
		Assert.Equal(cts.Token, ex.CancellationToken);
	}

	#endregion

	#region FailFast Termination Paths

	/// <summary>
	/// Verifies that when <see cref="LifecycleManagement.OnShuttingDownAsync"/> throws during initialization cleanup,
	/// <see cref="FailFast.TerminateApplication(Exception)"/> is invoked.
	/// </summary>
	[Fact]
	public async Task Initialize_WhenShutdownThrowsDuringCleanup_InvokesFailFast()
	{
		// Arrange
		using var failFastScope = new FailFastTestScope();
		var initException = new InvalidOperationException("Initialization failed");
		var shutdownException = new InvalidOperationException("Shutdown cleanup failed");

		// OnInitializingAsync() throws, then OnShuttingDownAsync (cleanup) also throws.
		TestableLifecycleManagement sut = CreateSut(
			onInitializingCallback: (_, _) => throw initException,
			onShuttingDownCallback: _ => throw shutdownException);

		// Act + Assert
		// The shutdown exception during cleanup triggers FailFast.
		var ex = await Assert.ThrowsAsync<FailFastCanceledException>(() => sut.InitializeAsync());

		Assert.True(failFastScope.WasTerminationRequested);
		Assert.Same(shutdownException, failFastScope.CapturedException);
		Assert.Equal(shutdownException.Message, ex.Message);
		Assert.Same(shutdownException, ex.InnerException);
	}

	/// <summary>
	/// Verifies that when <see cref="LifecycleManagement.OnShuttingDownAsync"/> throws during normal shutdown,
	/// <see cref="FailFast.TerminateApplication(Exception)"/> is invoked.
	/// </summary>
	[Fact]
	public async Task Shutdown_WhenOnShuttingDownThrows_InvokesFailFast()
	{
		// Arrange
		using var failFastScope = new FailFastTestScope();
		var shutdownException = new InvalidOperationException("Shutdown failed");

		TestableLifecycleManagement sut = CreateSut(onShuttingDownCallback: _ => throw shutdownException);

		await sut.InitializeAsync();

		// Act + Assert
		// Shutdown failure is unrecoverable - triggers FailFast.
		var ex = await Assert.ThrowsAsync<FailFastCanceledException>(() => sut.ShutdownAsync());

		Assert.True(failFastScope.WasTerminationRequested);
		Assert.Same(shutdownException, failFastScope.CapturedException);
		Assert.Equal(shutdownException.Message, ex.Message);
		Assert.Same(shutdownException, ex.InnerException);
	}

	/// <summary>
	/// Verifies that when <see cref="LifecycleManagement.OnDisposingAsync"/> throws,
	/// <see cref="FailFast.TerminateApplication(Exception)"/> is invoked.
	/// </summary>
	[Fact]
	public async Task Dispose_WhenOnDisposingThrows_InvokesFailFast()
	{
		// Arrange
		using var failFastScope = new FailFastTestScope();
		var disposeException = new InvalidOperationException("Dispose failed");

		TestableLifecycleManagement sut = CreateSut(onDisposingCallback: _ => throw disposeException);

		// Act + Assert
		// Dispose failure is unrecoverable - triggers FailFast.
		var ex = await Assert.ThrowsAsync<FailFastCanceledException>(() => sut.DisposeAsync().AsTask());

		Assert.True(failFastScope.WasTerminationRequested);
		Assert.Same(disposeException, failFastScope.CapturedException);
		Assert.Equal(disposeException.Message, ex.Message);
		Assert.Same(disposeException, ex.InnerException);
	}

	/// <summary>
	/// Verifies that when <see cref="LifecycleManagement.OnShuttingDownAsync"/> throws during disposal of an
	/// initialized object, <see cref="FailFast.TerminateApplication(Exception)"/> is invoked.
	/// </summary>
	[Fact]
	public async Task Dispose_WhenInitializedAndShutdownThrows_InvokesFailFast()
	{
		// Arrange
		using var failFastScope = new FailFastTestScope();
		var shutdownException = new InvalidOperationException("Shutdown during dispose failed");

		TestableLifecycleManagement sut = CreateSut(onShuttingDownCallback: _ => throw shutdownException);

		await sut.InitializeAsync();

		// Act + Assert
		// Dispose calls Shutdown first (because initialized). Shutdown throws → FailFast.
		var ex = await Assert.ThrowsAsync<FailFastCanceledException>(() => sut.DisposeAsync().AsTask());

		Assert.True(failFastScope.WasTerminationRequested);
		Assert.Same(shutdownException, failFastScope.CapturedException);
		Assert.Equal(shutdownException.Message, ex.Message);
		Assert.Same(shutdownException, ex.InnerException);
	}

	#endregion
}
