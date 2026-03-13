// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections;

namespace LumaCore.Core.Collections;

/// <summary>
/// Provides helper methods for working with collections.
/// </summary>
public static class CollectionHelpers
{
	/// <summary>
	/// Reifies the specified enumerable as a read-only collection, avoiding unnecessary allocations
	/// when the source already implements an appropriate collection interface.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="source">The enumerable to reify as a collection.</param>
	/// <returns>
	///     <para>
	///     A read-only collection containing the same elements as <paramref name="source"/>.
	///     </para>
	///     <para>
	///     The method uses the following strategy to minimize allocations:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             If <paramref name="source"/> is already an <see cref="IReadOnlyCollection{T}"/>, it is returned directly.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             If <paramref name="source"/> is an <see cref="ICollection{T}"/>, a lightweight wrapper is returned.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             If <paramref name="source"/> is a non-generic <see cref="ICollection"/>, a casting wrapper is returned.
	///             </description>
	///         </item>
	///         <item>
	///             <description>Otherwise, the enumerable is materialized into a new <see cref="List{T}"/>.</description>
	///         </item>
	///     </list>
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
	public static IReadOnlyCollection<T> ReifyCollection<T>(IEnumerable<T> source)
	{
		return source switch
		{
			null                             => throw new ArgumentNullException(nameof(source)),
			IReadOnlyCollection<T> result    => result,
			ICollection<T> collection        => new CollectionWrapper<T>(collection),
			ICollection nonGenericCollection => new NonGenericCollectionWrapper<T>(nonGenericCollection),
			var _                            => new List<T>(source)
		};
	}

	private sealed class CollectionWrapper<T>(ICollection<T> collection) : IReadOnlyCollection<T>
	{
		private readonly ICollection<T> mCollection = collection ?? throw new ArgumentNullException(nameof(collection));

		public int Count => mCollection.Count;

		public IEnumerator<T> GetEnumerator()
		{
			return mCollection.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	private sealed class NonGenericCollectionWrapper<T>(ICollection collection) : IReadOnlyCollection<T>
	{
		private readonly ICollection mCollection = collection ?? throw new ArgumentNullException(nameof(collection));

		public int Count => mCollection.Count;

		public IEnumerator<T> GetEnumerator()
		{
			return mCollection.Cast<T>().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
