// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.TestUtilities.Async.AsyncTestHelpers;

// ReSharper disable ReplaceAsyncWithTaskReturn

namespace LumaCore.Core.Tests.Threading;

public partial class AsyncManualResetEventTests
{
	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Reset()"/> transitions the event
	/// from signaled to non-signaled state.
	/// </summary>
	[Fact]
	public void Reset_WhenEventIsSet_ResetsToNonSignaledState()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(true);

		// Act
		mre.Reset();

		// Assert
		Assert.False(mre.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Reset()"/> is idempotent
	/// (multiple calls on already-reset event have no effect).
	/// </summary>
	[Fact]
	public void Reset_CalledMultipleTimes_RemainsNonSignaled()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);

		// Act
		mre.Reset();
		mre.Reset();
		mre.Reset();

		// Assert
		Assert.False(mre.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Reset()"/> after <see cref="AsyncManualResetEvent.Set()"/>
	/// causes new waiters to block.
	/// </summary>
	[Fact]
	public async Task Reset_AfterSet_NewWaitersBlock()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);
		mre.Set();

		// First waiter completes immediately
		Task waitTask1 = mre.WaitAsync();
		Assert.True(waitTask1.IsCompleted);

		// Act - reset the event
		mre.Reset();

		// New waiter should block
		Task waitTask2 = mre.WaitAsync();

		// Assert
		Assert.False(waitTask2.IsCompleted);

		// Cleanup
		mre.Set();
		await AwaitWithTimeoutAsync(waitTask2, "Cleanup wait timed out");
	}

	/// <summary>
	/// Verifies that tasks obtained before <see cref="AsyncManualResetEvent.Reset()"/>
	/// remain completed after reset.
	/// </summary>
	[Fact]
	public void Reset_DoesNotAffectAlreadyCompletedTasks()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(true);
		Task waitTask = mre.WaitAsync();

		// Act
		mre.Reset();

		// Assert - task obtained before reset is still completed
		Assert.True(waitTask.IsCompleted);
	}
}
