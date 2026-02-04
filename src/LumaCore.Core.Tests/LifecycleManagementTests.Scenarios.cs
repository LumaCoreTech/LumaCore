// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable MethodSupportsCancellation

namespace LumaCore.Core.Tests;

public partial class LifecycleManagementTests
{
	#region Complete Lifecycle

	/// <summary>
	/// Verifies the complete lifecycle flow: <see cref="LifecycleManagement.InitializeAsync"/> →
	/// <see cref="LifecycleManagement.BeginAsyncOperation"/> → <see cref="LifecycleManagement.ShutdownAsync"/> →
	/// <see cref="LifecycleManagement.DisposeAsync()"/>.
	/// </summary>
	[Fact]
	public async Task Lifecycle_InitializeOperateShutdownDispose_CompletesSuccessfully()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();

		// Act - Initialize
		await sut.InitializeAsync();
		AssertInitializedState(sut);

		// Act - Operation
		using (sut.BeginAsyncOperation())
		{
			lock (sut.LifecycleState.Sync)
			{
				Assert.Equal(1, sut.LifecycleState.PendingOperationCount);
			}
		}

		// Act - Shutdown
		await sut.ShutdownAsync();
		AssertShutdownState(sut);

		// Act - Dispose
		await sut.DisposeAsync();
		AssertDisposedState(sut, expectedInitCount: 1, expectedShutdownCount: 1, expectedDisposeCount: 1);
	}

	/// <summary>
	/// Verifies the minimal lifecycle:
	/// <see cref="LifecycleManagement.InitializeAsync"/> → <see cref="LifecycleManagement.DisposeAsync()"/>
	/// (skipping explicit shutdown). <see cref="LifecycleManagement.OnShuttingDownAsync"/> is called
	/// automatically by dispose.
	/// </summary>
	[Fact]
	public async Task Lifecycle_InitializeAndDispose_ShutdownCalledAutomatically()
	{
		// Arrange
		var callOrder = new List<string>();
		TestableLifecycleManagement sut = CreateSut(
			onShuttingDownCallback: _ =>
			{
				callOrder.Add("Shutdown");
				return Task.CompletedTask;
			},
			onDisposingCallback: _ =>
			{
				callOrder.Add("Dispose");
				return Task.CompletedTask;
			});

		// Act
		await sut.InitializeAsync();
		await sut.DisposeAsync();

		// Assert
		Assert.Equal(["Shutdown", "Dispose"], callOrder);
		AssertDisposedState(sut, expectedInitCount: 1, expectedShutdownCount: 1, expectedDisposeCount: 1);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.DisposeAsync()"/> without prior
	/// <see cref="LifecycleManagement.InitializeAsync"/> skips <see cref="LifecycleManagement.OnShuttingDownAsync"/>.
	/// </summary>
	[Fact]
	public async Task Lifecycle_DisposeWithoutInitialize_SkipsShutdown()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();

		// Act
		await sut.DisposeAsync();

		// Assert
		AssertDisposedState(sut, expectedInitCount: 0, expectedShutdownCount: 0, expectedDisposeCount: 1);
	}

	#endregion

	#region Re-Initialization

	/// <summary>
	/// Verifies the lifecycle can be restarted after <see cref="LifecycleManagement.ShutdownAsync"/>:
	/// Initialize → Shutdown → Initialize → Shutdown → Dispose.
	/// </summary>
	[Fact]
	public async Task Lifecycle_ReinitializeAfterShutdown_CompletesSuccessfully()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();

		// Act - First cycle
		await sut.InitializeAsync();
		AssertInitializedState(sut, expectedInitCount: 1, expectedShutdownCount: 0);

		await sut.ShutdownAsync();
		AssertShutdownState(sut, expectedInitCount: 1, expectedShutdownCount: 1);

		// Act - Second cycle
		await sut.InitializeAsync();
		AssertInitializedState(sut, expectedInitCount: 2, expectedShutdownCount: 1);

		await sut.ShutdownAsync();
		AssertShutdownState(sut, expectedInitCount: 2, expectedShutdownCount: 2);

		// Act - Dispose
		await sut.DisposeAsync();
		AssertDisposedState(sut, expectedInitCount: 2, expectedShutdownCount: 2, expectedDisposeCount: 1);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleState.ShutdownToken"/> is reset after re-initialization.
	/// </summary>
	[Fact]
	public async Task Lifecycle_ReinitializeAfterShutdown_ResetsShutdownToken()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();

		// Act - First cycle
		await sut.InitializeAsync();
		CancellationToken tokenBeforeShutdown;
		lock (sut.LifecycleState.Sync)
		{
			tokenBeforeShutdown = sut.LifecycleState.ShutdownToken;
		}

		await sut.ShutdownAsync();
		Assert.True(tokenBeforeShutdown.IsCancellationRequested);

		// Act - Re-initialize
		await sut.InitializeAsync();

		// Assert
		CancellationToken tokenAfterReinit;
		lock (sut.LifecycleState.Sync)
		{
			tokenAfterReinit = sut.LifecycleState.ShutdownToken;
		}

		Assert.False(tokenAfterReinit.IsCancellationRequested);
		AssertInitializedState(sut, expectedInitCount: 2, expectedShutdownCount: 1);
	}

	#endregion

	#region Operations During Lifecycle

	/// <summary>
	/// Verifies that multiple <see cref="LifecycleManagement.BeginAsyncOperation"/> scopes can be active concurrently.
	/// </summary>
	[Fact]
	public async Task Lifecycle_MultipleOperationsDuringInitialized_TracksAllOperations()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.InitializeAsync();

		// Act
		using LifecycleManagement.AsyncOperationScope scope1 = sut.BeginAsyncOperation();
		using LifecycleManagement.AsyncOperationScope scope2 = sut.BeginAsyncOperation();
		using LifecycleManagement.AsyncOperationScope scope3 = sut.BeginAsyncOperation();

		// Assert
		lock (sut.LifecycleState.Sync)
		{
			Assert.Equal(3, sut.LifecycleState.PendingOperationCount);
		}

		AssertInitializedState(sut);
	}

	/// <summary>
	/// Verifies that <see cref="LifecycleManagement.BeginOperation"/> scopes are tracked correctly.
	/// </summary>
	[Fact]
	public async Task Lifecycle_SyncOperationDuringInitialized_TracksOperation()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.InitializeAsync();

		// Act + Assert
		using (sut.BeginOperation())
		{
			lock (sut.LifecycleState.Sync)
			{
				Assert.Equal(1, sut.LifecycleState.PendingOperationCount);
			}
		}

		lock (sut.LifecycleState.Sync)
		{
			Assert.Equal(0, sut.LifecycleState.PendingOperationCount);
		}

		AssertInitializedState(sut);
	}

	/// <summary>
	/// Verifies that disposing an <see cref="LifecycleManagement.AsyncOperationScope"/> multiple times is safe.
	/// </summary>
	[Fact]
	public async Task Lifecycle_DisposeAsyncOperationScopeMultipleTimes_OnlyDecrementsOnce()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.InitializeAsync();

		// Act
		LifecycleManagement.AsyncOperationScope scope = sut.BeginAsyncOperation();
		scope.Dispose();
		scope.Dispose();

		// Assert
		lock (sut.LifecycleState.Sync)
		{
			Assert.Equal(0, sut.LifecycleState.PendingOperationCount);
		}

		AssertInitializedState(sut);
	}

	/// <summary>
	/// Verifies that disposing an <see cref="LifecycleManagement.OperationScope"/> multiple times is safe.
	/// </summary>
	[Fact]
	public async Task Lifecycle_DisposeSyncOperationScopeMultipleTimes_OnlyDecrementsOnce()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.InitializeAsync();

		// Act
		LifecycleManagement.OperationScope scope = sut.BeginOperation();
		scope.Dispose();
		scope.Dispose();

		// Assert
		lock (sut.LifecycleState.Sync)
		{
			Assert.Equal(0, sut.LifecycleState.PendingOperationCount);
		}

		AssertInitializedState(sut);
	}

	#endregion

	#region Idempotent Operations

	/// <summary>
	/// Verifies that calling <see cref="LifecycleManagement.ShutdownAsync"/> multiple times is idempotent.
	/// </summary>
	[Fact]
	public async Task Lifecycle_MultipleShutdownCalls_OnlyExecutesOnce()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.InitializeAsync();

		// Act
		await sut.ShutdownAsync();
		await sut.ShutdownAsync();
		await sut.ShutdownAsync();

		// Assert
		AssertShutdownState(sut, expectedInitCount: 1, expectedShutdownCount: 1);
	}

	/// <summary>
	/// Verifies that calling <see cref="LifecycleManagement.DisposeAsync()"/> multiple times is idempotent.
	/// </summary>
	[Fact]
	public async Task Lifecycle_MultipleDisposeCalls_OnlyExecutesOnce()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.InitializeAsync();

		// Act
		await sut.DisposeAsync();
		await sut.DisposeAsync();
		await sut.DisposeAsync();

		// Assert
		AssertDisposedState(sut, expectedInitCount: 1, expectedShutdownCount: 1, expectedDisposeCount: 1);
	}

	/// <summary>
	/// Verifies that synchronous <see cref="IDisposable.Dispose"/> works correctly.
	/// </summary>
	[Fact]
	public async Task Lifecycle_SyncDispose_CompletesSuccessfully()
	{
		// Arrange
		TestableLifecycleManagement sut = CreateSut();
		await sut.InitializeAsync();

		// Act
		sut.Dispose();

		// Assert
		AssertDisposedState(sut, expectedInitCount: 1, expectedShutdownCount: 1, expectedDisposeCount: 1);
	}

	#endregion
}
