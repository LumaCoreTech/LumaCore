// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

public partial class DefaultAsyncWaitQueueTests
{
	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.IsEmpty"/> returns <see langword="true"/>
	/// for a newly created queue.
	/// </summary>
	[Fact]
	public void IsEmpty_WhenNewlyCreated_ReturnsTrue()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();

		// Act + Assert
		Assert.True(queue.IsEmpty);
	}

	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.IsEmpty"/> returns <see langword="false"/>
	/// when a waiter is enqueued.
	/// </summary>
	[Fact]
	public void IsEmpty_AfterEnqueue_ReturnsFalse()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();

		// Act
		queue.Enqueue();

		// Assert
		Assert.False(queue.IsEmpty);

		// Cleanup
		queue.Dequeue(0);
	}

	/// <summary>
	/// Verifies that <see cref="DefaultAsyncWaitQueue{T}.IsEmpty"/> returns <see langword="true"/>
	/// after all waiters are dequeued.
	/// </summary>
	[Fact]
	public void IsEmpty_AfterAllDequeued_ReturnsTrue()
	{
		// Arrange
		var queue = new DefaultAsyncWaitQueue<int>();
		queue.Enqueue();
		queue.Enqueue();

		// Act
		queue.Dequeue(0);
		queue.Dequeue(0);

		// Assert
		Assert.True(queue.IsEmpty);
	}
}
