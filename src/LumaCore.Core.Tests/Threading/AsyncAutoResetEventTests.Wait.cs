// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.TestUtilities.Async.AsyncTestHelpers;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable MethodHasAsyncOverload
// ReSharper disable MethodSupportsCancellation

namespace LumaCore.Core.Tests.Threading;

public partial class AsyncAutoResetEventTests
{
	#region Wait()

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.Wait()"/> returns immediately when the event is already
	/// set, and auto-resets the event.
	/// </summary>
	[Fact]
	public async Task Wait_WhenEventIsSet_ReturnsImmediatelyAndAutoResets()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(true);

		// Act - wrap in Task.Run with timeout to prevent deadlock if implementation is broken
		Task waitTask = Task.Run(() => are.Wait());
		await AwaitWithTimeoutAsync(waitTask, "Wait() did not return immediately on set event");

		// Assert
		Assert.False(are.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.Wait()"/> blocks until the event is set.
	/// </summary>
	[Fact]
	public async Task Wait_WhenEventIsNotSet_BlocksUntilSet()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		bool waitCompleted = false;
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Use a dedicated thread for the blocking Wait() to avoid thread pool starvation on CI runners
		// where parallel xUnit execution + limited cores exhaust the pool.
		var waitThread = new Thread(() =>
		{
			are.Wait();
			waitCompleted = true;
			completed.SetResult();
		})
		{
			IsBackground = true
		};

		// Act
		waitThread.Start();
		await Task.Delay(50);
		are.Set();

		await AwaitWithTimeoutAsync(completed.Task, "Wait() did not complete after Set() was called");

		// Assert
		Assert.True(waitCompleted);
	}

	#endregion

	#region Wait(CancellationToken)

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.Wait(CancellationToken)"/> returns immediately when the
	/// event is already set, ignoring the cancellation token.
	/// </summary>
	[Fact]
	public async Task Wait_WithCancellationToken_WhenEventIsSet_ReturnsImmediately()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(true);
		using var cts = new CancellationTokenSource();

		// Act - wrap in Task.Run with timeout to prevent deadlock if implementation is broken
		Task waitTask = Task.Run(() => are.Wait(cts.Token));
		await AwaitWithTimeoutAsync(waitTask, "Wait() did not return immediately on set event");

		// Assert
		Assert.False(are.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.Wait(CancellationToken)"/> throws
	/// <see cref="OperationCanceledException"/> when the token is canceled before the event is set.
	/// </summary>
	[Fact]
	public async Task Wait_WithCancellationToken_WhenCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		using var cts = new CancellationTokenSource();

		// Schedule cancellation via timer — avoids thread pool dependency that causes flakiness on CI runners.
		cts.CancelAfter(TimeSpan.FromMilliseconds(100));

		// Act + Assert - wrap synchronous Wait in Task.Run to prevent blocking test runner
		Task waitTask = Task.Run(() => are.Wait(cts.Token));
		Task assertion = Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
		await AwaitWithTimeoutAsync(assertion, "Cancellation did not throw OperationCanceledException");
	}

	#endregion
}
