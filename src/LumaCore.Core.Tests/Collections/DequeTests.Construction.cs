// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region Deque()

	/// <summary>
	/// Verifies that the default <see cref="Deque{T}()"/> constructor creates an empty deque with the default capacity.
	/// </summary>
	[Fact]
	public void Constructor_WithNoArguments_CreatesEmptyDequeWithDefaultCapacity()
	{
		// Arrange + Act
		var deque = new Deque<int>();

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	#endregion

	#region Deque(int)

	/// <summary>
	/// Verifies that the <see cref="Deque{T}(int)"/> constructor creates an empty deque with the specified capacity.
	/// </summary>
	/// <param name="capacity">The initial capacity to use.</param>
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(16)]
	[InlineData(100)]
	public void Constructor_WithCapacity_CreatesEmptyDequeWithSpecifiedCapacity(int capacity)
	{
		// Arrange + Act
		var deque = new Deque<int>(capacity);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: capacity,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}(int)"/> constructor throws <see cref="ArgumentOutOfRangeException"/> when given a
	/// negative capacity.
	/// </summary>
	[Fact]
	public void Constructor_WithNegativeCapacity_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		int negativeCapacity = -1;

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Deque<int>(negativeCapacity));
		Assert.Equal("capacity", ex.ParamName);
	}

	#endregion

	#region Deque(IEnumerable<T>)

	/// <summary>
	/// Verifies that the <see cref="Deque{T}(IEnumerable{T})"/> constructor creates a deque
	/// with all elements from the source collection.
	/// </summary>
	[Fact]
	public void Constructor_WithCollection_CreatesDequeWithAllElements()
	{
		// Arrange
		int[] source = [1, 2, 3, 4, 5];

		// Act
		var deque = new Deque<int>(source);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}(IEnumerable{T})"/> constructor handles an empty collection
	/// by using the default capacity.
	/// </summary>
	[Fact]
	public void Constructor_WithEmptyCollection_CreatesEmptyDequeWithDefaultCapacity()
	{
		// Arrange
		IEnumerable<int> emptySource = [];

		// Act
		var deque = new Deque<int>(emptySource);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}(IEnumerable{T})"/> constructor works with
	/// non-<see cref="IReadOnlyCollection{T}"/> enumerables (requiring materialization).
	/// </summary>
	[Fact]
	public void Constructor_WithEnumerableNotCollection_MaterializesElements()
	{
		// Arrange + Act
		var deque = new Deque<int>(AsEnumerable(10, 20, 30));

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [10, 20, 30]);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}(IEnumerable{T})"/> constructor throws <see cref="ArgumentNullException"/>
	/// when given a <see langword="null"/> collection.
	/// </summary>
	[Fact]
	public void Constructor_WithNullCollection_ThrowsArgumentNullException()
	{
		// Arrange
		IEnumerable<int> nullCollection = null!;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new Deque<int>(nullCollection));
		Assert.Equal("collection", ex.ParamName);
	}

	#endregion

	#region Deque(ReadOnlySpan<T>)

	/// <summary>
	/// Verifies that the <see cref="Deque{T}(ReadOnlySpan{T})"/> constructor creates a deque with all elements from the span.
	/// </summary>
	[Fact]
	public void Constructor_WithSpan_CreatesDequeWithAllElements()
	{
		// Arrange
		ReadOnlySpan<int> source = [1, 2, 3, 4, 5];

		// Act
		var deque = new Deque<int>(source);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}(ReadOnlySpan{T})"/> constructor handles an empty span by using the default
	/// capacity.
	/// </summary>
	[Fact]
	public void Constructor_WithEmptySpan_CreatesEmptyDequeWithDefaultCapacity()
	{
		// Arrange
		ReadOnlySpan<int> emptySource = [];

		// Act
		var deque = new Deque<int>(emptySource);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that the <see cref="Deque{T}(ReadOnlySpan{T})"/> constructor works with stack-allocated arrays.
	/// </summary>
	[Fact]
	public void Constructor_WithStackAllocatedSpan_CreatesDequeWithAllElements()
	{
		// Arrange
#pragma warning disable IDE0302
		Span<int> stackItems = stackalloc int[] { 10, 20, 30 };
#pragma warning restore IDE0302

		// Act
		var deque = new Deque<int>(stackItems);

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [10, 20, 30]);
	}

	#endregion
}
