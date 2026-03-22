// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections;

using LumaCore.Core.Collections;

using Xunit;

// ReSharper disable MoveLocalFunctionAfterJumpStatement

namespace LumaCore.Core.Tests.Collections;

/// <summary>
/// Unit tests for <see cref="CollectionHelpers"/>.
/// </summary>
[Trait("Category", "Collections")]
public class CollectionHelpersTests
{
	/// <summary>
	/// Verifies that <see cref="CollectionHelpers.ReifyCollection{T}"/> returns the same instance when source is already
	/// <see cref="IReadOnlyCollection{T}"/>.
	/// </summary>
	[Fact]
	public void ReifyCollection_WhenSourceIsIReadOnlyCollection_ReturnsSameInstance()
	{
		// Arrange
		IReadOnlyCollection<int> source = new List<int> { 1, 2, 3 };

		// Act
		IReadOnlyCollection<int> result = CollectionHelpers.ReifyCollection(source);

		// Assert
		Assert.Same(source, result);
	}

	/// <summary>
	/// Verifies that <see cref="CollectionHelpers.ReifyCollection{T}"/> wraps <see cref="ICollection{T}"/> that is not
	/// <see cref="IReadOnlyCollection{T}"/>.
	/// </summary>
	[Fact]
	public void ReifyCollection_WhenSourceIsICollectionGeneric_WrapsAndReturnsCorrectCount()
	{
		// Arrange
		ICollection<int> source = new HashSet<int> { 1, 2, 3 };

		// Act
		IReadOnlyCollection<int> result = CollectionHelpers.ReifyCollection(source);

		// Assert
		Assert.Equal(3, result.Count);
		Assert.Equal([1, 2, 3], result.OrderBy(x => x).ToArray());
	}

	/// <summary>
	/// Verifies that <see cref="CollectionHelpers.ReifyCollection{T}"/> wraps non-generic <see cref="ICollection"/> that also
	/// implements <see cref="IEnumerable{T}"/>.
	/// </summary>
	[Fact]
	public void ReifyCollection_WhenSourceIsNonGenericICollection_WrapsAndReturnsCorrectCount()
	{
		// Arrange - use wrapper that implements IEnumerable<T> and ICollection (non-generic)
		var source = new NonGenericCollectionEnumerable<int>(new ArrayList { 1, 2, 3 });

		// Act
		IReadOnlyCollection<int> result = CollectionHelpers.ReifyCollection(source);

		// Assert
		Assert.Equal(3, result.Count);
		Assert.Equal([1, 2, 3], result.ToArray());
	}

	/// <summary>
	/// Verifies that <see cref="CollectionHelpers.ReifyCollection{T}"/> materializes a pure enumerable.
	/// </summary>
	[Fact]
	public void ReifyCollection_WhenSourceIsPureEnumerable_MaterializesToList()
	{
		// Arrange
		static IEnumerable<int> Generate()
		{
			yield return 1;
			yield return 2;
			yield return 3;
		}

		IEnumerable<int> enumerable = Generate();

		// Act
		IReadOnlyCollection<int> result = CollectionHelpers.ReifyCollection(enumerable);

		// Assert
		Assert.Equal(3, result.Count);
		Assert.Equal([1, 2, 3], result.ToArray());
	}

	/// <summary>
	/// Verifies that the CollectionWrapper returned by <see cref="CollectionHelpers.ReifyCollection{T}"/> can enumerate its
	/// elements.
	/// </summary>
	[Fact]
	public void ReifyCollection_CollectionWrapper_CanEnumerate()
	{
		// Arrange - use a collection that implements ICollection<T> but not IReadOnlyCollection<T>
		var source = new TestCollection<int>([1, 2, 3]);

		// Act
		IReadOnlyCollection<int> result = CollectionHelpers.ReifyCollection(source);
		var items = new List<int>();
		foreach (int item in result)
		{
			items.Add(item);
		}

		// Assert
		Assert.Equal(3, result.Count);
		Assert.Equal([1, 2, 3], items);
	}

