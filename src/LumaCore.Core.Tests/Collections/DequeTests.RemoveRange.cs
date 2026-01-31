// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region RemoveRange - Happy Path

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> removes elements from the beginning.
	/// </summary>
	[Fact]
	public void RemoveRange_FromBeginning_RemovesElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);

		// Act
		deque.RemoveRange(0, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 5,
			expectedElements: [3, 4, 5]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> removes elements from the end.
	/// </summary>
	[Fact]
	public void RemoveRange_FromEnd_RemovesElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);

		// Act
		deque.RemoveRange(3, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> removes elements from the first half of the deque.
	/// </summary>
	[Fact]
	public void RemoveRange_FromFirstHalf_RemovesElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5, 6]);

		// Act
		deque.RemoveRange(1, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 6,
			expectedElements: [1, 4, 5, 6]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> removes elements from the second half of the deque.
	/// </summary>
	[Fact]
	public void RemoveRange_FromSecondHalf_RemovesElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5, 6]);

		// Act
		deque.RemoveRange(3, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 6,
			expectedElements: [1, 2, 3, 6]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> handles count of zero without error.
	/// </summary>
	[Fact]
	public void RemoveRange_WithZeroCount_NoChange()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		deque.RemoveRange(1, 0);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> removes all elements when offset is 0 and
	/// count equals <see cref="Deque{T}.Count"/>.
	/// </summary>
	[Fact]
	public void RemoveRange_AllElements_EmptiesDeque()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		deque.RemoveRange(0, 3);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 3,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> from beginning clears reference type slots for garbage collection.
	/// </summary>
	[Fact]
	public void RemoveRange_FromBeginningWithReferenceType_ClearsSlotsForGarbageCollection()
	{
		// Arrange
		var deque = new Deque<string>(["a", "b", "c", "d", "e"]);

		// Act - remove from beginning (index == 0)
		deque.RemoveRange(0, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 5,
			expectedElements: ["c", "d", "e"]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> from end clears reference type slots for garbage collection.
	/// </summary>
	[Fact]
	public void RemoveRange_FromEndWithReferenceType_ClearsSlotsForGarbageCollection()
	{
		// Arrange
		var deque = new Deque<string>(["a", "b", "c", "d", "e"]);

		// Act - remove from end (index == Count - count)
		deque.RemoveRange(3, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 5,
			expectedElements: ["a", "b", "c"]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> from first half clears reference type slots for garbage collection.
	/// </summary>
	[Fact]
	public void RemoveRange_FromFirstHalfWithReferenceType_ClearsSlotsForGarbageCollection()
	{
		// Arrange
		var deque = new Deque<string>(["a", "b", "c", "d", "e", "f", "g", "h"]);

		// Act - remove from first half (index 1, count 2)
		deque.RemoveRange(1, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 6,
			expectedCapacity: 8,
			expectedElements: ["a", "d", "e", "f", "g", "h"]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> from second half clears reference type slots for garbage collection.
	/// </summary>
	[Fact]
	public void RemoveRange_FromSecondHalfWithReferenceType_ClearsSlotsForGarbageCollection()
	{
		// Arrange
		var deque = new Deque<string>(["a", "b", "c", "d", "e"]);

		// Act - remove from second half
		deque.RemoveRange(2, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 5,
			expectedElements: ["a", "b", "e"]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> works correctly with wrap-around buffer.
	/// </summary>
	[Fact]
	public void RemoveRange_WithWrapAround_HandlesCorrectly()
	{
		// Arrange
		var deque = new Deque<int>(4);
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.AddToBack(3);
		deque.AddToBack(4);
		deque.RemoveFromFront();
		deque.RemoveFromFront();
		deque.AddToBack(5);
		deque.AddToBack(6);
		// deque is now [3, 4, 5, 6] with wrap-around

		// Act
		deque.RemoveRange(1, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 4,
			expectedElements: [3, 6]);
	}

	#endregion

	#region RemoveRange - Error / Exception

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> throws <see cref="ArgumentOutOfRangeException"/>
	/// when offset is negative.
	/// </summary>
	[Fact]
	public void RemoveRange_WithNegativeOffset_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.RemoveRange(-1, 1));
		Assert.Equal("offset", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> throws <see cref="ArgumentOutOfRangeException"/>
	/// when count is negative.
	/// </summary>
	[Fact]
	public void RemoveRange_WithNegativeCount_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.RemoveRange(0, -1));
		Assert.Equal("count", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveRange"/> throws <see cref="ArgumentException"/>
	/// when range exceeds bounds.
	/// </summary>
	[Fact]
	public void RemoveRange_WhenRangeExceedsBounds_ThrowsArgumentException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		Assert.Throws<ArgumentException>(() => deque.RemoveRange(1, 5));
	}

	#endregion
}
