// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region InsertRange - Happy Path

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> inserts elements at the beginning.
	/// </summary>
	[Fact]
	public void InsertRange_AtBeginning_InsertsElements()
	{
		// Arrange
		var deque = new Deque<int>([3, 4, 5]);
		IEnumerable<int> items = [1, 2];

		// Act
		deque.InsertRange(0, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> inserts elements at the end.
	/// </summary>
	[Fact]
	public void InsertRange_AtEnd_InsertsElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IEnumerable<int> items = [4, 5];

		// Act
		deque.InsertRange(3, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> inserts elements in the middle (first half).
	/// </summary>
	[Fact]
	public void InsertRange_InFirstHalf_InsertsElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 5, 6]);
		IEnumerable<int> items = [3, 4];

		// Act
		deque.InsertRange(1, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 6,
			expectedCapacity: 6,
			expectedElements: [1, 3, 4, 2, 5, 6]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> inserts elements in the middle (second half).
	/// </summary>
	[Fact]
	public void InsertRange_InSecondHalf_InsertsElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 5, 6]);
		IEnumerable<int> items = [3, 4];

		// Act
		deque.InsertRange(3, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 6,
			expectedCapacity: 6,
			expectedElements: [1, 2, 5, 3, 4, 6]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> triggers capacity expansion when necessary.
	/// </summary>
	[Fact]
	public void InsertRange_WhenCapacityInsufficient_ExpandsCapacity()
	{
		// Arrange
		var deque = new Deque<int>(4);
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.AddToBack(3);
		deque.AddToBack(4);
		IEnumerable<int> items = [10, 20, 30];

		// Act
		deque.InsertRange(2, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 7,
			expectedCapacity: 7,
			expectedElements: [1, 2, 10, 20, 30, 3, 4]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> handles an empty collection without error.
	/// </summary>
	[Fact]
	public void InsertRange_WithEmptyCollection_NoChange()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IEnumerable<int> empty = [];

		// Act
		deque.InsertRange(1, empty);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> handles inserting the deque into itself.
	/// </summary>
	[Fact]
	public void InsertRange_InsertingSelfIntoSelf_CreatesCopyAndInsertsCorrectly()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		deque.InsertRange(1, deque);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 6,
			expectedCapacity: 6,
			expectedElements: [1, 1, 2, 3, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> works with
	/// non-<see cref="IReadOnlyCollection{T}"/> enumerables.
	/// </summary>
	[Fact]
	public void InsertRange_WithEnumerableNotCollection_MaterializesAndInserts()
	{
		// Arrange
		var deque = new Deque<int>([1, 5]);

		// Act
		deque.InsertRange(1, AsEnumerable(2, 3, 4));

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	#endregion

	#region InsertRange - Error / Exception

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> throws <see cref="ArgumentNullException"/>
	/// when collection is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void InsertRange_WithNullCollection_ThrowsArgumentNullException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IEnumerable<int> nullCollection = null!;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => deque.InsertRange(0, nullCollection));
		Assert.Equal("collection", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> throws <see cref="ArgumentOutOfRangeException"/>
	/// when index is negative.
	/// </summary>
	[Fact]
	public void InsertRange_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IEnumerable<int> items = [4, 5];

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.InsertRange(-1, items));
		Assert.Equal("index", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, IEnumerable{T})"/> throws <see cref="ArgumentOutOfRangeException"/>
	/// when index is greater than <see cref="Deque{T}.Count"/>.
	/// </summary>
	[Fact]
	public void InsertRange_WithIndexGreaterThanCount_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IEnumerable<int> items = [4, 5];

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.InsertRange(4, items));
		Assert.Equal("index", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	#endregion

	#region InsertRange(int, ReadOnlySpan<T>) - Happy Path

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> inserts elements at the beginning.
	/// </summary>
	[Fact]
	public void InsertRangeSpan_AtBeginning_InsertsElements()
	{
		// Arrange
		var deque = new Deque<int>([3, 4, 5]);
		ReadOnlySpan<int> items = [1, 2];

		// Act
		deque.InsertRange(0, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> inserts elements at the end.
	/// </summary>
	[Fact]
	public void InsertRangeSpan_AtEnd_InsertsElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		ReadOnlySpan<int> items = [4, 5];

		// Act
		deque.InsertRange(3, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> inserts elements in the first half.
	/// </summary>
	[Fact]
	public void InsertRangeSpan_InFirstHalf_InsertsElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 5, 6]);
		ReadOnlySpan<int> items = [3, 4];

		// Act
		deque.InsertRange(1, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 6,
			expectedCapacity: 6,
			expectedElements: [1, 3, 4, 2, 5, 6]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> inserts elements in the second half.
	/// </summary>
	[Fact]
	public void InsertRangeSpan_InSecondHalf_InsertsElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 5, 6]);
		ReadOnlySpan<int> items = [3, 4];

		// Act
		deque.InsertRange(3, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 6,
			expectedCapacity: 6,
			expectedElements: [1, 2, 5, 3, 4, 6]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> handles an empty span without error.
	/// </summary>
	[Fact]
	public void InsertRangeSpan_WithEmptySpan_NoChange()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		ReadOnlySpan<int> empty = [];

		// Act
		deque.InsertRange(1, empty);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> triggers capacity expansion when necessary.
	/// </summary>
	[Fact]
	public void InsertRangeSpan_WhenCapacityInsufficient_ExpandsCapacity()
	{
		// Arrange
		var deque = new Deque<int>(4);
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.AddToBack(3);
		deque.AddToBack(4);
		ReadOnlySpan<int> items = [10, 20, 30];

		// Act
		deque.InsertRange(2, items);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 7,
			expectedCapacity: 7,
			expectedElements: [1, 2, 10, 20, 30, 3, 4]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> works with stack-allocated arrays.
	/// </summary>
	[Fact]
	public void InsertRangeSpan_WithStackAllocatedSpan_InsertsElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 5]);
		Span<int> stackItems = stackalloc int[] { 2, 3, 4 };

		// Act
		deque.InsertRange(1, stackItems);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	#endregion

	#region InsertRange(int, ReadOnlySpan<T>) - Error / Exception

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> throws <see cref="ArgumentOutOfRangeException"/>
	/// when index is negative.
	/// </summary>
	[Fact]
	public void InsertRangeSpan_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => InsertRangeSpanHelper(deque, -1, [4, 5]));
		Assert.Equal("index", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.InsertRange(int, ReadOnlySpan{T})"/> throws <see cref="ArgumentOutOfRangeException"/>
	/// when index is greater than <see cref="Deque{T}.Count"/>.
	/// </summary>
	[Fact]
	public void InsertRangeSpan_WithIndexGreaterThanCount_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => InsertRangeSpanHelper(deque, 4, [4, 5]));
		Assert.Equal("index", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Helper method to wrap span-based InsertRange call for exception testing.
	/// </summary>
	private static void InsertRangeSpanHelper<T>(Deque<T> deque, int index, T[] items)
	{
		deque.InsertRange(index, items.AsSpan());
	}

	#endregion
}
