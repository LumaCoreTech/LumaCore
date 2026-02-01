// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.Core.Tests.AsyncTestHelpers;

namespace LumaCore.Core.Tests.Threading;

public partial class AsyncManualResetEventTests
{
	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.IsSet"/> returns <see langword="false"/> for a newly
	/// created event in the reset state.
	/// </summary>
	[Fact]
	public void IsSet_WhenEventIsReset_ReturnsFalse()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);

		// Act
		bool result = mre.IsSet;

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.IsSet"/> returns <see langword="true"/> after
	/// <see cref="AsyncManualResetEvent.Set()"/> is called.
	/// </summary>
	[Fact]
	public void IsSet_AfterSet_ReturnsTrue()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);

		// Act
		mre.Set();
		bool result = mre.IsSet;

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.IsSet"/> returns <see langword="false"/> after
	/// <see cref="AsyncManualResetEvent.Reset()"/> is called.
	/// </summary>
	[Fact]
	public void IsSet_AfterReset_ReturnsFalse()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(true);

		// Act
		mre.Reset();
		bool result = mre.IsSet;

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.IsSet"/> remains <see langword="true"/> after multiple
	/// waiters consume the signal (unlike auto-reset).
	/// </summary>
	[Fact]
	public async Task IsSet_AfterMultipleWaitersConsume_RemainsTrue()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(true);

		// Act
		await AwaitWithTimeoutAsync(mre.WaitAsync(), "WaitAsync() on set event should complete immediately");
		await AwaitWithTimeoutAsync(mre.WaitAsync(), "WaitAsync() on set event should complete immediately");
		await AwaitWithTimeoutAsync(mre.WaitAsync(), "WaitAsync() on set event should complete immediately");

		// Assert
		Assert.True(mre.IsSet);
	}
}
