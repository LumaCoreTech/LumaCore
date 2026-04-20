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
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.DequeueAll"/> completes all waiting tasks with the
	/// specified result and clears the queue.
	/// </summary>
	[Fact]
	public async Task DequeueAll_CompletesAllWaitingTasksWithSpecifiedResult()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		Task<int> task1 = queue.Enqueue();
		Task<int> task2 = queue.Enqueue();
		Task<int> task3 = queue.Enqueue();

		// Act
		queue.DequeueAll(42);

		// Assert
		Assert.True(queue.IsEmpty);
		Assert.Equal(42, await AwaitWithTimeoutAsync(task1, "DequeueAll() did not complete task1"));
		Assert.Equal(42, await AwaitWithTimeoutAsync(task2, "DequeueAll() did not complete task2"));
		Assert.Equal(42, await AwaitWithTimeoutAsync(task3, "DequeueAll() did not complete task3"));
	}

	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.DequeueAll"/> does nothing when queue is empty.
	/// </summary>
	[Fact]
	public void DequeueAll_WhenQueueIsEmpty_DoesNothing()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();

		// Act + Assert (no exception)
		queue.DequeueAll(42);
		Assert.True(queue.IsEmpty);
	}
}
