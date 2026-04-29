// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.TestUtilities.Async.AsyncTestHelpers;

namespace LumaCore.Core.Tests.Threading;

public partial class DefaultAsyncWaitQueueTests
{
	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.Enqueue"/> adds a new waiter to the queue.
	/// </summary>
	[Fact]
	public void Enqueue_AddsWaiterToQueue()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();

		// Act
		Task<int> task = queue.Enqueue();

		// Assert
		Assert.False(queue.IsEmpty);
		Assert.False(task.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.Enqueue"/> returns a task that supports
	/// both synchronous and asynchronous waits.
	/// </summary>
	[Fact]
	public async Task Enqueue_ReturnsTaskThatCanBeAwaited()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();

		// Act
		Task<int> task = queue.Enqueue();
		queue.Dequeue(42);

		// Assert
		int result = await AwaitWithTimeoutAsync(task, "Enqueue() task did not complete after Dequeue()");
		Assert.Equal(42, result);
	}

	/// <summary>
	/// Verifies that multiple calls to <see cref="DefaultAsyncWaitQueue{T}.Enqueue"/> add multiple waiters.
	/// </summary>
	[Fact]
	public void Enqueue_CalledMultipleTimes_AddsMultipleWaiters()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();

		// Act
		Task<int> task1 = queue.Enqueue();
		Task<int> task2 = queue.Enqueue();
		Task<int> task3 = queue.Enqueue();

		// Assert
		Assert.False(queue.IsEmpty);
		Assert.False(task1.IsCompleted);
		Assert.False(task2.IsCompleted);
		Assert.False(task3.IsCompleted);

		// Cleanup
		queue.DequeueAll(0);
	}
}
