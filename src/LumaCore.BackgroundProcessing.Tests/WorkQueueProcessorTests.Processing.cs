// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.Concurrent;
using System.Transactions;

using Xunit;

namespace LumaCore.BackgroundProcessing.Tests;

// Unit tests for the background processing behavior of WorkQueueProcessor,
// including sequential processing, parallel processing, and execution context isolation.
public partial class WorkQueueProcessorTests
{
	#region Sequential Processing (maxConcurrency = 1) - Normal Operation

	/// <summary>
	/// Verifies that with default concurrency (1), work items are processed in FIFO order.
	/// </summary>
	[Fact]
	public async Task Processing_WithSequentialConcurrency_ProcessesInFifoOrder()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(loggerFactory: LoggerFactory, maxConcurrency: 1);
		var executionOrder = new List<int>();
		var allCompleted = new TaskCompletionSource<bool>();
		const int itemCount = 5;

		// Act
		for (int i = 0; i < itemCount; i++)
		{
			int index = i;
			_ = service.QueueWorkItem(_ =>
			{
				lock (executionOrder)
				{
					executionOrder.Add(index);
					if (executionOrder.Count == itemCount)
						allCompleted.SetResult(true);
				}
				return Task.CompletedTask;
			});
		}

		// Wait for all items to complete.
		await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Assert
		Assert.Equal(itemCount, executionOrder.Count);
		for (int i = 0; i < itemCount; i++)
		{
			Assert.Equal(i, executionOrder[i]);
		}
	}

	/// <summary>
	/// Verifies that with sequential processing, only one work item runs at a time.
	/// </summary>
	[Fact]
	public async Task Processing_WithSequentialConcurrency_OnlyOneItemRunsAtATime()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(loggerFactory: LoggerFactory, maxConcurrency: 1);
		int maxConcurrent = 0;
		int currentConcurrent = 0;
		var allCompleted = new TaskCompletionSource<bool>();
		const int itemCount = 5;
		int completedCount = 0;

		// Act
		for (int i = 0; i < itemCount; i++)
		{
			_ = service.QueueWorkItem(async _ =>
			{
				// Increment concurrency count.
				int concurrent = Interlocked.Increment(ref currentConcurrent);

				// Update max concurrency observed.
				InterlockedMax(ref maxConcurrent, concurrent);

				// Introduce small delay to allow overlap detection.
				await Task.Delay(10, CancellationToken.None);

				// Decrement concurrency count.
				Interlocked.Decrement(ref currentConcurrent);

				// Assert completion, signal when all done.
				if (Interlocked.Increment(ref completedCount) == itemCount)
					allCompleted.SetResult(true);
			});
		}

		await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Assert
		Assert.Equal(1, maxConcurrent);
	}

	#endregion

	#region Parallel Processing (maxConcurrency > 1) - Normal Operation

	/// <summary>
	/// Verifies that with parallel concurrency, multiple work items can run simultaneously.
	/// </summary>
	[Fact]
	public async Task Processing_WithParallelConcurrency_AllowsMultipleConcurrentItems()
	{
		// Arrange
		const int maxConcurrency = 4;
		await using var service = await WorkQueueProcessor.CreateAsync(
			                          loggerFactory: LoggerFactory,
			                          maxConcurrency: maxConcurrency);
		int maxObservedConcurrency = 0;
		int currentConcurrency = 0;
		var allCompleted = new TaskCompletionSource<bool>();
		const int itemCount = 20;
		int completedCount = 0;

		// Act
		for (int i = 0; i < itemCount; i++)
		{
			_ = service.QueueWorkItem(async _ =>
			{
				// Increment concurrency count.
				int concurrent = Interlocked.Increment(ref currentConcurrency);

				// Update max observed concurrency.
				InterlockedMax(ref maxObservedConcurrency, concurrent);

				// Introduce delay to allow overlap.
				await Task.Delay(50, CancellationToken.None);

				// Decrement concurrency count.
				Interlocked.Decrement(ref currentConcurrency);

				// Signal completion when all done.
				if (Interlocked.Increment(ref completedCount) == itemCount)
					allCompleted.SetResult(true);
			});
		}

		await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

		// Assert - should observe more than 1 concurrent execution
		Assert.True(
			maxObservedConcurrency > 1,
			$"Expected concurrent execution but observed max {maxObservedConcurrency}");
		Assert.True(
			maxObservedConcurrency <= maxConcurrency,
			$"Concurrency exceeded limit: {maxObservedConcurrency} > {maxConcurrency}");
	}

	/// <summary>
	/// Verifies that parallel processing reaches and respects the configured concurrency limit.
	/// </summary>
	[Fact]
	public async Task Processing_WithParallelConcurrency_RespectsMaxConcurrency()
	{
		// Arrange
		const int maxConcurrency = 2;
		await using var service = await WorkQueueProcessor.CreateAsync(
			                          loggerFactory: LoggerFactory,
			                          maxConcurrency: maxConcurrency);
		int maxObservedConcurrency = 0;
		int currentConcurrency = 0;
		var allCompleted = new TaskCompletionSource<bool>();
		const int itemCount = 10;
		int completedCount = 0;

		// Act
		for (int i = 0; i < itemCount; i++)
		{
			_ = service.QueueWorkItem(async _ =>
			{
				// Increment concurrency count.
				int concurrent = Interlocked.Increment(ref currentConcurrency);

				// Update max observed concurrency.
				InterlockedMax(ref maxObservedConcurrency, concurrent);

				// Introduce delay to allow overlap.
				await Task.Delay(50, CancellationToken.None);

				// Decrement concurrency count.
				Interlocked.Decrement(ref currentConcurrency);

				// Assert completion, signal when all done.
				if (Interlocked.Increment(ref completedCount) == itemCount)
					allCompleted.SetResult(true);
			});
		}

		// Wait for all items to complete.
		await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

		// Assert - should reach exactly the configured concurrency limit
		Assert.Equal(maxConcurrency, maxObservedConcurrency);
	}

	/// <summary>
	/// Verifies that with parallel processing, all work items are eventually executed.
	/// </summary>
	[Fact]
	public async Task Processing_WithParallelConcurrency_AllItemsAreExecuted()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(loggerFactory: LoggerFactory, maxConcurrency: 4);
		var executedIds = new ConcurrentBag<int>();
		var allCompleted = new TaskCompletionSource<bool>();
		const int itemCount = 50;

		// Act
		for (int i = 0; i < itemCount; i++)
		{
			int id = i;
			_ = service.QueueWorkItem(async _ =>
			{
				await Task.Delay(10, CancellationToken.None);
				executedIds.Add(id);

				if (executedIds.Count == itemCount)
					allCompleted.SetResult(true);
			});
		}

		// Wait for all items to complete.
		await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

		// Assert
		Assert.Equal(itemCount, executedIds.Count);
		var sortedIds = new List<int>(executedIds);
		sortedIds.Sort();
		for (int i = 0; i < itemCount; i++)
		{
			Assert.Equal(i, sortedIds[i]);
		}
	}

	/// <summary>
	/// Verifies that parallel processing triggers periodic cleanup of completed tasks
	/// when the running tasks list exceeds the threshold.
	/// </summary>
	[Fact]
	public async Task Processing_WithParallelConcurrency_CleansUpCompletedTasks()
	{
		// Arrange - use concurrency of 2, threshold multiplier is 2, so cleanup at > 4 tasks
		const int maxConcurrency = 2;
		await using var service = await WorkQueueProcessor.CreateAsync(
			                          loggerFactory: LoggerFactory,
			                          maxConcurrency: maxConcurrency);
		const int itemCount = 20; // Much more than threshold to trigger cleanup
		var allCompleted = new TaskCompletionSource<bool>();
		int completedCount = 0;

		// Act
		for (int i = 0; i < itemCount; i++)
		{
			_ = service.QueueWorkItem(async _ =>
			{
				await Task.Delay(5, CancellationToken.None);
				if (Interlocked.Increment(ref completedCount) == itemCount)
					allCompleted.SetResult(true);
			});
		}

		// Wait for all items to complete.
		await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

		// Assert
		Assert.Equal(itemCount, completedCount);
	}

	/// <summary>
	/// Verifies that graceful shutdown in parallel mode waits for all running tasks.
	/// </summary>
	[Fact]
	public async Task Processing_ParallelShutdown_WaitsForRunningTasks()
	{
		// Arrange
		var service = new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			maxConcurrency: 4,
			shutdownTimeout: TimeSpan.FromSeconds(10));
		await service.InitializeAsync();

		int completedCount = 0;
		const int itemCount = 8;

		for (int i = 0; i < itemCount; i++)
		{
			_ = service.QueueWorkItem(async _ =>
			{
				await Task.Delay(100, CancellationToken.None);
				Interlocked.Increment(ref completedCount);
			});
		}

		// Give a moment for tasks to start.
		await Task.Delay(50);

		// Act
		// Shut down - should wait for all tasks to complete.
		await service.ShutdownAsync();

		// Assert - all items should have completed
		Assert.Equal(itemCount, completedCount);

		// Cleanup
		await service.DisposeAsync();
	}

	#endregion

	#region Parallel Processing (maxConcurrency > 1) - Error Cases

	/// <summary>
	/// Verifies that exceptions in one parallel work item do not affect other work items.
	/// </summary>
	[Fact]
	public async Task Processing_WithParallelConcurrency_ExceptionInOneItemDoesNotAffectOthers()
	{
		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(loggerFactory: LoggerFactory, maxConcurrency: 4);
		int successCount = 0;
		var allCompleted = new TaskCompletionSource<bool>();
		const int totalItems = 10;

		// Act
		for (int i = 0; i < totalItems; i++)
		{
			int index = i;
			_ = service.QueueWorkItem(async _ =>
			{
				await Task.Delay(10, CancellationToken.None);

				// Every other item throws
				if (index % 2 == 0)
					throw new InvalidOperationException($"Test exception from item {index}");

				if (Interlocked.Increment(ref successCount) == totalItems / 2)
					allCompleted.SetResult(true);
			});
		}

		// Wait for all successful items to complete.
		await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Assert
		Assert.Equal(totalItems / 2, successCount);
	}

	#endregion

	#region ExecutionContext Isolation

	/// <summary>
	/// Verifies that work items do not inherit <see cref="Transaction.Current"/> from the enqueueing thread.
	/// </summary>
	/// <param name="maxConcurrency">The concurrency level to test.</param>
	/// <param name="caseName">A descriptive name for the test case.</param>
	[Theory]
	[InlineData(1, "sequential")]
	[InlineData(2, "parallel")]
	public async Task Processing_WorkItemDoesNotInheritTransactionContext(int maxConcurrency, string caseName)
	{
		_ = caseName; // Used for test output

		// Arrange
		await using var service = await WorkQueueProcessor.CreateAsync(
			                          loggerFactory: LoggerFactory,
			                          maxConcurrency: maxConcurrency);
		var capturedTransactions = new ConcurrentBag<Transaction?>();
		var allExecuted = new TaskCompletionSource<bool>();
		const int itemCount = 5;

		// Act - Create an ambient transaction and queue work items.
		using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
		{
			Assert.NotNull(Transaction.Current);

			for (int i = 0; i < itemCount; i++)
			{
				_ = service.QueueWorkItem(_ =>
				{
					capturedTransactions.Add(Transaction.Current);
					if (capturedTransactions.Count == itemCount)
						allExecuted.SetResult(true);
					return Task.CompletedTask;
				});
			}

			// Don't complete the scope - just let it roll back.
		}

		// Wait for all items to execute.
		await allExecuted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Assert - work items should NOT have inherited the transaction.
		Assert.Equal(itemCount, capturedTransactions.Count);
		foreach (Transaction? tx in capturedTransactions)
		{
			Assert.Null(tx);
		}
	}

	#endregion

	#region Shutdown Cancellation - Normal Operation

	/// <summary>
	/// Verifies that the cancellation token is signaled during shutdown.
	/// </summary>
	[Fact]
	public async Task Processing_DuringShutdown_CancellationTokenIsSignaled()
	{
		// Arrange
		var service = new WorkQueueProcessor(loggerFactory: LoggerFactory, shutdownTimeout: TimeSpan.FromSeconds(5));
		await service.InitializeAsync();

		var tokenSignaled = new TaskCompletionSource<bool>();
		var workItemStarted = new TaskCompletionSource<bool>();

		_ = service.QueueWorkItem(async ct =>
		{
			// Signal that work item has started.
			workItemStarted.SetResult(true);

			// Wait for cancellation.
			try
			{
				await Task.Delay(Timeout.Infinite, ct);
			}
			catch (OperationCanceledException)
			{
				tokenSignaled.SetResult(true);
				throw;
			}
		});

		// Wait for work item to start.
		await workItemStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Act - Initiate shutdown.
		await service.ShutdownAsync();

		// Assert
		Assert.True(tokenSignaled.Task.IsCompletedSuccessfully);

		// Cleanup
		await service.DisposeAsync();
	}

	/// <summary>
	/// Verifies that shutdown with a long-running work item waits for completion.
	/// </summary>
	[Fact]
	public async Task Processing_ShutdownWithLongRunningWorkItem_WaitsForCompletion()
	{
		// Arrange
		var service = new WorkQueueProcessor(loggerFactory: LoggerFactory, shutdownTimeout: TimeSpan.FromSeconds(5));
		await service.InitializeAsync();

		bool workItemCompleted = false;
		var workItemStarted = new TaskCompletionSource<bool>();

		_ = service.QueueWorkItem(async _ =>
		{
			workItemStarted.SetResult(true);
			await Task.Delay(200, CancellationToken.None); // Longer than typical but shorter than timeout
			workItemCompleted = true;
		});

		// Wait for work item to start.
		await workItemStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Act
		await service.ShutdownAsync();

		// Assert
		Assert.True(workItemCompleted);

		// Cleanup
		await service.DisposeAsync();
	}

	/// <summary>
	/// Verifies that the service handles cancellation during parallel semaphore wait.
	/// </summary>
	[Fact]
	public async Task Processing_ParallelShutdownDuringSemaphoreWait_HandlesGracefully()
	{
		// Arrange - small concurrency to easily fill slots
		var service = new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			maxConcurrency: 1,
			shutdownTimeout: TimeSpan.FromMilliseconds(100));
		await service.InitializeAsync();

		var workItemStarted = new TaskCompletionSource<bool>();
		var workItemCanComplete = new TaskCompletionSource<bool>();

		// Queue a blocking work item
		_ = service.QueueWorkItem(async ct =>
		{
			workItemStarted.SetResult(true);
			try
			{
				await workItemCanComplete.Task.WaitAsync(ct);
			}
			catch (OperationCanceledException)
			{
				// Expected during shutdown
			}
		});

		await workItemStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Act - start shutdown while work item is running
		Task shutdownTask = service.ShutdownAsync();

		// Let the work item complete after a short delay
		await Task.Delay(50);
		workItemCanComplete.SetResult(true);

		// Assert - shutdown should complete
		await shutdownTask;

		// Cleanup
		await service.DisposeAsync();
	}

	#endregion

	#region Shutdown Cancellation - Timeout/Error Cases

	/// <summary>
	/// Verifies that shutdown timeout causes remaining queued items to be discarded.
	/// </summary>
	/// <param name="maxConcurrency">The concurrency level to test.</param>
	/// <param name="caseName">A descriptive name for the test case.</param>
	[Theory]
	[InlineData(1, "sequential")]
	[InlineData(2, "parallel")]
	public async Task Processing_ShutdownTimeout_DiscardsRemainingQueuedItems(int maxConcurrency, string caseName)
	{
		_ = caseName; // Used for test output

		// Arrange - very short shutdown timeout
		var service = new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			maxQueueSize: 100,
			shutdownTimeout: TimeSpan.FromMilliseconds(50),
			maxConcurrency: maxConcurrency);
		await service.InitializeAsync();

		var blockingWorkItemsStarted = new TaskCompletionSource<bool>();
		var blockingWorkItemsCanComplete = new TaskCompletionSource<bool>();
		int startedCount = 0;
		var queuedItemExecuted = new TaskCompletionSource<bool>();

		// Queue blocking work items that will hold all concurrency slots.
		for (int i = 0; i < maxConcurrency; i++)
		{
			_ = service.QueueWorkItem(async _ =>
			{
				if (Interlocked.Increment(ref startedCount) == maxConcurrency)
					blockingWorkItemsStarted.SetResult(true);
				await blockingWorkItemsCanComplete.Task;
			});
		}

		// Wait for blocking items to start.
		await blockingWorkItemsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// Queue another item that will be waiting in the queue.
		_ = service.QueueWorkItem(_ =>
		{
			queuedItemExecuted.TrySetResult(true);
			return Task.CompletedTask;
		});

		// Act - shutdown (timeout will occur).
		Task shutdownTask = service.ShutdownAsync();

		// Wait for timeout, then let the blocking items complete.
		await Task.Delay(200);
		blockingWorkItemsCanComplete.SetResult(true);

		// Wait for shutdown to complete.
		await shutdownTask;

		// Assert - the queued item should NOT have executed (was discarded due to timeout).
		await Task.Delay(100);
		Assert.False(queuedItemExecuted.Task.IsCompleted);

		// Cleanup
		await service.DisposeAsync();
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Atomically updates a location to the maximum of its current value and a new value.
	/// </summary>
	/// <remarks>
	/// This is a polyfill for <c>Interlocked.Maximum</c> which doesn't exist yet in .NET 10.
	/// </remarks>
	private static void InterlockedMax(ref int location, int value)
	{
		int current;
		do
		{
			current = location;
		} while (value > current && Interlocked.CompareExchange(ref location, value, current) != current);
	}

	#endregion
}
