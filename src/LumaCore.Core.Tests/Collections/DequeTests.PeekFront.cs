// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region PeekFront - Error / Exception

	/// <summary>
	/// Verifies that <see cref="Deque{T}.PeekFront"/> throws <see cref="InvalidOperationException"/> when the deque is empty.
	/// </summary>
	[Fact]
	public void PeekFront_WhenEmpty_ThrowsInvalidOperationException()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => deque.PeekFront());
		Assert.Equal("The deque is empty.", ex.Message);
	}

	#endregion

	#region PeekFront - Happy Path

	/// <summary>
	/// Verifies that <see cref="Deque{T}.PeekFront"/> returns the first element without removing it.
	/// </summary>
	[Fact]
	public void PeekFront_FromNonEmptyDeque_ReturnsFirstElementWithoutRemoving()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30]);

		// Act
		int result = deque.PeekFront();

		// Assert
		Assert.Equal(10, result);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [10, 20, 30]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryPeekFront"/> returns <see langword="true"/> and the element when successful.
	/// </summary>
	[Fact]
	public void TryPeekFront_WhenNotEmpty_ReturnsTrueAndElement()
	{
		// Arrange
		var deque = new Deque<int>([42, 43]);

		// Act
		bool success = deque.TryPeekFront(out int result);

		// Assert
		Assert.True(success);
		Assert.Equal(42, result);
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 2,
			expectedElements: [42, 43]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryPeekFront"/> returns <see langword="false"/> when the deque is empty.
	/// </summary>
	[Fact]
	public void TryPeekFront_WhenEmpty_ReturnsFalseAndDefault()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		bool success = deque.TryPeekFront(out int result);

		// Assert
		Assert.False(success);
		Assert.Equal(0, result);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	#endregion
}
