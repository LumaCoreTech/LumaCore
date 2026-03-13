// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.Core.Tests.AsyncTestHelpers;

// ReSharper disable ReplaceAsyncWithTaskReturn
// ReSharper disable MethodSupportsCancellation

namespace LumaCore.Core.Tests;

public partial class LifecycleManagementTests
{
	#region Concurrent Shutdown

	/// <summary>
	/// Verifies that concurrent <see cref="LifecycleManagement.ShutdownAsync"/> calls wait for the first to complete.
	/// </summary>
	[Fact]
	public async Task Shutdown_WhenCalledConcurrently_SecondCallWaits()
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

		// Act

		// First shutdown, which will initiate the process, but block completion.
		Task firstShutdown = sut.ShutdownAsync();
		await AwaitWithTimeoutAsync(shutdownStarted.WaitAsync(), "First shutdown did not start");

		// Second concurrent shutdown, which will see that shutdown is in progress and wait.
		Task secondShutdown = sut.ShutdownAsync();
		Assert.False(secondShutdown.IsCompleted);

		// Allow shutdown to complete, both callers should complete.
		allowShutdownToComplete.Set();
		await AwaitWithTimeoutAsync(firstShutdown, "First shutdown did not complete");
		await AwaitWithTimeoutAsync(secondShutdown, "Second shutdown did not complete");

