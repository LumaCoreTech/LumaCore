// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region Indexer - Happy Path

	/// <summary>
	/// Verifies that the <see cref="Deque{T}.this[int]"/> indexer returns the correct element at each position.
	/// </summary>
	[Fact]
	public void Indexer_Get_ReturnsCorrectElement()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30, 40, 50]);

		// Act + Assert
		Assert.Equal(10, deque[0]);
		Assert.Equal(20, deque[1]);
		Assert.Equal(30, deque[2]);
		Assert.Equal(40, deque[3]);
		Assert.Equal(50, deque[4]);
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [10, 20, 30, 40, 50]);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}.this[int]"/> indexer can set values at specific positions.
	/// </summary>
	[Fact]
	public void Indexer_Set_UpdatesElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		deque[1] = 99;

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 99, 3]);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}.this[Index]"/> indexer (^) works correctly for accessing elements from the end.
	/// </summary>
	[Fact]
	public void IndexerWithIndex_FromEnd_ReturnsCorrectElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);

		// Act + Assert
		Assert.Equal(5, deque[^1]);
		Assert.Equal(4, deque[^2]);
		Assert.Equal(1, deque[^5]);
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}.this[Index]"/> indexer can set values using from-end syntax.
	/// </summary>
	[Fact]
	public void IndexerWithIndex_SetFromEnd_UpdatesElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		deque[^1] = 99;

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 99]);
	}

	/// <summary>
	/// Verifies that accessing elements via the indexer works correctly after the buffer wraps around.
	/// </summary>
	[Fact]
	public void Indexer_AfterWrapAround_ReturnsCorrectElements()
	{
		// Arrange - create a deque that will wrap around
		var deque = new Deque<int>(4);
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.AddToBack(3);
		deque.AddToBack(4);
		deque.RemoveFromFront(); // removes 1, offset moves
		deque.RemoveFromFront(); // removes 2, offset moves
		deque.AddToBack(5);      // wraps to beginning
		deque.AddToBack(6);      // wraps to beginning

		// Act + Assert
		Assert.Equal(3, deque[0]);
		Assert.Equal(4, deque[1]);
		Assert.Equal(5, deque[2]);
		Assert.Equal(6, deque[3]);
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 4,
			expectedElements: [3, 4, 5, 6]);
	}

	#endregion

	#region Indexer - Error / Exception

	/// <summary>
	/// Verifies that the <see cref="Deque{T}.this[int]"/> indexer throws <see cref="ArgumentOutOfRangeException"/> when
	/// getting an element at a negative index.
	/// </summary>
	[Fact]
	public void Indexer_GetWithNegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _ = deque[-1]);
		Assert.Equal("index", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}.this[int]"/> indexer throws <see cref="ArgumentOutOfRangeException"/> when
	/// getting an element at an index equal to <see cref="Deque{T}.Count"/>.
	/// </summary>
	[Fact]
	public void Indexer_GetWithIndexEqualToCount_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _ = deque[3]);
		Assert.Equal("index", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}.this[int]"/> indexer throws <see cref="ArgumentOutOfRangeException"/> when
	/// setting an element at an invalid index.
	/// </summary>
	[Fact]
	public void Indexer_SetWithInvalidIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		// ReSharper disable once CollectionNeverQueried.Local
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque[5] = 99);
		Assert.Equal("index", ex.ParamName);
	}

	#endregion
}
