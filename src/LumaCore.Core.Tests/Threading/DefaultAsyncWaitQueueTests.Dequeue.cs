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
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.Dequeue"/> completes the first waiter (FIFO order).
	/// </summary>
	[Fact]
	public async Task Dequeue_CompletesFirstWaiterInFifoOrder()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		Task<int> task1 = queue.Enqueue();
		Task<int> task2 = queue.Enqueue();

		// Act
		queue.Dequeue(1);

		// Assert
		Assert.True(task1.IsCompleted);
		Assert.False(task2.IsCompleted);
		Assert.Equal(1, await AwaitWithTimeoutAsync(task1, "Dequeue() did not complete task1"));

		// Cleanup
		queue.Dequeue(2);
	}

	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.Dequeue"/> removes the completed waiter from the queue.
	/// </summary>
	[Fact]
	public void Dequeue_RemovesWaiterFromQueue()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		queue.Enqueue();

		// Act
		queue.Dequeue(0);

		// Assert
		Assert.True(queue.IsEmpty);
	}

	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.Dequeue"/> completes waiter with the specified result.
	/// </summary>
	[Fact]
	public async Task Dequeue_CompletesWaiterWithSpecifiedResult()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		Task<int> task = queue.Enqueue();

		// Act
		queue.Dequeue(42);

		// Assert
		Assert.Equal(42, await AwaitWithTimeoutAsync(task, "Dequeue() did not complete task"));
	}
}
