// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.TestUtilities.Async.AsyncTestHelpers;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable MethodSupportsCancellation
// ReSharper disable ReplaceAsyncWithTaskReturn

namespace LumaCore.Core.Tests.Threading;

public partial class AsyncAutoResetEventTests
{
	#region WaitAsync() without CancellationToken

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.WaitAsync()"/> returns a completed task immediately
	/// when the event is already set, and auto-resets the event.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WhenEventIsSet_CompletesImmediatelyAndAutoResets()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(true);

		// Act
		Task waitTask = are.WaitAsync();
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() on set event should complete immediately");

		// Assert
		Assert.True(waitTask.IsCompleted);
		Assert.False(are.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.WaitAsync()"/> returns an incomplete task when the event
	/// is not set.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WhenEventIsNotSet_ReturnsIncompleteTask()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);

		// Act
		Task waitTask = are.WaitAsync();

		// Assert
		Assert.False(waitTask.IsCompleted);

		// Cleanup
		are.Set();
		await AwaitWithTimeoutAsync(waitTask, "Cleanup wait timed out");
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.WaitAsync()"/> completes when
	/// <see cref="AsyncAutoResetEvent.Set()"/> is called.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WhenSetIsCalled_Completes()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		Task waitTask = are.WaitAsync();

		// Act
		are.Set();
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() did not complete after Set() was called");

		// Assert
		Assert.True(waitTask.IsCompleted);
	}

	/// <summary>
	/// Verifies that waiters are released in FIFO order (First-In-First-Out).
	/// </summary>
	[Fact]
	public async Task WaitAsync_MultipleWaiters_ReleasedInFifoOrder()
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
	}

	#endregion

	#region WaitAsync() with CancellationToken

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.WaitAsync(CancellationToken)"/> completes immediately
	/// when the event is already set, ignoring the cancellation token.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WithCancellationToken_WhenEventIsSet_CompletesImmediately()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(true);
		using var cts = new CancellationTokenSource();

		// Act
		Task waitTask = are.WaitAsync(cts.Token);
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() on set event should complete immediately");

		// Assert
		Assert.True(waitTask.IsCompleted);
		Assert.False(are.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.WaitAsync(CancellationToken)"/> completes when the event
	/// is set before cancellation.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WithCancellationToken_WhenSetBeforeCancellation_Completes()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		using var cts = new CancellationTokenSource();
		Task waitTask = are.WaitAsync(cts.Token);

		// Act
		are.Set();
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() did not complete after Set() was called");

		// Assert
		Assert.True(waitTask.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.WaitAsync(CancellationToken)"/> throws
	/// <see cref="OperationCanceledException"/> when the token is canceled before the event is set.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WithCancellationToken_WhenCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		using var cts = new CancellationTokenSource();
		Task waitTask = are.WaitAsync(cts.Token);

		// Act
		cts.Cancel();

		// Assert - wrap in timeout to prevent deadlock if cancellation handling is broken
		Task assertion = Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
		await AwaitWithTimeoutAsync(assertion, "Cancellation did not throw OperationCanceledException");
	}

	/// <summary>
	/// Verifies that cancellation does not consume the signal, allowing another waiter to receive it.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WithCancellationToken_WhenCanceled_DoesNotConsumeSignal()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		using var cts = new CancellationTokenSource();
		Task waitTask1 = are.WaitAsync(cts.Token);
		Task waitTask2 = are.WaitAsync();

		// Act - cancel first waiter, then set
		cts.Cancel();
		Task assertion = Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask1);
		await AwaitWithTimeoutAsync(assertion, "Cancellation did not throw OperationCanceledException");
		are.Set();
		await AwaitWithTimeoutAsync(waitTask2, "Second waiter did not complete after Set() was called");

		// Assert
		Assert.True(waitTask2.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.WaitAsync(CancellationToken)"/> with a non-cancelable
	/// token behaves like the overload without cancellation.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WithNonCancelableToken_BehavesLikeOverloadWithoutCancellation()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		Task waitTask = are.WaitAsync(CancellationToken.None);

		// Act
		are.Set();
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() did not complete after Set() was called");

		// Assert
		Assert.True(waitTask.IsCompleted);
	}

	#endregion
}
