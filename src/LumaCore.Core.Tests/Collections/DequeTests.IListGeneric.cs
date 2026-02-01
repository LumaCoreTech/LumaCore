// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

// ReSharper disable CollectionNeverQueried.Local
// ReSharper disable CollectionNeverUpdated.Local

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region ICollection<T>.Add

	/// <summary>
	/// Verifies that <see cref="ICollection{T}.Add"/> appends an element to the end.
	/// </summary>
	[Fact]
	public void ICollectionAdd_AppendsElementToEnd()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		ICollection<int> collection = deque;

		// Act
		collection.Add(4);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 6,
			expectedElements: [1, 2, 3, 4]);
	}

	#endregion

	#region ICollection<T>.IsReadOnly

	/// <summary>
	/// Verifies that <see cref="ICollection{T}.IsReadOnly"/> returns <see langword="false"/>.
	/// </summary>
	[Fact]
	public void ICollectionIsReadOnly_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>();
		ICollection<int> collection = deque;

		// Act + Assert
		Assert.False(collection.IsReadOnly);
	}

	#endregion

	#region Insert

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Insert"/> adds an element at the beginning.
	/// </summary>
	[Fact]
	public void Insert_AtBeginning_InsertsElement()
	{
		// Arrange
		var deque = new Deque<int>([2, 3, 4]);

		// Act
		deque.Insert(0, 1);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 6,
			expectedElements: [1, 2, 3, 4]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Insert"/> adds an element at the end.
	/// </summary>
	[Fact]
	public void Insert_AtEnd_InsertsElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		deque.Insert(3, 4);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 6,
			expectedElements: [1, 2, 3, 4]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Insert"/> adds an element in the middle.
	/// </summary>
	[Fact]
	public void Insert_InMiddle_InsertsElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 4]);

		// Act
		deque.Insert(2, 3);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 6,
			expectedElements: [1, 2, 3, 4]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Insert"/> throws <see cref="ArgumentOutOfRangeException"/> when index is negative.
	/// </summary>
	[Fact]
	public void Insert_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.Insert(-1, 0));
		Assert.Equal("index", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Insert"/> throws <see cref="ArgumentOutOfRangeException"/>
	/// when index is greater than <see cref="Deque{T}.Count"/>.
	/// </summary>
	[Fact]
	public void Insert_WithIndexGreaterThanCount_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.Insert(4, 0));
		Assert.Equal("index", ex.ParamName);
	}

	#endregion

	#region RemoveAt

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveAt"/> removes the element at the beginning.
	/// </summary>
	[Fact]
	public void RemoveAt_AtBeginning_RemovesElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4]);

		// Act
		deque.RemoveAt(0);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [2, 3, 4]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveAt"/> removes the element at the end.
	/// </summary>
	[Fact]
	public void RemoveAt_AtEnd_RemovesElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4]);

		// Act
		deque.RemoveAt(3);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveAt"/> removes the element in the middle.
	/// </summary>
	[Fact]
	public void RemoveAt_InMiddle_RemovesElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4]);

		// Act
		deque.RemoveAt(2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [1, 2, 4]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveAt"/> throws <see cref="ArgumentOutOfRangeException"/> when index is negative.
	/// </summary>
	[Fact]
	public void RemoveAt_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.RemoveAt(-1));
		Assert.Equal("index", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveAt"/> throws <see cref="ArgumentOutOfRangeException"/>
	/// when index is equal to <see cref="Deque{T}.Count"/>.
	/// </summary>
	[Fact]
	public void RemoveAt_WithIndexEqualToCount_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.RemoveAt(3));
		Assert.Equal("index", ex.ParamName);
	}

	#endregion

	#region IndexOf

	/// <summary>
	/// Verifies that <see cref="Deque{T}.IndexOf"/> returns the correct index when element exists.
	/// </summary>
	[Fact]
	public void IndexOf_WhenElementExists_ReturnsIndex()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30, 40]);

		// Act
		int index = deque.IndexOf(30);

		// Assert
		Assert.Equal(2, index);
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 4,
			expectedElements: [10, 20, 30, 40]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.IndexOf"/> returns -1 when element does not exist.
	/// </summary>
	[Fact]
	public void IndexOf_WhenElementDoesNotExist_ReturnsMinusOne()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30]);

		// Act
		int index = deque.IndexOf(99);

		// Assert
		Assert.Equal(-1, index);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.IndexOf"/> returns the first occurrence when duplicates exist.
	/// </summary>
	[Fact]
	public void IndexOf_WithDuplicates_ReturnsFirstOccurrence()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 2, 1]);

		// Act
		int index = deque.IndexOf(2);

		// Assert
		Assert.Equal(1, index);
	}

	#endregion

	#region Contains

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Contains"/> returns <see langword="true"/> when element exists.
	/// </summary>
	[Fact]
	public void Contains_WhenElementExists_ReturnsTrue()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		bool result = deque.Contains(2);

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Contains"/> returns <see langword="false"/> when element does not exist.
	/// </summary>
	[Fact]
	public void Contains_WhenElementDoesNotExist_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		bool result = deque.Contains(99);

		// Assert
		Assert.False(result);
	}

	#endregion

	#region Remove

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Remove"/> returns <see langword="true"/> and removes the element when it exists.
	/// </summary>
	[Fact]
	public void Remove_WhenElementExists_ReturnsTrueAndRemoves()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4]);

		// Act
		bool result = deque.Remove(3);

		// Assert
		Assert.True(result);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [1, 2, 4]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Remove"/> returns <see langword="false"/> when element does not exist.
	/// </summary>
	[Fact]
	public void Remove_WhenElementDoesNotExist_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		bool result = deque.Remove(99);

		// Assert
		Assert.False(result);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Remove"/> removes only the first occurrence when duplicates exist.
	/// </summary>
	[Fact]
	public void Remove_WithDuplicates_RemovesOnlyFirstOccurrence()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 2, 1]);

		// Act
		bool result = deque.Remove(2);

		// Assert
		Assert.True(result);
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 5,
			expectedElements: [1, 3, 2, 1]);
	}

	#endregion
}
