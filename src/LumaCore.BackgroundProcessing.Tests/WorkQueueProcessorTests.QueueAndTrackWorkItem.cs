// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using static LumaCore.BackgroundProcessing.Tests.AsyncTestHelpers;

namespace LumaCore.BackgroundProcessing.Tests;

/// <summary>
/// Unit tests for the trackable <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Func{CancellationToken, Task})"/>
/// and <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Action{CancellationToken})"/> methods.
/// </summary>
public partial class WorkQueueProcessorTests
{
	#region QueueAndTrackWorkItem(Func<CancellationToken, Task>) - Normal Operation

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Func{CancellationToken, Task})"/>
	/// returns a non-null task when work item is successfully queued.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_WithValidWorkItem_ReturnsTask()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act
		Task? result = service.QueueAndTrackWorkItem(_ => Task.CompletedTask);

		// Assert
		Assert.NotNull(result);
	}

	/// <summary>
	/// Verifies that the returned task completes when the work item completes.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_WhenQueued_ReturnedTaskCompletesWithWorkItem()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act
		Task? task = service.QueueAndTrackWorkItem(_ => Task.CompletedTask);

		// Assert
		Assert.NotNull(task);
		await AwaitWithTimeoutAsync(task, "Work item task did not complete", TimeSpan.FromSeconds(5));
	}

	/// <summary>
	/// Verifies that queued async work items are actually executed.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_WhenQueued_ExecutesWorkItem()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		var executed = new TaskCompletionSource<bool>();

		// Act
		Task? task = service.QueueAndTrackWorkItem(_ =>
		{
			executed.SetResult(true);
			return Task.CompletedTask;
		});

		// Assert
		Assert.NotNull(task);
		bool result = await AwaitWithTimeoutAsync(
			              executed.Task,
			              "Work item execution timed out",
			              TimeSpan.FromSeconds(5));
		Assert.True(result);
		Assert.True(task.IsCompletedSuccessfully);
	}

	/// <summary>
	/// Verifies that awaiting the returned task propagates exceptions from the work item.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_WhenWorkItemThrows_ReturnedTaskPropagatesException()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act
		Task? task = service.QueueAndTrackWorkItem(_ => throw new InvalidOperationException("Test exception"));

		// Assert
		Assert.NotNull(task);
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
		Assert.Equal("Test exception", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="OperationCanceledException"/> thrown by work items is propagated.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_WhenWorkItemThrowsOperationCanceledException_PropagatesCancellation()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act
		Task? task = service.QueueAndTrackWorkItem(_ => throw new OperationCanceledException("Test cancellation"));

		// Assert
		Assert.NotNull(task);
		// Use ThrowsAnyAsync because TaskCanceledException derives from OperationCanceledException
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
	}

	/// <summary>
	/// Verifies that the cancellation token passed to work items is functional.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_WorkItemReceivesCancellationToken()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		CancellationToken? receivedToken = null;

		// Act
		Task? task = service.QueueAndTrackWorkItem(ct =>
		{
			receivedToken = ct;
			return Task.CompletedTask;
		});

		// Assert
		Assert.NotNull(task);
		await AwaitWithTimeoutAsync(task, "Work item did not complete", TimeSpan.FromSeconds(5));
		Assert.NotNull(receivedToken);
		Assert.False(receivedToken.Value.IsCancellationRequested);
	}

	/// <summary>
	/// Verifies that multiple tracked work items can be awaited independently.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_MultipleItems_CanBeAwaitedIndependently()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		var tasks = new List<Task>();

		// Act
		for (int i = 0; i < 5; i++)
		{
			Task? task = service.QueueAndTrackWorkItem(_ => Task.CompletedTask);
			Assert.NotNull(task);
			tasks.Add(task);
		}

		// Assert - all tasks should complete
		await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
		Assert.All(tasks, t => Assert.True(t.IsCompletedSuccessfully));
	}

	#endregion

	#region QueueAndTrackWorkItem(Func<CancellationToken, Task>) - Error Cases

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Func{CancellationToken, Task})"/>
	/// throws <see cref="ArgumentNullException"/> when work item is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_WithNull_ThrowsArgumentNullException()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
		{
			Func<CancellationToken, Task>? nullWorkItem = null;
			service.QueueAndTrackWorkItem(nullWorkItem!);
		});

		Assert.Equal("workItem", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Func{CancellationToken, Task})"/>
	/// throws <see cref="InvalidOperationException"/> when service is not initialized.
	/// </summary>
	[Fact]
	public void QueueAndTrackWorkItemFunc_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act + Assert
		Assert.Throws<InvalidOperationException>(() =>
		{
			Func<CancellationToken, Task> workItem = _ => Task.CompletedTask;
			service.QueueAndTrackWorkItem(workItem);
		});
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Func{CancellationToken, Task})"/>
	/// throws <see cref="ObjectDisposedException"/> when service is disposed.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();
		await service.DisposeAsync();

		// Act + Assert
		Assert.Throws<ObjectDisposedException>(() =>
		{
			Func<CancellationToken, Task> workItem = _ => Task.CompletedTask;
			service.QueueAndTrackWorkItem(workItem);
		});
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Func{CancellationToken, Task})"/>
	/// returns <see langword="null"/> when queue is full.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemFunc_WhenQueueFull_ReturnsNull()
	{
		// Arrange - create service with very small queue
		await using var service = new WorkQueueProcessor(loggerFactory: LoggerFactory, maxQueueSize: 2);
		await service.InitializeAsync();

		// Block processing so queue fills up
		var blockProcessing = new TaskCompletionSource<bool>();
		service.QueueWorkItem(async _ => await blockProcessing.Task);

		// Wait for blocking item to be picked up
		await Task.Delay(50);

		// Fill the queue
		service.QueueWorkItem(_ => Task.CompletedTask);
		service.QueueWorkItem(_ => Task.CompletedTask);

		// Act - try to add one more with tracking
		Task? result = service.QueueAndTrackWorkItem(_ => Task.CompletedTask);

		// Assert
		Assert.Null(result);

		// Cleanup
		blockProcessing.SetResult(true);
	}

	#endregion

	#region QueueAndTrackWorkItem(Action<CancellationToken>) - Normal Operation

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Action{CancellationToken})"/>
	/// returns a non-null task when work item is successfully queued.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemAction_WithValidWorkItem_ReturnsTask()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act
		Task? result = service.QueueAndTrackWorkItem(_ => { });

		// Assert
		Assert.NotNull(result);
	}

	/// <summary>
	/// Verifies that the returned task completes when the synchronous work item completes.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemAction_WhenQueued_ReturnedTaskCompletesWithWorkItem()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act
		Task? task = service.QueueAndTrackWorkItem(_ => { });

		// Assert
		Assert.NotNull(task);
		await AwaitWithTimeoutAsync(task, "Work item task did not complete", TimeSpan.FromSeconds(5));
	}

	/// <summary>
	/// Verifies that queued synchronous work items are actually executed.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemAction_WhenQueued_ExecutesWorkItem()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		bool executed = false;

		// Act
		Task? task = service.QueueAndTrackWorkItem(_ => executed = true);

		// Assert
		Assert.NotNull(task);
		await AwaitWithTimeoutAsync(task, "Work item task did not complete", TimeSpan.FromSeconds(5));
		Assert.True(executed);
	}

	/// <summary>
	/// Verifies that awaiting the returned task propagates exceptions from synchronous work items.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemAction_WhenWorkItemThrows_ReturnedTaskPropagatesException()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act
		Task? task = service.QueueAndTrackWorkItem(_ => throw new InvalidOperationException("Test exception"));

		// Assert
		Assert.NotNull(task);
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
		Assert.Equal("Test exception", ex.Message);
	}

	#endregion

	#region QueueAndTrackWorkItem(Action<CancellationToken>) - Error Cases

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Action{CancellationToken})"/>
	/// throws <see cref="ArgumentNullException"/> when work item is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemAction_WithNull_ThrowsArgumentNullException()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
		{
			Action<CancellationToken>? nullWorkItem = null;
			service.QueueAndTrackWorkItem(nullWorkItem!);
		});

		Assert.Equal("workItem", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Action{CancellationToken})"/>
	/// throws <see cref="InvalidOperationException"/> when service is not initialized.
	/// </summary>
	[Fact]
	public void QueueAndTrackWorkItemAction_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act + Assert
		Assert.Throws<InvalidOperationException>(() =>
		{
			Action<CancellationToken> workItem = _ => { };
			service.QueueAndTrackWorkItem(workItem);
		});
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueAndTrackWorkItem(Action{CancellationToken})"/>
	/// throws <see cref="ObjectDisposedException"/> when service is disposed.
	/// </summary>
	[Fact]
	public async Task QueueAndTrackWorkItemAction_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();
		await service.DisposeAsync();

		// Act + Assert
		Assert.Throws<ObjectDisposedException>(() =>
		{
			Action<CancellationToken> workItem = _ => { };
			service.QueueAndTrackWorkItem(workItem);
		});
	}

	#endregion
}
