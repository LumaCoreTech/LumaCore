// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.TestUtilities.Async.AsyncTestHelpers;

// ReSharper disable MethodHasAsyncOverload

namespace LumaCore.Core.Tests.Threading;

public partial class DefaultAsyncWaitQueueTests
{
	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.TryCancel"/> cancels the specified task.
	/// </summary>
	[Fact]
	public async Task TryCancel_WhenTaskExists_CancelsAndReturnsTrue()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Task<int> task = queue.Enqueue();

		// Act
		bool result = queue.TryCancel(task, cts.Token);

		// Assert - wrap in timeout to prevent deadlock if TryCancel() fails to cancel
		Assert.True(result);
		Assert.True(queue.IsEmpty);
		Task assertion = Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
		await AwaitWithTimeoutAsync(assertion, "Task was not canceled");
	}

	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.TryCancel"/> returns <see langword="false"/>
	/// when task is not in queue.
	/// </summary>
	[Fact]
	public void TryCancel_WhenTaskNotInQueue_ReturnsFalse()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Task<int> unrelatedTask = Task.FromResult(0);

		// Act
		bool result = queue.TryCancel(unrelatedTask, cts.Token);

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.TryCancel"/> cancels only the specified task, leaving other
	/// tasks in the queue.
	/// </summary>
	[Fact]
	public async Task TryCancel_CancelsOnlySpecifiedTask()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Task<int> task1 = queue.Enqueue();
		Task<int> task2 = queue.Enqueue();
		Task<int> task3 = queue.Enqueue();

		// Act - cancel middle task
		bool result = queue.TryCancel(task2, cts.Token);

		// Assert - wrap in timeout to prevent deadlock if TryCancel() fails to cancel
		Assert.True(result);
		Assert.False(queue.IsEmpty);
		Assert.False(task1.IsCompleted);
		Task assertion = Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2);
		await AwaitWithTimeoutAsync(assertion, "Task2 was not canceled");
		Assert.False(task3.IsCompleted);

		// Cleanup
		queue.DequeueAll(0);
	}
}
