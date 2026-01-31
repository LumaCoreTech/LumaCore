// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections;

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region IList.Add

	/// <summary>
	/// Verifies that <see cref="IList.Add"/> appends an element and returns the correct index.
	/// </summary>
	[Fact]
	public void IListAdd_AppendsElementAndReturnsIndex()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IList list = deque;

		// Act
		int index = list.Add(4);

		// Assert
		Assert.Equal(3, index);
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 6,
			expectedElements: [1, 2, 3, 4]);
	}

	/// <summary>
	/// Verifies that <see cref="IList.Add"/> throws <see cref="ArgumentNullException"/>
	/// when value is <see langword="null"/> for non-nullable value type.
	/// </summary>
	[Fact]
	public void IListAdd_WithNullForValueType_ThrowsArgumentNullException()
	{
		// Arrange
		var deque = new Deque<int>();
		IList list = deque;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => list.Add(null));
		Assert.Equal("value", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="IList.Add"/> throws <see cref="ArgumentException"/> when value is of incorrect type.
	/// </summary>
	[Fact]
	public void IListAdd_WithIncorrectType_ThrowsArgumentException()
	{
		// Arrange
		var deque = new Deque<int>();
		IList list = deque;

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => list.Add("not an int"));
		Assert.Equal("value", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="IList.Add"/> accepts <see langword="null"/> for reference types.
	/// </summary>
	[Fact]
	public void IListAdd_WithNullForReferenceType_AddsNull()
	{
		// Arrange
		var deque = new Deque<string?>();
		IList list = deque;

		// Act
		int index = list.Add(null);

		// Assert
		Assert.Equal(0, index);
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 8,
			expectedElements: [null]);
	}

	#endregion

	#region IList.Contains

	/// <summary>
	/// Verifies that <see cref="IList.Contains"/> returns <see langword="true"/> when element exists.
	/// </summary>
	[Fact]
	public void IListContains_WhenElementExists_ReturnsTrue()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IList list = deque;

		// Act + Assert
		Assert.True(list.Contains(2));
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="IList.Contains"/> returns <see langword="false"/> for wrong type.
	/// </summary>
	[Fact]
	public void IListContains_WithWrongType_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IList list = deque;

		// Act + Assert
		Assert.False(list.Contains("not an int"));
	}

	#endregion

	#region IList.IndexOf

	/// <summary>
	/// Verifies that <see cref="IList.IndexOf"/> returns the correct index.
	/// </summary>
	[Fact]
	public void IListIndexOf_WhenElementExists_ReturnsIndex()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30]);
		IList list = deque;

		// Act
		int index = list.IndexOf(20);

		// Assert
		Assert.Equal(1, index);
	}

	/// <summary>
	/// Verifies that <see cref="IList.IndexOf"/> returns -1 for wrong type.
	/// </summary>
	[Fact]
	public void IListIndexOf_WithWrongType_ReturnsMinusOne()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IList list = deque;

		// Act
		int index = list.IndexOf("not an int");

		// Assert
		Assert.Equal(-1, index);
	}

	#endregion

	#region IList.Insert

	/// <summary>
	/// Verifies that <see cref="IList.Insert"/> inserts an element at the specified index.
	/// </summary>
	[Fact]
	public void IListInsert_InsertsElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 3]);
		IList list = deque;

		// Act
		list.Insert(1, 2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="IList.Insert"/> throws <see cref="ArgumentNullException"/>
	/// when value is <see langword="null"/> for non-nullable value type.
	/// </summary>
	[Fact]
	public void IListInsert_WithNullForValueType_ThrowsArgumentNullException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2]);
		IList list = deque;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => list.Insert(0, null));
		Assert.Equal("value", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 2,
			expectedElements: [1, 2]);
	}

	/// <summary>
	/// Verifies that <see cref="IList.Insert"/> throws <see cref="ArgumentException"/> when value is of incorrect type.
	/// </summary>
	[Fact]
	public void IListInsert_WithIncorrectType_ThrowsArgumentException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2]);
		IList list = deque;

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => list.Insert(0, "not an int"));
		Assert.Equal("value", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 2,
			expectedElements: [1, 2]);
	}

	#endregion

	#region IList.Remove

	/// <summary>
	/// Verifies that <see cref="IList.Remove"/> removes the element when it exists.
	/// </summary>
	[Fact]
	public void IListRemove_WhenElementExists_RemovesElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IList list = deque;

		// Act
		list.Remove(2);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 3,
			expectedElements: [1, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="IList.Remove"/> does nothing for wrong type.
	/// </summary>
	[Fact]
	public void IListRemove_WithWrongType_DoesNothing()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IList list = deque;

		// Act
		list.Remove("not an int");

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	#endregion

	#region IList Indexer

	/// <summary>
	/// Verifies that <see cref="IList"/> indexer get returns the correct element.
	/// </summary>
	[Fact]
	public void IListIndexer_Get_ReturnsElement()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30]);
		IList list = deque;

		// Act + Assert
		Assert.Equal(20, list[1]);
	}

	/// <summary>
	/// Verifies that <see cref="IList"/> indexer set updates the element.
	/// </summary>
	[Fact]
	public void IListIndexer_Set_UpdatesElement()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IList list = deque;

		// Act
		list[1] = 99;

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 99, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="IList"/> indexer set throws <see cref="ArgumentNullException"/>
	/// when value is <see langword="null"/> for non-nullable value type.
	/// </summary>
	[Fact]
	public void IListIndexer_SetWithNullForValueType_ThrowsArgumentNullException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IList list = deque;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => list[0] = null);
		Assert.Equal("value", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="IList"/> indexer set throws <see cref="ArgumentException"/> when value is of incorrect type.
	/// </summary>
	[Fact]
	public void IListIndexer_SetWithIncorrectType_ThrowsArgumentException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		IList list = deque;

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => list[0] = "not an int");
		Assert.Equal("value", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	#endregion

	#region IList Properties

	/// <summary>
	/// Verifies that <see cref="ICollection.IsSynchronized"/> returns <see langword="false"/>.
	/// </summary>
	[Fact]
	public void ICollectionIsSynchronized_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>();
		ICollection collection = deque;

		// Act + Assert
		Assert.False(collection.IsSynchronized);
	}

	/// <summary>
	/// Verifies that <see cref="ICollection.SyncRoot"/> returns the deque instance.
	/// </summary>
	[Fact]
	public void ICollectionSyncRoot_ReturnsDequeInstance()
	{
		// Arrange
		var deque = new Deque<int>();
		ICollection collection = deque;

		// Act + Assert
		Assert.Same(deque, collection.SyncRoot);
	}

	/// <summary>
	/// Verifies that <see cref="IList.IsFixedSize"/> returns <see langword="false"/>.
	/// </summary>
	[Fact]
	public void IListIsFixedSize_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>();
		IList list = deque;

		// Act + Assert
		Assert.False(list.IsFixedSize);
	}

	/// <summary>
	/// Verifies that <see cref="IList.IsReadOnly"/> returns <see langword="false"/>.
	/// </summary>
	[Fact]
	public void IListIsReadOnly_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>();
		IList list = deque;

		// Act + Assert
		Assert.False(list.IsReadOnly);
	}

	#endregion

	#region ICollection.CopyTo

	/// <summary>
	/// Verifies that <see cref="ICollection.CopyTo"/> copies elements to the array.
	/// </summary>
	[Fact]
	public void ICollectionCopyTo_CopiesToArray()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		ICollection collection = deque;
		int[] array = new int[5];

		// Act
		collection.CopyTo(array, 1);

		// Assert
		Assert.Equal([0, 1, 2, 3, 0], array);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="ICollection.CopyTo"/> throws <see cref="ArgumentNullException"/>
	/// when array is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void ICollectionCopyTo_WithNullArray_ThrowsArgumentNullException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		ICollection collection = deque;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => collection.CopyTo(null!, 0));
		Assert.Equal("array", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="ICollection.CopyTo"/> throws <see cref="ArgumentOutOfRangeException"/> when index is negative.
	/// </summary>
	[Fact]
	public void ICollectionCopyTo_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		ICollection collection = deque;
		int[] array = new int[5];

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => collection.CopyTo(array, -1));
		Assert.Equal("offset", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="ICollection.CopyTo"/> throws <see cref="ArgumentException"/> when array is too small.
	/// </summary>
	[Fact]
	public void ICollectionCopyTo_WhenArrayTooSmall_ThrowsArgumentException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);
		ICollection collection = deque;
		int[] array = new int[3];

		// Act + Assert
		Assert.Throws<ArgumentException>(() => collection.CopyTo(array, 0));
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that <see cref="ICollection.CopyTo"/> throws <see cref="ArgumentException"/>
	/// when array is of an incompatible type.
	/// </summary>
	[Fact]
	public void ICollectionCopyTo_WithIncompatibleArrayType_ThrowsArgumentException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		ICollection collection = deque;
		string[] array = new string[5]; // Incompatible type

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => collection.CopyTo(array, 0));
		Assert.Equal("array", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="ICollection.CopyTo"/> throws <see cref="ArgumentException"/>
	/// when array is multidimensional.
	/// </summary>
	[Fact]
	public void ICollectionCopyTo_WithMultidimensionalArray_ThrowsArgumentException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		ICollection collection = deque;
		int[,] array = new int[3, 3]; // Multidimensional

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => collection.CopyTo(array, 0));
		Assert.Equal("array", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	#endregion
}
