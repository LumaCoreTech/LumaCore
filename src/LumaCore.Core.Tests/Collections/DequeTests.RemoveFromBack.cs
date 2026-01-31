// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region RemoveFromBack - Error / Exception

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromBack"/> throws <see cref="InvalidOperationException"/>
	/// when the deque is empty.
	/// </summary>
	[Fact]
	public void RemoveFromBack_WhenEmpty_ThrowsInvalidOperationException()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => deque.RemoveFromBack());
		Assert.Equal("The deque is empty.", ex.Message);
	}

	#endregion

	#region RemoveFromBack - Happy Path

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromBack"/> removes and returns the last element.
	/// </summary>
	[Fact]
	public void RemoveFromBack_FromNonEmptyDeque_ReturnsAndRemovesLastElement()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30]);

		// Act
		int result = deque.RemoveFromBack();

		// Assert
		Assert.Equal(30, result);
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 3,
			expectedElements: [10, 20]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromBack"/> removes all elements sequentially.
	/// </summary>
	[Fact]
	public void RemoveFromBack_AllElements_EmptiesDeque()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		int third = deque.RemoveFromBack();
		int second = deque.RemoveFromBack();
		int first = deque.RemoveFromBack();

		// Assert
		Assert.Equal(3, third);
		Assert.Equal(2, second);
		Assert.Equal(1, first);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 3,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromBack"/> clears reference type slots for garbage collection.
	/// </summary>
	[Fact]
	public void RemoveFromBack_WithReferenceType_ClearsSlotForGarbageCollection()
	{
		// Arrange
		var deque = new Deque<string>(["a", "b", "c"]);

		// Act
		string result = deque.RemoveFromBack();

		// Assert
		Assert.Equal("c", result);
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 3,
			expectedElements: ["a", "b"]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryRemoveFromBack"/> returns <see langword="true"/> and the element when successful.
	/// </summary>
	[Fact]
	public void TryRemoveFromBack_WhenNotEmpty_ReturnsTrueAndElement()
	{
		// Arrange
		var deque = new Deque<int>([42, 43]);

		// Act
		bool success = deque.TryRemoveFromBack(out int result);

		// Assert
		Assert.True(success);
		Assert.Equal(43, result);
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 2,
			expectedElements: [42]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryRemoveFromBack"/> returns <see langword="false"/> when the deque is empty.
	/// </summary>
	[Fact]
	public void TryRemoveFromBack_WhenEmpty_ReturnsFalseAndDefault()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		bool success = deque.TryRemoveFromBack(out int result);

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
	/// Verifies that <see cref="Deque{T}.TryRemoveFromBack"/> returns <see langword="false"/> and
	/// <see langword="null"/> for reference types when empty.
	/// </summary>
	[Fact]
	public void TryRemoveFromBack_WithReferenceTypeWhenEmpty_ReturnsFalseAndNull()
	{
		// Arrange
		var deque = new Deque<string>();

		// Act
		bool success = deque.TryRemoveFromBack(out string? result);

		// Assert
		Assert.False(success);
		Assert.Null(result);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.RemoveFromBack"/> works correctly with wrap-around.
	/// </summary>
	[Fact]
	public void RemoveFromBack_WithWrapAround_HandlesOffsetCorrectly()
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
		int result = deque.RemoveFromBack();

		// Assert
		Assert.Equal(6, result);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 4,
			expectedElements: [3, 4, 5]);
	}

	#endregion
}
