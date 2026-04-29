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
	/// Verifies that <see cref="AsyncAutoResetEvent.IsSet"/> returns <see langword="false"/> for a newly
	/// created event in the reset state.
	/// </summary>
	[Fact]
	public void IsSet_WhenEventIsReset_ReturnsFalse()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);

		// Act
		bool result = are.IsSet;

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.IsSet"/> returns <see langword="true"/> after
	/// <see cref="AsyncAutoResetEvent.Set()"/> is called with no waiters.
	/// </summary>
	[Fact]
	public void IsSet_AfterSetWithNoWaiters_ReturnsTrue()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(false);

		// Act
		are.Set();
		bool result = are.IsSet;

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncAutoResetEvent.IsSet"/> returns <see langword="false"/> after the event
	/// is consumed by a waiter.
	/// </summary>
	[Fact]
	public async Task IsSet_AfterWaitAsyncConsumesSignal_ReturnsFalse()
	{
		// Arrange
		var are = new AsyncAutoResetEvent(true);

		// Act
		await AwaitWithTimeoutAsync(are.WaitAsync(), "WaitAsync() on set event should complete immediately");
		bool result = are.IsSet;

		// Assert
		Assert.False(result);
	}
}
