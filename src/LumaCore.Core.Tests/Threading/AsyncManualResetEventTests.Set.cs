// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.TestUtilities.Async.AsyncTestHelpers;

namespace LumaCore.Core.Tests.Threading;

public partial class AsyncManualResetEventTests
{
	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Set()"/> transitions the event to the signaled state.
	/// </summary>
	[Fact]
	public void Set_WhenEventIsReset_SetsEventToSignaledState()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);

		// Act
		mre.Set();

		// Assert
		Assert.True(mre.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Set()"/> is idempotent (multiple calls have no additional
	/// effect).
	/// </summary>
	[Fact]
	public void Set_CalledMultipleTimes_RemainsSignaled()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);

		// Act
		mre.Set();
		mre.Set();
		mre.Set();

		// Assert
		Assert.True(mre.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Set()"/> releases all waiting tasks.
	/// </summary>
	[Fact]
	public async Task Set_WithMultipleWaiters_ReleasesAllWaiters()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);
		Task waitTask1 = mre.WaitAsync();
		Task waitTask2 = mre.WaitAsync();
		Task waitTask3 = mre.WaitAsync();

		// Act
		mre.Set();

		await AwaitWithTimeoutAsync(
			Task.WhenAll(waitTask1, waitTask2, waitTask3),
			"Not all waiters were released after Set() was called");

		// Assert
		Assert.True(waitTask1.IsCompleted);
		Assert.True(waitTask2.IsCompleted);
		Assert.True(waitTask3.IsCompleted);
	}
}