	/// <summary>
	/// Verifies that the CollectionWrapper's non-generic <see cref="IEnumerable.GetEnumerator"/> works.
	/// </summary>
	[Fact]
	public void ReifyCollection_CollectionWrapper_NonGenericEnumeratorWorks()
	{
		// Arrange
		var source = new TestCollection<int>([1, 2, 3]);

		// Act
		IReadOnlyCollection<int> result = CollectionHelpers.ReifyCollection(source);
		IEnumerable enumerable = result;
		var items = new List<object?>();
		foreach (object? item in enumerable)
		{
			items.Add(item);
		}

		// Assert
		Assert.Equal(3, items.Count);
	}

	/// <summary>
	/// Verifies that the NonGenericCollectionWrapper returned by <see cref="CollectionHelpers.ReifyCollection{T}"/> can
	/// enumerate its elements.
	/// </summary>
	[Fact]
	public void ReifyCollection_NonGenericCollectionWrapper_CanEnumerate()
	{
		// Arrange - use wrapper that implements IEnumerable<T> and ICollection (non-generic)
		var source = new NonGenericCollectionEnumerable<int>(new ArrayList { 1, 2, 3 });

		// Act
		IReadOnlyCollection<int> result = CollectionHelpers.ReifyCollection(source);
		var items = new List<int>();
		foreach (int item in result)
		{
			items.Add(item);
		}

		// Assert
		Assert.Equal([1, 2, 3], items);
	}

	/// <summary>
	/// Verifies that the NonGenericCollectionWrapper's non-generic <see cref="IEnumerable.GetEnumerator"/> works.
	/// </summary>
	[Fact]
	public void ReifyCollection_NonGenericCollectionWrapper_NonGenericEnumeratorWorks()
	{
		// Arrange - use wrapper that implements IEnumerable<T> and ICollection (non-generic)
		var source = new NonGenericCollectionEnumerable<int>(new ArrayList { 1, 2, 3 });

		// Act
		IReadOnlyCollection<int> result = CollectionHelpers.ReifyCollection(source);
		IEnumerable enumerable = result;
		var items = new List<object?>();
		foreach (object? item in enumerable)
		{
			items.Add(item);
		}

		// Assert
		Assert.Equal(3, items.Count);
	}

	/// <summary>
	/// Verifies that <see cref="CollectionHelpers.ReifyCollection{T}"/> throws <see cref="ArgumentNullException"/> when source
	/// is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void ReifyCollection_WhenSourceIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		IEnumerable<int> source = null!;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => CollectionHelpers.ReifyCollection(source));
		Assert.Equal("source", ex.ParamName);
	}

	#region Helper Classes

	/// <summary>
	/// A test collection that implements <see cref="ICollection{T}"/> but NOT <see cref="IReadOnlyCollection{T}"/>.
	/// </summary>
	/// <remarks>
	/// Used to test the CollectionWrapper path in <see cref="CollectionHelpers.ReifyCollection{T}"/>.
	/// </remarks>
	private sealed class TestCollection<T>(T[] items) : ICollection<T>
	{
		private readonly List<T> mItems = [.. items];

		public int  Count      => mItems.Count;
		public bool IsReadOnly => false;

		public void             Add(T item)                       => mItems.Add(item);
		public void             Clear()                           => mItems.Clear();
		public bool             Contains(T item)                  => mItems.Contains(item);
		public void             CopyTo(T[] array, int arrayIndex) => mItems.CopyTo(array, arrayIndex);
		public bool             Remove(T   item) => mItems.Remove(item);
		public IEnumerator<T>   GetEnumerator()  => mItems.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator()  => GetEnumerator();
	}

	/// <summary>
	/// A wrapper that implements <see cref="IEnumerable{T}"/> and <see cref="ICollection"/> (non-generic) but NOT
	/// <see cref="IReadOnlyCollection{T}"/> or <see cref="ICollection{T}"/>.
	/// </summary>
	/// <remarks>
	/// Used to test the NonGenericCollectionWrapper path in <see cref="CollectionHelpers.ReifyCollection{T}"/>.
	/// </remarks>
	private sealed class NonGenericCollectionEnumerable<T>(ICollection source) : IEnumerable<T>, ICollection
	{
		private readonly ICollection mSource = source;

		public int    Count          => mSource.Count;
		public object SyncRoot       => mSource.SyncRoot;
		public bool   IsSynchronized => mSource.IsSynchronized;

		public void             CopyTo(Array array, int index) => mSource.CopyTo(array, index);
		public IEnumerator<T>   GetEnumerator() => mSource.Cast<T>().GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => mSource.GetEnumerator();
	}

	#endregion
}
