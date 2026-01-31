// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections;
using System.Runtime.CompilerServices;

namespace LumaCore.Core.Collections;

public sealed partial class Deque<T>
{
	/// <summary>
	/// Enumerates the elements of a <see cref="Deque{T}"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is a value type enumerator that avoids heap allocations during foreach loops.
	///     </para>
	///     <para>
	///     The enumerator does not detect modifications to the deque during enumeration.
	///     Modifying the deque while enumerating results in undefined behavior.
	///     </para>
	/// </remarks>
	public struct Enumerator : IEnumerator<T>
	{
		private readonly Deque<T> mDeque;
		private readonly int      mCount;
		private          int      mIndex;

		/// <summary>
		/// Initializes a new instance of the <see cref="Enumerator"/> struct.
		/// </summary>
		/// <param name="deque">The deque to enumerate.</param>
		internal Enumerator(Deque<T> deque)
		{
			mDeque = deque;
			mCount = deque.Count;
			mIndex = -1;
		}

		/// <summary>
		/// Releases all resources used by the <see cref="Enumerator"/>.
		/// </summary>
		public readonly void Dispose()
		{
			// No resources to dispose
		}

		/// <summary>
		/// Gets the element at the current position of the enumerator.
		/// </summary>
		/// <value>The element in the <see cref="Deque{T}"/> at the current position of the enumerator.</value>
		public readonly T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => mDeque.DoGetItem(mIndex);
		}

		/// <inheritdoc/>
		readonly object? IEnumerator.Current => Current;

		/// <summary>
		/// Advances the enumerator to the next element of the deque.
		/// </summary>
		/// <returns>
		/// <see langword="true"/> if the enumerator was successfully advanced to the next element;
		/// <see langword="false"/> if the enumerator has passed the end of the collection.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext()
		{
			int nextIndex = mIndex + 1;
			if (nextIndex < mCount)
			{
				mIndex = nextIndex;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Sets the enumerator to its initial position, which is before the first element in the collection.
		/// </summary>
		public void Reset() => mIndex = -1;
	}
}
