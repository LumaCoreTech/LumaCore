// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region PeekBack()

	/// <summary>
	/// Verifies that <see cref="Deque{T}.PeekBack"/> returns the last element without removing it.
	/// </summary>
	[Fact]
	public void PeekBack_FromNonEmptyDeque_ReturnsLastElementWithoutRemoving()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30]);

		// Act
		int result = deque.PeekBack();

		// Assert
		Assert.Equal(30, result);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [10, 20, 30]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.PeekBack"/> throws <see cref="InvalidOperationException"/> when the deque is empty.
	/// </summary>
	[Fact]
	public void PeekBack_WhenEmpty_ThrowsInvalidOperationException()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => deque.PeekBack());
		Assert.Equal("The deque is empty.", ex.Message);
	}

	#endregion

	#region TryPeekBack()

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryPeekBack"/> returns <see langword="true"/> and the element when successful.
	/// </summary>
	[Fact]
	public void TryPeekBack_WhenNotEmpty_ReturnsTrueAndElement()
	{
		// Arrange
		var deque = new Deque<int>([42, 43]);

		// Act
		bool success = deque.TryPeekBack(out int result);

		// Assert
		Assert.True(success);
		Assert.Equal(43, result);
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 2,
			expectedElements: [42, 43]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.TryPeekBack"/> returns <see langword="false"/> when the deque is empty.
	/// </summary>
	[Fact]
	public void TryPeekBack_WhenEmpty_ReturnsFalseAndDefault()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		bool success = deque.TryPeekBack(out int result);

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
