// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	/// <summary>
	/// Verifies that <see cref="Deque{T}.Clear"/> removes all elements from the deque.
	/// </summary>
	[Fact]
	public void Clear_RemovesAllElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);

		// Act
		deque.Clear();

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 5,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Clear"/> on an empty deque does not throw.
	/// </summary>
	[Fact]
	public void Clear_OnEmptyDeque_DoesNotThrow()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		deque.Clear();

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Clear"/> on an empty deque with reference types does not throw
	/// and correctly skips clearing the buffer (early return in ClearBuffer()).
	/// </summary>
	[Fact]
	public void Clear_OnEmptyDequeWithReferenceType_DoesNotThrow()
	{
		// Arrange - empty deque with reference type to trigger ClearBuffer() early return
		var deque = new Deque<string>();

		// Act
		deque.Clear();

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Clear"/> resets offset to zero.
	/// </summary>
	[Fact]
	public void Clear_ResetsOffsetToZero()
	{
		// Arrange - create a deque with non-zero offset
		var deque = new Deque<int>(4);
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.RemoveFromFront();
		deque.AddToBack(3);

		// Act
		deque.Clear();
		deque.AddToBack(10);

		// Assert - after clear, new elements start at index 0
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 4,
			expectedElements: [10]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Clear"/> clears reference type slots for garbage collection.
	/// </summary>
	[Fact]
	public void Clear_WithReferenceType_ClearsSlotsForGarbageCollection()
	{
		// Arrange
		var deque = new Deque<string>(["a", "b", "c"]);

		// Act
		deque.Clear();
		deque.AddToBack("x");

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 3,
			expectedElements: ["x"]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Clear"/> works correctly with wrap-around buffer.
	/// </summary>
	[Fact]
	public void Clear_WithWrapAround_ClearsCorrectly()
	{
		// Arrange
		var deque = new Deque<string>(4);
		deque.AddToBack("a");
		deque.AddToBack("b");
		deque.AddToBack("c");
		deque.AddToBack("d");
		deque.RemoveFromFront();
		deque.RemoveFromFront();
		deque.AddToBack("e");
		deque.AddToBack("f");
		// Buffer is now wrapped around

		// Act
		deque.Clear();
		deque.AddToBack("x");

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 4,
			expectedElements: ["x"]);
	}
}
