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
	/// <see cref="OperationCanceledException"/> when the token is already canceled before <c>Wait()</c> is called.
	/// </summary>
	[Fact]
	public void Wait_WithCancellationToken_WhenCanceledBeforeWait_ThrowsOperationCanceledException()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		using var cts = new CancellationTokenSource();

		cts.Cancel();

		// Act + Assert — a pre-canceled token causes task.Wait(ct) to throw immediately without blocking,
		// so no Task.Run wrapper is needed here.
		var ex = Assert.Throws<OperationCanceledException>(() => are.Wait(cts.Token));
		Assert.Equal(cts.Token, ex.CancellationToken);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.Wait(CancellationToken)"/> throws
	/// <see cref="OperationCanceledException"/> when the token is canceled while the method is already
	/// blocking — testing the cancellation callback path, not just the pre-canceled token guard.
	/// </summary>
	[Fact]
	public async Task Wait_WithCancellationToken_WhenCanceledDuringWait_ThrowsOperationCanceledException()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);
		using var cts = new CancellationTokenSource();
		using var aboutToWait = new SemaphoreSlim(0, 1);

		// Use a dedicated thread instead of Task.Run to avoid thread pool starvation on CI runners
		// where parallel xUnit execution + limited cores may delay the lambda far past the cancellation.
		// The thread signals via the semaphore that it is about to enter Wait() so cancellation fires
		// while the thread is blocked. The tiny window between Release() and Wait(cts.Token) is harmless
		// because a pre-canceled token also causes Wait() to throw immediately.
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Exception? threadException = null;
		var waitThread = new Thread(() =>
		{
			try
			{
				aboutToWait.Release();
				are.Wait(cts.Token);
				completed.SetResult();
			}
			catch (Exception ex)
			{
				threadException = ex;
				completed.SetResult();
			}
		}) { IsBackground = true };
		waitThread.Start();

		await AwaitWithTimeoutAsync(aboutToWait.WaitAsync(), "Thread did not start");

		// Act
		cts.Cancel();
		await AwaitWithTimeoutAsync(completed.Task, "Cancellation did not unblock Wait()");

		// Assert
		var ex = Assert.IsAssignableFrom<OperationCanceledException>(threadException);
		Assert.Equal(cts.Token, ex.CancellationToken);
	}

	#endregion
}
