// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToFront"/> inserts an element at the beginning of the deque.
	/// </summary>
	[Fact]
	public void AddToFront_ToEmptyDeque_AddsElement()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.AddToFront(42);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 8,
			expectedElements: [42]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToFront"/> inserts multiple elements in reverse order at the front.
	/// </summary>
	[Fact]
	public void AddToFront_MultipleElements_MaintainsReverseInsertionOrder()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.AddToFront(1);
		deque.AddToFront(2);
		deque.AddToFront(3);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 8,
			expectedElements: [3, 2, 1]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToFront"/> triggers capacity expansion when the buffer is full.
	/// </summary>
	[Fact]
	public void AddToFront_WhenFull_ExpandsCapacity()
	{
		// Arrange
		var deque = new Deque<int>(2);
		deque.AddToFront(1);
		deque.AddToFront(2);
		Assert.True(deque.IsFull);

		// Act
		deque.AddToFront(3);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [3, 2, 1]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToFront"/> works correctly with reference types.
	/// </summary>
	[Fact]
	public void AddToFront_WithReferenceType_AddsElement()
	{
		// Arrange
		var deque = new Deque<string>();

		// Act
		deque.AddToFront("first");
		deque.AddToFront("second");

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 8,
			expectedElements: ["second", "first"]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToFront"/> handles wrap-around correctly.
	/// </summary>
	[Fact]
	public void AddToFront_WithWrapAround_MaintainsCorrectOrder()
	{
		// Arrange - create a situation where offset is not at 0
		var deque = new Deque<int>(4);
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.RemoveFromFront(); // offset moves to 1

		// Act
		deque.AddToFront(10);
		deque.AddToFront(20);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [20, 10, 2]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToFront"/> with zero capacity expands to 1.
	/// </summary>
	[Fact]
	public void AddToFront_WithZeroCapacity_ExpandsToOne()
	{
		// Arrange
		var deque = new Deque<int>(0);

		// Act
		deque.AddToFront(42);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 1,
			expectedElements: [42]);
	}
}
