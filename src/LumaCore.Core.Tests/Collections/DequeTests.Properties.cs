// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	#region Capacity

	/// <summary>
	/// Verifies that setting <see cref="Deque{T}.Capacity"/> to a larger value preserves elements.
	/// </summary>
	[Fact]
	public void Capacity_SetToLargerValue_PreservesElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);

		// Act
		deque.Capacity = 10;

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 10,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that setting <see cref="Deque{T}.Capacity"/> to the same value does nothing.
	/// </summary>
	[Fact]
	public void Capacity_SetToSameValue_NoChange()
	{
		// Arrange
		var deque = new Deque<int>(8);
		deque.AddToBack(1);
		deque.AddToBack(2);

		// Act
		deque.Capacity = 8;

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 8,
			expectedElements: [1, 2]);
	}

	/// <summary>
	/// Verifies that setting <see cref="Deque{T}.Capacity"/> to <see cref="Deque{T}.Count"/> trims excess capacity.
	/// </summary>
	[Fact]
	public void Capacity_SetToCount_TrimsExcessCapacity()
	{
		// Arrange
		var deque = new Deque<int>(10);
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.AddToBack(3);

		// Act
		deque.Capacity = 3;

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that setting <see cref="Deque{T}.Capacity"/> normalizes a wrapped buffer.
	/// </summary>
	[Fact]
	public void Capacity_SetWithWrapAround_NormalizesBuffer()
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
		// Buffer is wrapped: [5, 6, 3, 4] with offset at 2

		// Act
		deque.Capacity = 8;

		// Assert
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 8,
			expectedElements: [3, 4, 5, 6]);
	}

	/// <summary>
	/// Verifies that setting <see cref="Deque{T}.Capacity"/> below <see cref="Deque{T}.Count"/>
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public void Capacity_SetBelowCount_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);

		// Act + Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => deque.Capacity = 3);
		Assert.Equal("value", ex.ParamName);
	}

	#endregion

	#region IsEmpty

	/// <summary>
	/// Verifies that <see cref="Deque{T}.IsEmpty"/> returns <see langword="true"/> when deque is empty.
	/// </summary>
	[Fact]
	public void IsEmpty_WhenEmpty_ReturnsTrue()
	{
		// Arrange
		var deque = new Deque<int>();

		// Act + Assert
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.IsEmpty"/> returns <see langword="false"/> when deque has elements.
	/// </summary>
	[Fact]
	public void IsEmpty_WhenNotEmpty_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>([1]);

		// Act + Assert
		AssertDequeState(
			deque,
			expectedCount: 1,
			expectedCapacity: 1,
			expectedElements: [1]);
	}

	#endregion

	#region IsFull

	/// <summary>
	/// Verifies that <see cref="Deque{T}.IsFull"/> returns <see langword="true"/>
	/// when <see cref="Deque{T}.Count"/> equals <see cref="Deque{T}.Capacity"/>.
	/// </summary>
	[Fact]
	public void IsFull_WhenCountEqualsCapacity_ReturnsTrue()
	{
		// Arrange
		var deque = new Deque<int>(3);
		deque.AddToBack(1);
		deque.AddToBack(2);
		deque.AddToBack(3);

		// Act + Assert
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.IsFull"/> returns <see langword="false"/>
	/// when <see cref="Deque{T}.Count"/> is less than <see cref="Deque{T}.Capacity"/>.
	/// </summary>
	[Fact]
	public void IsFull_WhenCountLessThanCapacity_ReturnsFalse()
	{
		// Arrange
		var deque = new Deque<int>(4);
		deque.AddToBack(1);
		deque.AddToBack(2);

		// Act + Assert
		AssertDequeState(
			deque,
			expectedCount: 2,
			expectedCapacity: 4,
			expectedElements: [1, 2]);
	}

	#endregion
}
