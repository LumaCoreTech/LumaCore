// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.TestUtilities.Async.AsyncTestHelpers;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable ReplaceAsyncWithTaskReturn

namespace LumaCore.Core.Tests.Threading;

public partial class AsyncManualResetEventTests
{
	#region WaitAsync() without CancellationToken

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.WaitAsync()"/> returns a completed task immediately
	/// when the event is already set.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WhenEventIsSet_CompletesImmediately()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(true);

		// Act
		Task waitTask = mre.WaitAsync();
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() on set event should complete immediately");

		// Assert
		Assert.True(waitTask.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.WaitAsync()"/> returns an incomplete task when the
	/// event is not set.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WhenEventIsNotSet_ReturnsIncompleteTask()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);

		// Act
		Task waitTask = mre.WaitAsync();

		// Assert
		Assert.False(waitTask.IsCompleted);

		// Cleanup
		mre.Set();
		await AwaitWithTimeoutAsync(waitTask, "Cleanup wait timed out");
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.WaitAsync()"/> completes when
	/// <see cref="AsyncManualResetEvent.Set()"/> is called.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WhenSetIsCalled_Completes()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);
		Task waitTask = mre.WaitAsync();

		// Act
		mre.Set();
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() did not complete after Set() was called");

		// Assert
		Assert.True(waitTask.IsCompleted);
	}

	/// <summary>
	/// Verifies that multiple calls to <see cref="AsyncManualResetEvent.WaitAsync()"/> return the same task
	/// when the event is already set.
	/// </summary>
	[Fact]
	public void WaitAsync_WhenEventIsSet_ReturnsSameCompletedTask()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(true);

		// Act
		Task waitTask1 = mre.WaitAsync();
		Task waitTask2 = mre.WaitAsync();

		// Assert
		Assert.Same(waitTask1, waitTask2);
	}

	#endregion

	#region WaitAsync() with CancellationToken

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.WaitAsync(CancellationToken)"/> completes immediately
	/// when the event is already set, ignoring the cancellation token.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WithCancellationToken_WhenEventIsSet_CompletesImmediately()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(true);
		using var cts = new CancellationTokenSource();

		// Act
		Task waitTask = mre.WaitAsync(cts.Token);
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() on set event should complete immediately");

		// Assert
		Assert.True(waitTask.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.WaitAsync(CancellationToken)"/> completes when the
	/// event is set before cancellation.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WithCancellationToken_WhenSetBeforeCancellation_Completes()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);
		using var cts = new CancellationTokenSource();
		Task waitTask = mre.WaitAsync(cts.Token);

		// Act
		mre.Set();
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() did not complete after Set() was called");

		// Assert
		Assert.True(waitTask.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.WaitAsync(CancellationToken)"/> throws
	/// <see cref="OperationCanceledException"/> when the token is canceled before the event is set.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WithCancellationToken_WhenCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);
		using var cts = new CancellationTokenSource();
		Task waitTask = mre.WaitAsync(cts.Token);

		// Act
		cts.Cancel();

		// Assert - wrap in timeout to prevent deadlock if cancellation handling is broken


		Task assertion = Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);


		await AwaitWithTimeoutAsync(assertion, "Cancellation did not throw OperationCanceledException");
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.WaitAsync(CancellationToken)"/> with a non-cancelable
	/// token behaves like the overload without cancellation.
	/// </summary>
	[Fact]
	public async Task WaitAsync_WithNonCancelableToken_BehavesLikeOverloadWithoutCancellation()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);
		Task waitTask = mre.WaitAsync(CancellationToken.None);

		// Act
		mre.Set();
		await AwaitWithTimeoutAsync(waitTask, "WaitAsync() did not complete after Set() was called");

		// Assert
		Assert.True(waitTask.IsCompleted);
	}

	#endregion
}
