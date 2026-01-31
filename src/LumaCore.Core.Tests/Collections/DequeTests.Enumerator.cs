// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections;

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	/// <summary>
	/// Verifies that the enumerator iterates through all elements in order.
	/// </summary>
	[Fact]
	public void Enumerator_IteratesAllElements()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3, 4, 5]);
		var results = new List<int>();

		// Act
		foreach (int item in deque)
		{
			results.Add(item);
		}

		// Assert
		Assert.Equal([1, 2, 3, 4, 5], results);
		AssertDequeState(
			deque,
			expectedCount: 5,
			expectedCapacity: 5,
			expectedElements: [1, 2, 3, 4, 5]);
	}

	/// <summary>
	/// Verifies that the enumerator works correctly with an empty deque.
	/// </summary>
	[Fact]
	public void Enumerator_WithEmptyDeque_IteratesNothing()
	{
		// Arrange
		var deque = new Deque<int>();
		var results = new List<int>();

		// Act
		foreach (int item in deque)
		{
			results.Add(item);
		}

		// Assert
		Assert.Empty(results);
		AssertDequeState(
			deque,
			expectedCount: 0,
			expectedCapacity: 8,
			expectedElements: []);
	}

	/// <summary>
	/// Verifies that the enumerator works correctly with wrap-around buffer.
	/// </summary>
	[Fact]
	public void Enumerator_WithWrapAround_IteratesCorrectly()
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
		var results = new List<int>();

		// Act
		foreach (int item in deque)
		{
			results.Add(item);
		}

		// Assert
		Assert.Equal([3, 4, 5, 6], results);
		AssertDequeState(
			deque,
			expectedCount: 4,
			expectedCapacity: 4,
			expectedElements: [3, 4, 5, 6]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Enumerator.Reset"/> returns the enumerator to its initial position.
	/// </summary>
	[Fact]
	public void Enumerator_Reset_ReturnsToInitialPosition()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		Deque<int>.Enumerator enumerator = deque.GetEnumerator();

		// Act - first enumeration
		var firstPass = new List<int>();
		while (enumerator.MoveNext())
		{
			firstPass.Add(enumerator.Current);
		}

		// Reset and enumerate again
		enumerator.Reset();
		var secondPass = new List<int>();
		while (enumerator.MoveNext())
		{
			secondPass.Add(enumerator.Current);
		}

		// Assert
		Assert.Equal([1, 2, 3], firstPass);
		Assert.Equal([1, 2, 3], secondPass);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [1, 2, 3]);
	}

	/// <summary>
	/// Verifies that <see cref="IEnumerable{T}.GetEnumerator"/> returns a working enumerator.
	/// </summary>
	[Fact]
	public void IEnumerableGeneric_GetEnumerator_ReturnsWorkingEnumerator()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30]);
		IEnumerable<int> enumerable = deque;
		var results = new List<int>();

		// Act
		using IEnumerator<int> enumerator = enumerable.GetEnumerator();
		while (enumerator.MoveNext())
		{
			results.Add(enumerator.Current);
		}

		// Assert
		Assert.Equal([10, 20, 30], results);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [10, 20, 30]);
	}

	/// <summary>
	/// Verifies that <see cref="IEnumerable.GetEnumerator"/> returns a working enumerator.
	/// </summary>
	[Fact]
	public void IEnumerable_GetEnumerator_ReturnsWorkingEnumerator()
	{
		// Arrange
		var deque = new Deque<int>([10, 20, 30]);
		IEnumerable enumerable = deque;
		var results = new List<int>();

		// Act
		IEnumerator enumerator = enumerable.GetEnumerator();
		while (enumerator.MoveNext())
		{
			results.Add((int)enumerator.Current!);
		}

		// Assert
		Assert.Equal([10, 20, 30], results);
		AssertDequeState(
			deque,
			expectedCount: 3,
			expectedCapacity: 3,
			expectedElements: [10, 20, 30]);
	}

	/// <summary>
	/// Verifies that <see cref="Deque{T}.Enumerator.Dispose"/> does not throw.
	/// </summary>
	[Fact]
	public void Enumerator_Dispose_DoesNotThrow()
	{
		// Arrange
		var deque = new Deque<int>([1, 2, 3]);
		Deque<int>.Enumerator enumerator = deque.GetEnumerator();
		enumerator.MoveNext();

		// Act + Assert
		enumerator.Dispose(); // Should not throw
	}

	/// <summary>
	/// Verifies that <see cref="IEnumerator.Current"/> returns the same value as <see cref="Deque{T}.Enumerator.Current"/>.
	/// </summary>
	[Fact]
	public void Enumerator_IEnumeratorCurrent_ReturnsSameAsTypedCurrent()
	{
		// Arrange
		var deque = new Deque<int>([42]);
		IEnumerator enumerator = deque.GetEnumerator();

		// Act
		enumerator.MoveNext();
		object? current = enumerator.Current;

		// Assert
		Assert.Equal(42, current);
	}
}
