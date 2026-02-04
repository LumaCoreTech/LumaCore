// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using static LumaCore.BackgroundProcessing.Tests.AsyncTestHelpers;

namespace LumaCore.BackgroundProcessing.Tests;

// Unit tests for the lifecycle methods of WorkQueueProcessor
// (InitializeAsync, ShutdownAsync, DisposeAsync, and QueuedItemCount).
public partial class WorkQueueProcessorTests
{
	#region Re-Initialization Tests

	/// <summary>
	/// Verifies that rapid init-shutdown-init cycles work correctly.
	/// </summary>
	[Fact]
	public async Task Lifecycle_RapidInitShutdownCycles_WorksCorrectly()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act - perform multiple init-shutdown cycles
		for (int i = 0; i < 3; i++)
		{
			await service.InitializeAsync();
			Assert.True(service.IsInitialized);

			// Queue and execute a work item to verify functionality
			var executed = new TaskCompletionSource<bool>();
			_ = service.QueueWorkItem(_ =>
			{
				executed.SetResult(true);
				return Task.CompletedTask;
			});

			await AwaitWithTimeoutAsync(executed.Task, "Work item should execute within timeout");

			await service.ShutdownAsync();
			Assert.False(service.IsInitialized);
		}

		// Cleanup
		await service.DisposeAsync();
	}

	#endregion

	#region InitializeAsync

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.InitializeAsync"/> successfully initializes the service.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenNotInitialized_InitializesService()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act
		await service.InitializeAsync();

		// Assert
		Assert.True(service.IsInitialized);

		// Cleanup
		await service.DisposeAsync();
	}

	/// <summary>
	/// Verifies that after <see cref="WorkQueueProcessor.InitializeAsync"/>,
	/// the service accepts work items.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_AfterInitialization_AcceptsWorkItems()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		var executed = new TaskCompletionSource<bool>();

		// Act
		Task? queued = service.QueueAndTrackWorkItem(_ =>
		{
			executed.SetResult(true);
			return Task.CompletedTask;
		});

		// Assert
		Assert.NotNull(queued);
		bool result = await executed.Task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that calling <see cref="WorkQueueProcessor.InitializeAsync"/> twice
	/// throws <see cref="InvalidOperationException"/>.
	/// </summary>
	[Fact]
	public async Task InitializeAsync_WhenAlreadyInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync());
	}

	#endregion

	#region ShutdownAsync

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.ShutdownAsync"/> gracefully shuts down the service.
	/// </summary>
	[Fact]
	public async Task ShutdownAsync_WhenInitialized_ShutsDownGracefully()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();

		// Act
		await service.ShutdownAsync();

		// Assert
		Assert.False(service.IsInitialized);

		// Cleanup
		await service.DisposeAsync();
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.ShutdownAsync"/> waits for queued items to complete.
	/// </summary>
	[Fact]
	public async Task ShutdownAsync_WithPendingWorkItems_WaitsForCompletion()
	{
		// Arrange
		await using var service = new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			shutdownTimeout: TimeSpan.FromSeconds(10));
		await service.InitializeAsync();

		var workItemStarted = new TaskCompletionSource<bool>();
		var workItemCanComplete = new TaskCompletionSource<bool>();
		bool workItemCompleted = false;

		_ = service.QueueWorkItem(async _ =>
		{
			workItemStarted.SetResult(true);
			await workItemCanComplete.Task;
			workItemCompleted = true;
		});

		// Wait for work item to start
		await workItemStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Act
		Task shutdownTask = service.ShutdownAsync();

		// Allow work item to complete
		workItemCanComplete.SetResult(true);

		await shutdownTask;

		// Assert
		Assert.True(workItemCompleted);
		Assert.False(service.IsInitialized);
	}

	/// <summary>
	/// Verifies that after <see cref="WorkQueueProcessor.ShutdownAsync"/>,
	/// the service can be re-initialized.
	/// </summary>
	[Fact]
	public async Task ShutdownAsync_AfterShutdown_CanBeReinitialized()
	{
		// Arrange
		await using var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();
		await service.ShutdownAsync();
		Assert.False(service.IsInitialized);

		// Act
		await service.InitializeAsync();

		// Assert
		Assert.True(service.IsInitialized);

		// Verify service is functional after re-initialization.
		var executed = new TaskCompletionSource<bool>();
		Task? queued = service.QueueAndTrackWorkItem(_ =>
		{
			executed.SetResult(true);
			return Task.CompletedTask;
		});
		Assert.NotNull(queued);
		bool result = await executed.Task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.ShutdownAsync"/> on a never-initialized service
	/// completes without error.
	/// </summary>
	[Fact]
	public async Task ShutdownAsync_WhenNeverInitialized_CompletesWithoutError()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act + Assert - should not throw
		await service.ShutdownAsync();

		// Cleanup
		await service.DisposeAsync();
	}

	/// <summary>
	/// Verifies that when shutdown timeout is reached, running work items are still awaited
	/// but queued items are discarded.
	/// </summary>
	[Fact]
	public async Task ShutdownAsync_WhenTimeoutReached_DiscardsQueuedItemsButWaitsForRunning()
	{
		// Arrange - use a very short shutdown timeout
		await using var service = new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			shutdownTimeout: TimeSpan.FromMilliseconds(50));
		await service.InitializeAsync();

		var runningWorkItemStarted = new TaskCompletionSource<bool>();
		var runningWorkItemCanComplete = new TaskCompletionSource<bool>();
		bool runningWorkItemCompleted = false;
		bool queuedWorkItemExecuted = false;

		// Queue a work item that will be running when timeout occurs.
		service.QueueWorkItem(async _ =>
		{
			runningWorkItemStarted.SetResult(true);
			await runningWorkItemCanComplete.Task;
			runningWorkItemCompleted = true;
		});

		// Wait for it to start running.
		await runningWorkItemStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Queue additional items that should be discarded after timeout.
		service.QueueWorkItem(_ =>
		{
			queuedWorkItemExecuted = true;
			return Task.CompletedTask;
		});

		// Act - start shutdown (will timeout because running work item is blocked)
		Task shutdownTask = service.ShutdownAsync();

		// Wait a bit for the timeout to be reached, then let the running work item complete.
		await Task.Delay(200);
		runningWorkItemCanComplete.SetResult(true);

		await shutdownTask;

		// Assert
		Assert.True(runningWorkItemCompleted, "Running work item should complete even after timeout");
		Assert.False(queuedWorkItemExecuted, "Queued work item should be discarded after timeout");
		Assert.False(service.IsInitialized);
	}

	/// <summary>
	/// Verifies that tracked work items discarded during shutdown timeout have their tasks cancelled.
	/// </summary>
	[Fact]
	public async Task ShutdownAsync_WhenTimeoutReached_CancelsDiscardedTrackedItems()
	{
		// Arrange - use a very short shutdown timeout
		await using var service = new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			shutdownTimeout: TimeSpan.FromMilliseconds(50));
		await service.InitializeAsync();

		var runningWorkItemStarted = new TaskCompletionSource<bool>();
		var runningWorkItemCanComplete = new TaskCompletionSource<bool>();

		// Queue a work item that will be running when timeout occurs.
		service.QueueWorkItem(async _ =>
		{
			runningWorkItemStarted.SetResult(true);
			await runningWorkItemCanComplete.Task;
		});

		// Wait for it to start running.
		await runningWorkItemStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Queue a TRACKED item that should be discarded and cancelled after timeout.
		Task? trackedTask = service.QueueAndTrackWorkItem(_ => Task.CompletedTask);
		Assert.NotNull(trackedTask);

		// Act - start shutdown (will timeout because running work item is blocked)
		Task shutdownTask = service.ShutdownAsync();

		// Wait a bit for the timeout to be reached, then let the running work item complete.
		await Task.Delay(200);
		runningWorkItemCanComplete.SetResult(true);

		await shutdownTask;

		// Assert - the tracked task should be cancelled (not completed successfully)
		Assert.True(trackedTask.IsCanceled, "Discarded tracked work item should have cancelled task");
	}

	/// <summary>
	/// Verifies that multiple concurrent calls to <see cref="WorkQueueProcessor.ShutdownAsync"/>
	/// complete without errors (idempotent).
	/// </summary>
	[Fact]
	public async Task ShutdownAsync_CalledMultipleTimes_IsIdempotent()
	{
		// Arrange
		await using var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();

		// Act
		Task shutdown1 = service.ShutdownAsync();
		Task shutdown2 = service.ShutdownAsync();
		Task shutdown3 = service.ShutdownAsync();

		await Task.WhenAll(shutdown1, shutdown2, shutdown3);

		// Assert
		Assert.False(service.IsInitialized);
	}

	#endregion

	#region DisposeAsync

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.DisposeAsync"/> disposes the service cleanly.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_WhenInitialized_DisposesCleanly()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();

		// Act
		await service.DisposeAsync();

		// Assert - accessing service after dispose should throw
		Assert.Throws<ObjectDisposedException>(() =>
		{
			static Task WorkItem(CancellationToken _) => Task.CompletedTask;
			_ = service.QueueWorkItem((Func<CancellationToken, Task>)WorkItem);
		});
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.DisposeAsync"/> waits for work items before disposing.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_WithPendingWorkItems_WaitsForCompletion()
	{
		// Arrange
		var service = new WorkQueueProcessor(loggerFactory: LoggerFactory, shutdownTimeout: TimeSpan.FromSeconds(10));
		await service.InitializeAsync();

		var workItemCompleted = new TaskCompletionSource<bool>();

		_ = service.QueueWorkItem(async _ =>
		{
			await Task.Delay(100, CancellationToken.None);
			workItemCompleted.SetResult(true);
		});

		// Act
		await service.DisposeAsync();

		// Assert
		Assert.True(workItemCompleted.Task.IsCompletedSuccessfully);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.DisposeAsync"/> can be called multiple times.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_CalledMultipleTimes_IsIdempotent()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();

		// Act & Assert - should not throw
		await service.DisposeAsync();
		await service.DisposeAsync();
		await service.DisposeAsync();
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.DisposeAsync"/> works on a non-initialized service.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_WhenNotInitialized_DisposesCleanly()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act & Assert - should not throw
		await service.DisposeAsync();
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.DisposeAsync"/> immediately after creating
	/// (without initialization) works.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_ImmediatelyAfterConstruction_Succeeds()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act & Assert - should not throw
		await service.DisposeAsync();
	}

	#endregion

	#region QueuedItemCount

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueuedItemCount"/> returns 0 when not initialized.
	/// </summary>
	[Fact]
	public void QueuedItemCount_WhenNotInitialized_ReturnsZero()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act
		int count = service.QueuedItemCount;

		// Assert
		Assert.Equal(0, count);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueuedItemCount"/> reflects queued items.
	/// </summary>
	[Fact]
	public async Task QueuedItemCount_WithQueuedItems_ReflectsCount()
	{
		// Arrange
		await using var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();

		var blockWorkItems = new TaskCompletionSource<bool>();

		// Queue a blocking work item to prevent processing.
		_ = service.QueueWorkItem(async _ => await blockWorkItems.Task);

		// Wait a bit for the first item to be picked up.
		await Task.Delay(50);

		// Queue additional items that will wait in the queue.
		_ = service.QueueWorkItem(_ => Task.CompletedTask);
		_ = service.QueueWorkItem(_ => Task.CompletedTask);

		// Act
		int count = service.QueuedItemCount;

		// Assert
		Assert.True(count >= 2, $"Expected at least 2 queued items, but got {count}");

		// Cleanup
		blockWorkItems.SetResult(true);
	}

	#endregion
}
