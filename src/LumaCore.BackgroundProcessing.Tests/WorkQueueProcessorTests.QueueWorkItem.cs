// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using static LumaCore.BackgroundProcessing.Tests.AsyncTestHelpers;

namespace LumaCore.BackgroundProcessing.Tests;

/// <summary>
/// Unit tests for the fire-and-forget <see cref="WorkQueueProcessor.QueueWorkItem(Func{CancellationToken, Task})"/>
/// and <see cref="WorkQueueProcessor.QueueWorkItem(Action{CancellationToken})"/> methods.
/// </summary>
public partial class WorkQueueProcessorTests
{
	#region QueueWorkItem(Func<CancellationToken, Task>) - Normal Operation

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueWorkItem(Func{CancellationToken, Task})"/>
	/// returns <see langword="true"/> when work item is successfully queued.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemFunc_WithValidWorkItem_ReturnsTrue()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act
		bool result = service.QueueWorkItem(_ => Task.CompletedTask);

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that queued async work items are actually executed.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemFunc_WhenQueued_ExecutesWorkItem()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		var executed = new TaskCompletionSource<bool>();

		// Act
		bool queued = service.QueueWorkItem(_ =>
		{
			executed.SetResult(true);
			return Task.CompletedTask;
		});

		// Assert
		Assert.True(queued);
		bool result = await AwaitWithTimeoutAsync(
			              executed.Task,
			              "Work item execution timed out",
			              TimeSpan.FromSeconds(5));
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that the cancellation token passed to work items is functional.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemFunc_WorkItemReceivesCancellationToken()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		CancellationToken? receivedToken = null;
		var executed = new TaskCompletionSource<bool>();

		// Act
		service.QueueWorkItem(ct =>
		{
			receivedToken = ct;
			executed.SetResult(true);
			return Task.CompletedTask;
		});

		await AwaitWithTimeoutAsync(executed.Task, "Work item execution timed out", TimeSpan.FromSeconds(5));

