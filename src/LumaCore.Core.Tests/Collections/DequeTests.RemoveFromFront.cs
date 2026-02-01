// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region RemoveFromFront()

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromFront"/> removes and returns the first element.
	/// </summary>
	[Fact]
	public void RemoveFromFront_FromNonEmptyDeque_ReturnsAndRemovesFirstElement()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30]);

		// Act
		int result = deque.RemoveFromFront();

		// Assert
		Assert.Equal(10, result);
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 3,
			expectedElements: [20, 30]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromFront"/> removes all elements sequentially.
	/// </summary>
	[Fact]
	public void RemoveFromFront_AllElements_EmptiesDeque()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		int first = deque.RemoveFromFront();
		int second = deque.RemoveFromFront();
		int third = deque.RemoveFromFront();

		// Assert
		Assert.Equal(1, first);
		Assert.Equal(2, second);
		Assert.Equal(3, third);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 3,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromFront"/> clears reference type slots for garbage collection.
	/// </summary>
	[Fact]
	public void RemoveFromFront_WithReferenceType_ClearsSlotForGarbageCollection()
	{
		// Arrange
		var deque = new Deque<string>(["a", "b", "c"]);

		// Act
		string result = deque.RemoveFromFront();

		// Assert
		Assert.Equal("a", result);
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 3,
			expectedElements: ["b", "c"]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromFront"/> works correctly with wrap-around.
	/// </summary>
	[Fact]
	public void RemoveFromFront_WithWrapAround_HandlesOffsetCorrectly()
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

		// Act
		int result = deque.RemoveFromFront();

		// Assert
		Assert.Equal(3, result);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [4, 5, 6]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromFront"/> throws <see cref="InvalidOperationException"/> when the deque is
	/// empty.
	/// </summary>
	[Fact]
	public void RemoveFromFront_WhenEmpty_ThrowsInvalidOperationException()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => deque.RemoveFromFront());
		Assert.Equal("The deque is empty.", ex.Message);
	}

	#endregion

	#region TryRemoveFromFront()

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryRemoveFromFront"/> returns <see langword="true"/> and the element when successful.
	/// </summary>
	[Fact]
	public void TryRemoveFromFront_WhenNotEmpty_ReturnsTrueAndElement()
	{
		// Arrange
		var deque = new Deque<int>([42, 43]);

		// Act
		bool success = deque.TryRemoveFromFront(out int result);

		// Assert
		Assert.True(success);
		Assert.Equal(42, result);
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 2,
			expectedElements: [43]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryRemoveFromFront"/> returns <see langword="false"/> when the deque is empty.
	/// </summary>
	[Fact]
	public void TryRemoveFromFront_WhenEmpty_ReturnsFalseAndDefault()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		bool success = deque.TryRemoveFromFront(out int result);

		// Assert
		Assert.False(success);
		Assert.Equal(default, result);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryRemoveFromFront"/> returns <see langword="false"/> and <see langword="null"/> for
	/// reference types when empty.
	/// </summary>
	[Fact]
	public void TryRemoveFromFront_WithReferenceTypeWhenEmpty_ReturnsFalseAndNull()
	{
		// Arrange
		var deque = new Deque<string>();

		// Act
		bool success = deque.TryRemoveFromFront(out string? result);

		// Assert
		Assert.False(success);
		Assert.Null(result);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	#endregion
}
