// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	/// <summary>
	/// Verifies that <see cref="Deque{T}.ToArray"/> returns a new array with all elements.
	/// </summary>
	[Fact]
	public void ToArray_ReturnsNewArrayWithAllElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);

		// Act
		int[] array = deque.ToArray();

		// Assert
		Assert.Equal([1, 2, 3, 4, 5], array);
		Assert.NotSame(deque.ToArray(), array); // should be a new array each time
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.ToArray"/> returns an empty array for an empty deque.
	/// </summary>
	[Fact]
	public void ToArray_WhenEmpty_ReturnsEmptyArray()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act
		int[] array = deque.ToArray();

		// Assert
		Assert.Empty(array);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}
}