		// Assert
		AssertShutdownState(sut, expectedInitCount: 1, expectedShutdownCount: 1);
	}

	#endregion

	#region Concurrent Disposal

	/// <summary>
	/// Verifies that concurrent <see cref="LifecycleManagement.DisposeAsync()"/> calls wait for the first to complete.
	/// </summary>
	[Fact]
	public async Task Dispose_WhenCalledConcurrently_SecondCallWaits()
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

		// Act
		Task firstDispose = sut.DisposeAsync().AsTask();
		await AwaitWithTimeoutAsync(disposingStarted.WaitAsync(), "First disposal did not start");

		Task secondDispose = sut.DisposeAsync().AsTask();
		Assert.False(secondDispose.IsCompleted);

		allowDisposingToComplete.Set();
		await AwaitWithTimeoutAsync(firstDispose, "First disposal did not complete");
		await AwaitWithTimeoutAsync(secondDispose, "Second disposal did not complete");

		// Assert
		AssertDisposedState(sut, expectedInitCount: 0, expectedShutdownCount: 0, expectedDisposeCount: 1);
	}

	#endregion

	#region Concurrent Initialization

	/// <summary>
	/// Verifies that concurrent <see cref="LifecycleManagement.InitializeAsync"/> calls wait for the first to complete,
	/// then throw <see cref="InvalidOperationException"/> because the object is already initialized.
	/// </summary>
	[Fact]
	public async Task Initialize_WhenCalledConcurrently_SecondCallWaitsAndThrows()
	{
		// Arrange
		var initializingStarted = new AsyncManualResetEvent(false);
		var allowInitializationToComplete = new AsyncManualResetEvent(false);

		TestableLifecycleManagement sut = CreateSut(
			onInitializingCallback: async (_, _) =>
			{
				initializingStarted.Set();
				await allowInitializationToComplete.WaitAsync();
			});

		// Act
		Task firstInit = sut.InitializeAsync();
		await AwaitWithTimeoutAsync(initializingStarted.WaitAsync(), "First initialization did not start");

		Task secondInit = sut.InitializeAsync();
		Assert.False(secondInit.IsCompleted);

		allowInitializationToComplete.Set();
		await AwaitWithTimeoutAsync(firstInit, "First initialization did not complete");

		// Assert
		Task<InvalidOperationException> assertion = Assert.ThrowsAsync<InvalidOperationException>(() => secondInit);
		InvalidOperationException ex = await AwaitWithTimeoutAsync(assertion, "Second initialization did not throw");
		Assert.Equal("The object is already initialized.", ex.Message);
		AssertInitializedState(sut, expectedInitCount: 1, expectedShutdownCount: 0);
	}

	/// <summary>
	/// Verifies that when the first concurrent <see cref="LifecycleManagement.InitializeAsync"/> fails,
	/// the second caller can retry and succeed.
	/// </summary>
	[Fact]
	public async Task Initialize_WhenFirstCallFails_SecondCallerCanSucceed()
	{
		// Arrange
		int callCount = 0;
		var initializingStarted = new AsyncManualResetEvent(false);
		var allowFirstInitToFail = new AsyncManualResetEvent(false);

		TestableLifecycleManagement sut = CreateSut(
			onInitializingCallback: async (_, _) =>
			{
				int currentCall = ++callCount;
				if (currentCall == 1)
				{
					initializingStarted.Set();
					await allowFirstInitToFail.WaitAsync();
					throw new InvalidOperationException("First init failed");
				}
			});

		// Act
		Task firstInit = sut.InitializeAsync();
		await AwaitWithTimeoutAsync(initializingStarted.WaitAsync(), "First initialization did not start");

		Task secondInit = sut.InitializeAsync();
		Assert.False(secondInit.IsCompleted);

		allowFirstInitToFail.Set();
		Task<InvalidOperationException> assertion = Assert.ThrowsAsync<InvalidOperationException>(() => firstInit);
		await AwaitWithTimeoutAsync(assertion, "First initialization did not throw");

		await AwaitWithTimeoutAsync(secondInit, "Second initialization did not complete");

		// Assert
		AssertInitializedState(sut, expectedInitCount: 2, expectedShutdownCount: 1);
	}

	#endregion

	#region Cross-Phase Concurrency

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.ShutdownAsync"/> waits for ongoing
	/// <see cref="LifecycleManagement.InitializeAsync"/> to complete.
	/// </summary>
	[Fact]
	public async Task Shutdown_WhenInitializationInProgress_WaitsForInitialization()
	{
		// Arrange
		var initializingStarted = new AsyncManualResetEvent(false);
		var allowInitializationToComplete = new AsyncManualResetEvent(false);

		TestableLifecycleManagement sut = CreateSut(
			onInitializingCallback: async (_, _) =>
			{
				initializingStarted.Set();
				await allowInitializationToComplete.WaitAsync();
			});

		// Act
		Task initTask = sut.InitializeAsync();
		await AwaitWithTimeoutAsync(initializingStarted.WaitAsync(), "Initialization did not start");

		Task shutdownTask = sut.ShutdownAsync();
		Assert.False(shutdownTask.IsCompleted);

		allowInitializationToComplete.Set();
		await AwaitWithTimeoutAsync(initTask, "Initialization did not complete");
		await AwaitWithTimeoutAsync(shutdownTask, "Shutdown did not complete");

		// Assert
		AssertShutdownState(sut, expectedInitCount: 1, expectedShutdownCount: 1);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.InitializeAsync"/> waits for ongoing
	/// <see cref="LifecycleManagement.ShutdownAsync"/> to complete, then re-initializes.
	/// </summary>
	[Fact]
	public async Task Initialize_WhenShutdownInProgress_WaitsForShutdownThenReinitializes()
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

		// Act
		Task shutdownTask = sut.ShutdownAsync();
		await AwaitWithTimeoutAsync(shutdownStarted.WaitAsync(), "Shutdown did not start");

		Task reinitTask = sut.InitializeAsync();
		Assert.False(reinitTask.IsCompleted);

		allowShutdownToComplete.Set();
		await AwaitWithTimeoutAsync(shutdownTask, "Shutdown did not complete");
		await AwaitWithTimeoutAsync(reinitTask, "Re-initialization did not complete");

		// Assert
		AssertInitializedState(sut, expectedInitCount: 2, expectedShutdownCount: 1);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.DisposeAsync()"/> waits for ongoing
	/// <see cref="LifecycleManagement.InitializeAsync"/> to complete.
	/// </summary>
	[Fact]
	public async Task Dispose_WhenInitializationInProgress_WaitsForInitialization()
	{
		// Arrange
		var initializingStarted = new AsyncManualResetEvent(false);
		var allowInitializationToComplete = new AsyncManualResetEvent(false);
		var disposeCallbackInvoked = new AsyncManualResetEvent(false);

		TestableLifecycleManagement sut = CreateSut(
			onInitializingCallback: async (_, _) =>
			{
				initializingStarted.Set();
				await allowInitializationToComplete.WaitAsync();
			},
			onDisposingCallback: _ =>
			{
				disposeCallbackInvoked.Set();
				return Task.CompletedTask;
			});

		// Act
		Task initTask = sut.InitializeAsync();
		await AwaitWithTimeoutAsync(initializingStarted.WaitAsync(), "Initialization did not start");

		Task disposeTask = sut.DisposeAsync().AsTask();
		await Task.Delay(50);
		Assert.False(disposeCallbackInvoked.IsSet);

		allowInitializationToComplete.Set();
		await AwaitWithTimeoutAsync(initTask, "Initialization did not complete");
		await AwaitWithTimeoutAsync(disposeTask, "Disposal did not complete");

		// Assert
		AssertDisposedState(sut, expectedInitCount: 1, expectedShutdownCount: 1, expectedDisposeCount: 1);
	}

	#endregion

	#region Pending Operations

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.ShutdownAsync"/> waits for pending operations to complete
	/// before invoking <see cref="LifecycleManagement.OnShuttingDownAsync"/>.
	/// </summary>
	/// <remarks>
	/// <see cref="LifecycleManagement.ShutdownAsync"/> uses <c>OnlyOneOperationPendingEvent</c> (count == 1),
	/// meaning it waits until only the shutdown operation itself is pending.
	/// </remarks>
	[Fact]
	public async Task Shutdown_WhenOperationsPending_WaitsForOperationsToComplete()
	{
		// Arrange
		var shutdownCallbackInvoked = new AsyncManualResetEvent(false);

		TestableLifecycleManagement sut = CreateSut(
			onShuttingDownCallback: _ =>
			{
				shutdownCallbackInvoked.Set();
				return Task.CompletedTask;
			});

		await sut.InitializeAsync();

		// Start a long-running operation (no 'using' - we control Dispose explicitly)
		LifecycleManagement.AsyncOperationScope scope = sut.BeginAsyncOperation();

		// Act
		Task shutdownTask = sut.ShutdownAsync();
		await Task.Delay(50);
		Assert.False(shutdownCallbackInvoked.IsSet);

		scope.Dispose();

		await AwaitWithTimeoutAsync(shutdownTask, "Shutdown did not complete");

		// Assert
		await AwaitWithTimeoutAsync(shutdownCallbackInvoked.WaitAsync(), "Shutdown callback was not invoked");
		AssertShutdownState(sut, expectedInitCount: 1, expectedShutdownCount: 1);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.DisposeAsync()"/> waits for pending operations to complete
	/// before proceeding with shutdown and disposal.
	/// </summary>
	/// <remarks>
	/// <see cref="LifecycleManagement.DisposeAsync(ILifecycleContext)"/> uses <c>NoPendingOperationsLeftEvent</c>
	/// (count == 0), meaning it waits until ALL operations are complete before starting disposal. This is different
	/// from <see cref="LifecycleManagement.ShutdownAsync"/> which uses <c>OnlyOneOperationPendingEvent</c> (count == 1).
	/// </remarks>
	[Fact]
	public async Task Dispose_WhenOperationsPending_WaitsForOperationsToComplete()
	{
		// Arrange
		var disposeCallbackInvoked = new AsyncManualResetEvent(false);

		TestableLifecycleManagement sut = CreateSut(
			onDisposingCallback: _ =>
			{
				disposeCallbackInvoked.Set();
				return Task.CompletedTask;
			});

		await sut.InitializeAsync();

		// Start a long-running operation
		LifecycleManagement.AsyncOperationScope scope = sut.BeginAsyncOperation();

		// Act
		Task disposeTask = sut.DisposeAsync().AsTask();
		await Task.Delay(50);
		Assert.False(disposeCallbackInvoked.IsSet);

		scope.Dispose();

		await AwaitWithTimeoutAsync(disposeTask, "Disposal did not complete");

		// Assert
		await AwaitWithTimeoutAsync(disposeCallbackInvoked.WaitAsync(), "Dispose callback was not invoked");
		AssertDisposedState(sut, expectedInitCount: 1, expectedShutdownCount: 1, expectedDisposeCount: 1);
	}

	#endregion
}