		// Assert
		Assert.NotNull(receivedToken);
		Assert.False(receivedToken.Value.IsCancellationRequested);
	}

	/// <summary>
	/// Verifies that multiple work items can be queued and executed.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemFunc_MultipleItems_AllAreExecuted()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		int executedCount = 0;
		var allExecuted = new TaskCompletionSource<bool>();

		// Act
		for (int i = 0; i < 10; i++)
		{
			service.QueueWorkItem(_ =>
			{
				if (Interlocked.Increment(ref executedCount) == 10)
					allExecuted.SetResult(true);
				return Task.CompletedTask;
			});
		}

		// Assert
		await AwaitWithTimeoutAsync(allExecuted.Task, "Not all work items executed in time", TimeSpan.FromSeconds(5));
		Assert.Equal(10, executedCount);
	}

	/// <summary>
	/// Verifies that exceptions in async work items do not crash the service
	/// and subsequent work items are still processed.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemFunc_WhenWorkItemThrows_ContinuesProcessing()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		var secondItemExecuted = new TaskCompletionSource<bool>();

		// Act - queue a failing item, then a successful one
		service.QueueWorkItem(_ => throw new InvalidOperationException("Test exception"));
		service.QueueWorkItem(_ =>
		{
			secondItemExecuted.SetResult(true);
			return Task.CompletedTask;
		});

		// Assert - second item should still execute
		bool result = await AwaitWithTimeoutAsync(
			              secondItemExecuted.Task,
			              "Second work item did not execute after exception",
			              TimeSpan.FromSeconds(5));
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that <see cref="OperationCanceledException"/> thrown by work items
	/// (not due to shutdown) does not crash the service.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemFunc_WhenWorkItemThrowsOperationCanceledException_ContinuesProcessing()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		var secondItemExecuted = new TaskCompletionSource<bool>();

		// Act - queue a cancellation throwing item, then a successful one
		service.QueueWorkItem(_ => throw new OperationCanceledException("Test cancellation"));
		service.QueueWorkItem(_ =>
		{
			secondItemExecuted.SetResult(true);
			return Task.CompletedTask;
		});

		// Assert - second item should still execute
		bool result = await AwaitWithTimeoutAsync(
			              secondItemExecuted.Task,
			              "Second work item did not execute after OperationCanceledException",
			              TimeSpan.FromSeconds(5));
		Assert.True(result);
	}

	#endregion

	#region QueueWorkItem(Func<CancellationToken, Task>) - Error Cases

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueWorkItem(Func{CancellationToken, Task})"/>
	/// throws <see cref="ArgumentNullException"/> when work item is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemFunc_WithNull_ThrowsArgumentNullException()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
		{
			Func<CancellationToken, Task>? nullWorkItem = null;
			service.QueueWorkItem(nullWorkItem!);
		});

		Assert.Equal("workItem", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueWorkItem(Func{CancellationToken, Task})"/>
	/// throws <see cref="InvalidOperationException"/> when service is not initialized.
	/// </summary>
	[Fact]
	public void QueueWorkItemFunc_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act + Assert
		Assert.Throws<InvalidOperationException>(() =>
		{
			Func<CancellationToken, Task> workItem = _ => Task.CompletedTask;
			service.QueueWorkItem(workItem);
		});
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueWorkItem(Func{CancellationToken, Task})"/>
	/// throws <see cref="ObjectDisposedException"/> when service is disposed.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemFunc_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();
		await service.DisposeAsync();

		// Act + Assert
		Assert.Throws<ObjectDisposedException>(() =>
		{
			Func<CancellationToken, Task> workItem = _ => Task.CompletedTask;
			service.QueueWorkItem(workItem);
		});
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueWorkItem(Func{CancellationToken, Task})"/>
	/// returns <see langword="false"/> when queue is full.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemFunc_WhenQueueFull_ReturnsFalse()
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

		// Act - try to add one more
		bool result = service.QueueWorkItem(_ => Task.CompletedTask);

		// Assert
		Assert.False(result);

		// Cleanup
		blockProcessing.SetResult(true);
	}

	#endregion

	#region QueueWorkItem(Action<CancellationToken>) - Normal Operation

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueWorkItem(Action{CancellationToken})"/>
	/// returns <see langword="true"/> when work item is successfully queued.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemAction_WithValidWorkItem_ReturnsTrue()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act
		bool result = service.QueueWorkItem(_ => { });

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that queued synchronous work items are actually executed.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemAction_WhenQueued_ExecutesWorkItem()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		var executed = new TaskCompletionSource<bool>();

		// Act
		bool queued = service.QueueWorkItem(_ => executed.SetResult(true));

		// Assert
		Assert.True(queued);
		bool result = await AwaitWithTimeoutAsync(
			              executed.Task,
			              "Work item execution timed out",
			              TimeSpan.FromSeconds(5));
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that the cancellation token passed to synchronous work items is functional.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemAction_WorkItemReceivesCancellationToken()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		CancellationToken? receivedToken = null;
		var executed = new TaskCompletionSource<bool>();

		// Act
		service.QueueWorkItem(ct =>
		{
			receivedToken = ct;
			executed.SetResult(true);
		});

		await AwaitWithTimeoutAsync(executed.Task, "Work item execution timed out", TimeSpan.FromSeconds(5));

		// Assert
		Assert.NotNull(receivedToken);
		Assert.False(receivedToken.Value.IsCancellationRequested);
	}

	/// <summary>
	/// Verifies that exceptions in synchronous work items do not crash the service
	/// and subsequent work items are still processed.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemAction_WhenWorkItemThrows_ContinuesProcessing()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);
		var secondItemExecuted = new TaskCompletionSource<bool>();

		// Act - queue a failing item, then a successful one
		service.QueueWorkItem(_ => throw new InvalidOperationException("Test exception"));
		service.QueueWorkItem(_ => secondItemExecuted.SetResult(true));

		// Assert - second item should still execute
		bool result = await AwaitWithTimeoutAsync(
			              secondItemExecuted.Task,
			              "Second work item did not execute after exception",
			              TimeSpan.FromSeconds(5));
		Assert.True(result);
	}

	#endregion

	#region QueueWorkItem(Action<CancellationToken>) - Error Cases

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueWorkItem(Action{CancellationToken})"/>
	/// throws <see cref="ArgumentNullException"/> when work item is <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemAction_WithNull_ThrowsArgumentNullException()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
		{
			Action<CancellationToken>? nullWorkItem = null;
			service.QueueWorkItem(nullWorkItem!);
		});

		Assert.Equal("workItem", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueWorkItem(Action{CancellationToken})"/>
	/// throws <see cref="InvalidOperationException"/> when service is not initialized.
	/// </summary>
	[Fact]
	public void QueueWorkItemAction_WhenNotInitialized_ThrowsInvalidOperationException()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);

		// Act + Assert
		Assert.Throws<InvalidOperationException>(() =>
		{
			Action<CancellationToken> workItem = _ => { };
			service.QueueWorkItem(workItem);
		});
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.QueueWorkItem(Action{CancellationToken})"/>
	/// throws <see cref="ObjectDisposedException"/> when service is disposed.
	/// </summary>
	[Fact]
	public async Task QueueWorkItemAction_WhenDisposed_ThrowsObjectDisposedException()
	{
		// Arrange
		var service = new WorkQueueProcessor(LoggerFactory);
		await service.InitializeAsync();
		await service.DisposeAsync();

		// Act + Assert
		Assert.Throws<ObjectDisposedException>(() =>
		{
			Action<CancellationToken> workItem = _ => { };
			service.QueueWorkItem(workItem);
		});
	}

	#endregion
}
