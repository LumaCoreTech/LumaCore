// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

// ReSharper disable CollectionNeverUpdated.Local

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region CopyTo (Span) - Error / Exception

	/// <summary>
	/// Verifies that <see cref="Deque{T}.CopyTo(Span{T})"/> throws <see cref="ArgumentException"/> when span is too small.
	/// </summary>
	[Fact]
	public void CopyToSpan_WhenSpanTooSmall_ThrowsArgumentException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);
		int[] destinationArray = new int[3];

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => deque.CopyTo(destinationArray.AsSpan()));
		Assert.Equal("destination", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	#endregion

	#region CopyTo (Array) - Happy Path

	/// <summary>
	/// Verifies that <see cref="Deque{T}.CopyTo(T[], int)"/> copies elements to an array starting at the specified index.
	/// </summary>
	[Fact]
	public void CopyToArray_CopiesElementsAtIndex()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		int[] array = new int[5];

		// Act
		deque.CopyTo(array, 1);

		// Assert
		Assert.Equal([0, 1, 2, 3, 0], array);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	#endregion

	#region CopyTo (Span) - Happy Path

	/// <summary>
	/// Verifies that <see cref="Deque{T}.CopyTo(Span{T})"/> copies elements to a span.
	/// </summary>
	[Fact]
	public void CopyToSpan_CopiesElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		Span<int> destination = stackalloc int[5];

		// Act
		deque.CopyTo(destination);

		// Assert
		Assert.Equal(1, destination[0]);
		Assert.Equal(2, destination[1]);
		Assert.Equal(3, destination[2]);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.CopyTo(Span{T})"/> handles empty deque without error.
	/// </summary>
	[Fact]
	public void CopyToSpan_WhenEmpty_DoesNotThrow()
	{
		// Arrange - pre-fill with sentinel values to verify nothing is copied
		var deque = new Deque<int>();
		int[] destinationArray = [-1, -1, -1, -1, -1];
		Span<int> destination = destinationArray;

		// Act
		deque.CopyTo(destination);

		// Assert - sentinel values should remain unchanged
		Assert.Equal([-1, -1, -1, -1, -1], destinationArray);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.CopyTo(Span{T})"/> handles wrap-around correctly.
	/// </summary>
	[Fact]
	public void CopyToSpan_WithWrapAround_CopiesCorrectly()
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
		Span<int> destination = stackalloc int[4];

		// Act
		deque.CopyTo(destination);

		// Assert
		Assert.Equal(3, destination[0]);
		Assert.Equal(4, destination[1]);
		Assert.Equal(5, destination[2]);
		Assert.Equal(6, destination[3]);
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 4,
			expectedElements: [3, 4, 5, 6]);
	}

	#endregion

	#region CopyTo (Array) - Error / Exception

	/// <summary>
	/// Verifies that <see cref="Deque{T}.CopyTo(T[], int)"/> throws <see cref="ArgumentNullException"/>
	/// when array is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void CopyToArray_WithNullArray_ThrowsArgumentNullException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => deque.CopyTo(null!, 0));
		Assert.Equal("array", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.CopyTo(T[], int)"/> throws <see cref="ArgumentOutOfRangeException"/> when arrayIndex
	/// is negative.
	/// </summary>
	[Fact]
	public void CopyToArray_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		int[] array = new int[5];

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.CopyTo(array, -1));
		Assert.Equal("offset", ex.ParamName);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.CopyTo(T[], int)"/> throws <see cref="ArgumentException"/> when array is too small.
	/// </summary>
	[Fact]
	public void CopyToArray_WhenArrayTooSmall_ThrowsArgumentException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);
		int[] array = new int[3];

		// Act + Assert
		Assert.Throws<ArgumentException>(() => deque.CopyTo(array, 0));
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	#endregion

	#region TryCopyTo - Happy Path

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryCopyTo"/> returns <see langword="true"/> and copies elements when destination is
	/// sufficient.
	/// </summary>
	[Fact]
	public void TryCopyTo_WhenDestinationSufficient_ReturnsTrueAndCopies()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);
		Span<int> destination = new int[5];

		// Act
		bool result = deque.TryCopyTo(destination);

		// Assert
		Assert.True(result);
		Assert.Equal([1, 2, 3, 4, 5], destination.ToArray());
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryCopyTo"/> returns <see langword="true"/> when destination is larger than needed.
	/// </summary>
	[Fact]
	public void TryCopyTo_WhenDestinationLarger_ReturnsTrueAndCopies()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		Span<int> destination = new int[10];

		// Act
		bool result = deque.TryCopyTo(destination);

		// Assert
		Assert.True(result);
		Assert.Equal([1, 2, 3, 0, 0, 0, 0, 0, 0, 0], destination.ToArray());
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryCopyTo"/> returns <see langword="true"/> for an empty deque.
	/// </summary>
	[Fact]
	public void TryCopyTo_WhenEmpty_ReturnsTrueWithoutCopying()
	{
		// Arrange - pre-fill with sentinel values to verify nothing is copied
		var deque = new Deque<int>();
		int[] destinationArray = [-1, -1, -1, -1, -1];
		Span<int> destination = destinationArray;

		// Act
		bool result = deque.TryCopyTo(destination);

		// Assert - sentinel values should remain unchanged
		Assert.True(result);
		Assert.Equal([-1, -1, -1, -1, -1], destinationArray);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryCopyTo"/> returns <see langword="true"/> for an empty deque with empty
	/// destination.
	/// </summary>
	[Fact]
	public void TryCopyTo_WhenEmptyWithEmptyDestination_ReturnsTrue()
	{
		// Arrange
		var deque = new Deque<int>();
		Span<int> destination = [];

		// Act
		bool result = deque.TryCopyTo(destination);

		// Assert
		Assert.True(result);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryCopyTo"/> works correctly with wrap-around buffer.
	/// </summary>
	[Fact]
	public void TryCopyTo_WithWrapAround_CopiesCorrectly()
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
		Span<int> destination = new int[4];

		// Act
		bool result = deque.TryCopyTo(destination);

		// Assert
		Assert.True(result);
		Assert.Equal([3, 4, 5, 6], destination.ToArray());
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 4,
			expectedElements: [3, 4, 5, 6]);
	}

	#endregion

	#region TryCopyTo - Failure Cases

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryCopyTo"/> returns <see langword="false"/> when destination is too small.
	/// </summary>
	[Fact]
	public void TryCopyTo_WhenDestinationTooSmall_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);
		Span<int> destination = new int[3];

		// Act
		bool result = deque.TryCopyTo(destination);

		// Assert
		Assert.False(result);
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryCopyTo"/> returns <see langword="false"/>
	/// when destination is empty but deque is not.
	/// </summary>
	[Fact]
	public void TryCopyTo_WhenDestinationEmptyButDequeNotEmpty_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		Span<int> destination = [];

		// Act
		bool result = deque.TryCopyTo(destination);

		// Assert
		Assert.False(result);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	#endregion
}
