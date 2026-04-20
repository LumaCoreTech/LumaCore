// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.TestUtilities.Async.AsyncTestHelpers;

namespace LumaCore.Core.Tests.Threading;

public partial class AsyncAutoResetEventTests
{
	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.Set()"/> transitions the event to the signaled state
	/// when there are no waiters.
	/// </summary>
	[Fact]
	public void Set_WhenNoWaiters_SetsEventToSignaledState()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);

		// Act
		are.Set();

		// Assert
		Assert.True(are.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.Set()"/> releases exactly one waiter and the event
	/// remains in the reset state.
	/// </summary>
	[Fact]
	public async Task Set_WhenOneWaiterExists_ReleasesWaiterAndRemainsReset()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		Task waitTask = are.WaitAsync();

		// Act
		are.Set();
		await AwaitWithTimeoutAsync(waitTask, "Set() did not release the waiter");

		// Assert
		Assert.False(are.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.Set()"/> releases waiters in FIFO order (First-In-First-Out).
	/// </summary>
	[Fact]
	public async Task Set_WhenMultipleWaitersExist_ReleasesInFifoOrder()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		var completionOrder = new List<int>();

		async Task WaitAndRecordAsync(int id)
		{
			await are.WaitAsync();
			lock (completionOrder)
			{
				completionOrder.Add(id);
			}
		}

		// Queue waiters sequentially
		Task waitTask1 = WaitAndRecordAsync(1);
		Task waitTask2 = WaitAndRecordAsync(2);
		Task waitTask3 = WaitAndRecordAsync(3);

		// Act & Assert - Release 1st
		are.Set();
		await AwaitWithTimeoutAsync(waitTask1, "FIFO violation: Waiter 1 should be released first");
		Assert.False(waitTask2.IsCompleted, "Waiter 2 should not be completed yet");
		Assert.False(waitTask3.IsCompleted, "Waiter 3 should not be completed yet");

		// Act & Assert - Release 2nd
		are.Set();
		await AwaitWithTimeoutAsync(waitTask2, "FIFO violation: Waiter 2 should be released second");
		Assert.False(waitTask3.IsCompleted, "Waiter 3 should not be completed yet");

		// Act & Assert - Release 3rd
		are.Set();
		await AwaitWithTimeoutAsync(waitTask3, "FIFO violation: Waiter 3 should be released third");

		// Assert integrity
		Assert.Equal([1, 2, 3], completionOrder);
		Assert.False(are.IsSet);
	}

	/// <summary>
	/// Verifies that calling <see cref="AsyncAutoResetEvent.Set()"/> multiple times without waiters does not
	/// stack signals (idempotent behavior).
	/// </summary>
	[Fact]
	public Task Set_CalledMultipleTimesWithNoWaiters_DoesNotStackSignals()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);

		// Act
		are.Set();
		are.Set();
		are.Set();

		// Assert - first wait consumes the single signal
		Task waitTask1 = are.WaitAsync();
		Assert.True(waitTask1.IsCompleted);

		// Second wait should block (no stacked signal)
		Task waitTask2 = are.WaitAsync();
		Assert.False(waitTask2.IsCompleted);

		// Cleanup
		are.Set();
		return AwaitWithTimeoutAsync(waitTask2, "Cleanup wait timed out");
	}
}
