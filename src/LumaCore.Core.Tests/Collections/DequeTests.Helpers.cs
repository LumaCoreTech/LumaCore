// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Collections;

using Xunit;

namespace LumaCore.Core.Tests.Collections;

public partial class DequeTests
{
	/// <summary>
	/// Asserts the complete state of a <see cref="Deque{T}"/> against expected values.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="deque">The deque to verify.</param>
	/// <param name="expectedCount">Expected number of elements.</param>
	/// <param name="expectedCapacity">Expected buffer capacity.</param>
	/// <param name="expectedElements">Expected elements in logical order.</param>
	/// <remarks>
	/// This method verifies all observable state properties of the deque:
	/// <list type="bullet">
	///     <item><see cref="Deque{T}.Count"/> equals <paramref name="expectedCount"/></item>
	///     <item><see cref="Deque{T}.IsEmpty"/> is consistent with <paramref name="expectedCount"/></item>
	///     <item><see cref="Deque{T}.Capacity"/> equals <paramref name="expectedCapacity"/></item>
	///     <item><see cref="Deque{T}.IsFull"/> is consistent with count and capacity</item>
	///     <item><see cref="Deque{T}.ToArray"/> returns <paramref name="expectedElements"/></item>
	/// </list>
	/// </remarks>
	// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
	private static void AssertDequeState<T>(
		Deque<T> deque,
		int      expectedCount,
		int      expectedCapacity,
		T[]      expectedElements)
	{
		Assert.Equal(expectedCount, deque.Count);
		Assert.Equal(expectedCount == 0, deque.IsEmpty);
		Assert.Equal(expectedCapacity, deque.Capacity);
		Assert.Equal(expectedCount == expectedCapacity, deque.IsFull);
		Assert.Equal(expectedElements, deque.ToArray());
	}
	// ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local

	/// <summary>
	/// Yields elements from the source array one by one, simulating a pure <see cref="IEnumerable{T}"/>
	/// that is not an <see cref="ICollection{T}"/> or <see cref="IReadOnlyCollection{T}"/>.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="source">The elements to yield.</param>
	/// <returns>An <see cref="IEnumerable{T}"/> that yields each element via <c>yield return</c>.</returns>
	/// <remarks>
	/// Use this helper to test code paths that handle pure enumerables (e.g., iterator methods)
	/// rather than collections with a known <see cref="ICollection{T}.Count"/>.
	/// </remarks>
	private static IEnumerable<T> AsEnumerable<T>(params T[] source)
	{
		foreach (T item in source)
		{
			yield return item;
		}
	}
}
