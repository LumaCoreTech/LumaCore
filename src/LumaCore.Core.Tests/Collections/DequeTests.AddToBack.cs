// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToBack"/> appends an element at the end of the deque.
	/// </summary>
	[Fact]
	public void AddToBack_ToEmptyDeque_AddsElement()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.AddToBack(42);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 8,
			expectedElements: [42]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToBack"/> maintains insertion order.
	/// </summary>
	[Fact]
	public void AddToBack_MultipleElements_MaintainsInsertionOrder()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.AddToBack(3);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 8,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToBack"/> triggers capacity expansion when the buffer is full.
	/// </summary>
	[Fact]
	public void AddToBack_WhenFull_ExpandsCapacity()
	{
		// Arrange
		var deque = new Deque<int>(2);
		deque.AddToBack(1);
		deque.AddToBack(2);
		Assert.True(deque.IsFull);

		// Act
		deque.AddToBack(3);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToBack"/> works correctly with reference types.
	/// </summary>
	[Fact]
	public void AddToBack_WithReferenceType_AddsElement()
	{
		// Arrange
		var deque = new Deque<string>();

		// Act
		deque.AddToBack("first");
		deque.AddToBack("second");

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 8,
			expectedElements: ["first", "second"]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToBack"/> handles wrap-around correctly.
	/// </summary>
	[Fact]
	public void AddToBack_WithWrapAround_MaintainsCorrectOrder()
	{
		// Arrange - fill and then remove from front to cause wrap
		var deque = new Deque<int>(4);
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.AddToBack(3);
		deque.AddToBack(4);
		deque.RemoveFromFront();
		deque.RemoveFromFront();

		// Act
		deque.AddToBack(5);
		deque.AddToBack(6);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 4,
			expectedElements: [3, 4, 5, 6]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.AddToBack"/> with zero capacity expands to 1.
	/// </summary>
	[Fact]
	public void AddToBack_WithZeroCapacity_ExpandsToOne()
	{
		// Arrange
		var deque = new Deque<int>(0);

		// Act
		deque.AddToBack(42);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 1,
			expectedElements: [42]);
	}
}
