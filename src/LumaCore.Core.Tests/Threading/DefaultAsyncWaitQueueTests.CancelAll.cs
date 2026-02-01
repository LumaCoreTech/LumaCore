// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.Core.Tests.AsyncTestHelpers;

// ReSharper disable MethodHasAsyncOverload

namespace LumaCore.Core.Tests.Threading;

public partial class DefaultAsyncWaitQueueTests
{
	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.CancelAll"/> cancels all waiting tasks and clears the queue.
	/// </summary>
	[Fact]
	public async Task CancelAll_CancelsAllWaitingTasks()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Task<int> task1 = queue.Enqueue();
		Task<int> task2 = queue.Enqueue();
		Task<int> task3 = queue.Enqueue();

		// Act
		queue.CancelAll(cts.Token);

		// Assert - wrap in timeout to prevent deadlock if CancelAll() fails to cancel
		Assert.True(queue.IsEmpty);
		Task assertion1 = Assert.ThrowsAnyAsync<OperationCanceledException>(() => task1);
		await AwaitWithTimeoutAsync(assertion1, "Task1 was not canceled");
		Task assertion2 = Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2);
		await AwaitWithTimeoutAsync(assertion2, "Task2 was not canceled");
		Task assertion3 = Assert.ThrowsAnyAsync<OperationCanceledException>(() => task3);
		await AwaitWithTimeoutAsync(assertion3, "Task3 was not canceled");
	}

	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.CancelAll"/> does nothing when queue is empty.
	/// </summary>
	[Fact]
	public void CancelAll_WhenQueueIsEmpty_DoesNothing()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act + Assert (no exception)
		queue.CancelAll(cts.Token);
		Assert.True(queue.IsEmpty);
	}
}
