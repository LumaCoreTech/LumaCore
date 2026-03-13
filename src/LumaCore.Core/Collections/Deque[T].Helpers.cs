// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Runtime.CompilerServices;

namespace LumaCore.Core.Collections;

public sealed partial class Deque<T>
{
	#region Buffer Management

	/// <summary>
	/// Clears the logical portion of the buffer to allow garbage collection of reference types.
	/// </summary>
	private void ClearBuffer()
	{
		if (Count == 0) return;

		if (IsSplit)
		{
			// Buffer is split: clear from mOffset to end, then from 0 to wrap point.
			int firstPartLength = Capacity - mOffset;
			Array.Clear(mBuffer, mOffset, firstPartLength);
			Array.Clear(mBuffer, 0, Count - firstPartLength);
		}
		else
		{
			// Buffer is contiguous.
			Array.Clear(mBuffer, mOffset, Count);
		}
	}

	/// <summary>
	/// Ensures the buffer has space for at least one more element, doubling the capacity if necessary.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Postcondition:</b> <see cref="IsFull"/> is <see langword="false"/>.
	///     </para>
	///     <para>
	///     If <see cref="Capacity"/> is 0, it is set to 1. Otherwise, capacity is doubled.
	///     This provides amortized O(1) insertion performance.
	///     </para>
	/// </remarks>
	private void EnsureCapacityForOneElement()
	{
		if (IsFull)
		{
			Capacity = Capacity == 0 ? 1 : Capacity * 2;
		}
	}

	/// <summary>
	/// Copies all deque elements to the specified array in logical order.
	/// </summary>
	/// <param name="array">The destination array. Must have sufficient space starting at <paramref name="arrayIndex"/>.</param>
	/// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
	/// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <para>
	///     Handles the circular buffer by performing one or two <see cref="Array.Copy(Array, int, Array, int, int)"/>
	///     operations,
	///     depending on whether <see cref="IsSplit"/> is <see langword="true"/>.
	///     </para>
	/// </remarks>
	private void CopyToArray(Array array, int arrayIndex = 0)
	{
		ArgumentNullException.ThrowIfNull(array);

		if (IsSplit)
		{
			// The existing buffer is split, so we have to copy it in parts
			int length = Capacity - mOffset;
			Array.Copy(mBuffer, mOffset, array, arrayIndex, length);
			Array.Copy(mBuffer, 0, array, arrayIndex + length, Count - length);
		}
		else
		{
			// The existing buffer is whole
			Array.Copy(mBuffer, mOffset, array, arrayIndex, Count);
		}
	}

	/// <summary>
	/// Copies all deque elements to the specified span in logical order.
	/// </summary>
	/// <param name="destination">The destination span. Must have sufficient space for all elements.</param>
	/// <remarks>
	/// Handles the circular buffer by performing one or two span copy operations,
	/// depending on whether <see cref="IsSplit"/> is <see langword="true"/>.
	/// </remarks>
	private void CopyToSpan(Span<T> destination)
	{
		if (Count == 0) return;

		if (IsSplit)
		{
			int firstPartLength = Capacity - mOffset;
			mBuffer.AsSpan(mOffset, firstPartLength).CopyTo(destination);
			mBuffer.AsSpan(0, Count - firstPartLength).CopyTo(destination[firstPartLength..]);
		}
		else
		{
			mBuffer.AsSpan(mOffset, Count).CopyTo(destination);
		}
	}

	#endregion

	#region Index Arithmetic

	/// <summary>
	/// Converts a logical deque index to the corresponding physical buffer index.
	/// </summary>
	/// <param name="index">The zero-based logical index within the deque.</param>
	/// <returns>
	/// The physical index into <see cref="mBuffer"/> where the element at the logical <paramref name="index"/> resides.
	/// </returns>
	/// <remarks>
	/// Uses modulo arithmetic to handle wrap-around: <c>(index + mOffset) % Capacity</c>.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int DequeIndexToBufferIndex(int index)
	{
		return (index + mOffset) % Capacity;
	}

	/// <summary>
	/// Advances <see cref="mOffset"/> forward by the specified amount using modulo-<see cref="Capacity"/> arithmetic.
	/// </summary>
	/// <param name="value">The amount by which to advance. Must be non-negative.</param>
	/// <returns>The value of <see cref="mOffset"/> <b>before</b> the increment (post-increment semantics).</returns>
	/// <remarks>
	/// Used when removing elements from the front of the deque.
	/// </remarks>
	private int PostIncrement(int value)
	{
		int result = mOffset;
		mOffset += value;
		mOffset %= Capacity;
		return result;
	}

	/// <summary>
	/// Moves <see cref="mOffset"/> backward by the specified amount using modulo-<see cref="Capacity"/> arithmetic.
	/// </summary>
	/// <param name="value">The amount by which to move back. Must be in the range [0, <see cref="Capacity"/>].</param>
	/// <returns>The value of <see cref="mOffset"/> <b>after</b> the decrement (pre-decrement semantics).</returns>
	/// <remarks>
	/// Used when adding elements to the front of the deque. Handles wrap-around by adding <see cref="Capacity"/>
	/// when the result would be negative.
	/// </remarks>
	private int PreDecrement(int value)
	{
		mOffset -= value;
		if (mOffset < 0) mOffset += Capacity;
		return mOffset;
	}

	#endregion

	#region Element Access

	/// <summary>
	/// Retrieves the element at the specified logical index without bounds checking.
	/// </summary>
	/// <param name="index">The zero-based logical index. Must be in the range [0, <see cref="Count"/>).</param>
	/// <returns>The element at the specified logical index.</returns>
	/// <remarks>
	/// This method assumes the caller has already validated <paramref name="index"/>.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private T DoGetItem(int index)
	{
		return mBuffer[DequeIndexToBufferIndex(index)];
	}

	/// <summary>
	/// Stores an element at the specified logical index without bounds checking.
	/// </summary>
	/// <param name="index">The zero-based logical index. Must be in the range [0, <see cref="Count"/>).</param>
	/// <param name="item">The element to store.</param>
	/// <remarks>
	/// This method assumes the caller has already validated <paramref name="index"/>.
	/// </remarks>
	private void DoSetItem(int index, T item)
	{
		mBuffer[DequeIndexToBufferIndex(index)] = item;
	}

	#endregion

	#region Insertion Operations

	/// <summary>
	/// Inserts an element at the specified logical index, resizing the buffer if necessary.
	/// </summary>
	/// <param name="index">The zero-based logical index. Must be in the range [0, <see cref="Count"/>].</param>
	/// <param name="item">The element to insert.</param>
	/// <remarks>
	///     <para>
	///     This method assumes the caller has already validated <paramref name="index"/>.
	///     </para>
	///     <para>
	///     Optimizes for front/back insertions by delegating to <see cref="DoAddToFront"/> or <see cref="DoAddToBack"/>.
	///     Middle insertions use <see cref="DoInsertRange(int,IReadOnlyCollection{T})"/>.
	///     </para>
	/// </remarks>
	private void DoInsert(int index, T item)
	{
		EnsureCapacityForOneElement();

		if (index == 0)
		{
			DoAddToFront(item);
			return;
		}

		if (index == Count)
		{
			DoAddToBack(item);
			return;
		}

		DoInsertRange(index, [item]);
	}

	/// <summary>
	/// Appends an element to the back of the deque without capacity checks.
	/// </summary>
	/// <param name="value">The element to append.</param>
	/// <remarks>
	///     <para>
	///     <b>Precondition:</b> <see cref="IsFull"/> must be <see langword="false"/>.
	///     </para>
	///     <para>
	///     Increments <see cref="Count"/> after storing the element.
	///     </para>
	/// </remarks>
	private void DoAddToBack(T value)
	{
		mBuffer[DequeIndexToBufferIndex(Count)] = value;
		++Count;
	}

	/// <summary>
	/// Prepends an element to the front of the deque without capacity checks.
	/// </summary>
	/// <param name="value">The element to prepend.</param>
	/// <remarks>
	///     <para>
	///     <b>Precondition:</b> <see cref="IsFull"/> must be <see langword="false"/>.
	///     </para>
	///     <para>
	///     Decrements <see cref="mOffset"/> (with wrap-around) and increments <see cref="Count"/>.
	///     </para>
	/// </remarks>
	private void DoAddToFront(T value)
	{
		mBuffer[PreDecrement(1)] = value;
		++Count;
	}

	/// <summary>
	/// Inserts a range of elements at the specified logical index without capacity checks.
	/// </summary>
	/// <param name="index">The zero-based logical index at which to insert the elements.</param>
	/// <param name="collection">The elements to insert.</param>
	/// <remarks>
	///     <para>
	///     <b>Precondition:</b> <c><paramref name="collection"/>.Count + <see cref="Count"/> &lt;= <see cref="Capacity"/></c>.
	///     </para>
	///     <para>
	///     The algorithm shifts elements to make room for the new items. It chooses to shift either the front
	///     or back portion, depending on which requires moving fewer elements (determined by comparing
	///     <paramref name="index"/> to <c>Count / 2</c>).
	///     </para>
	/// </remarks>
	private void DoInsertRange(int index, IReadOnlyCollection<T> collection)
	{
		int collectionCount = collection.Count;

		// Make room in the existing list.
		if (index < Count / 2)
		{
			// Inserting into the first half of the list

			// Move lower items down: [0, index) -> [Capacity - collectionCount, Capacity - collectionCount + index)
			// This clears out the low "index" number of items, moving them "collectionCount" places down;
			// after rotation, there will be a "collectionCount"-sized hole at "index".
			int copyCount = index;
			int writeIndex = Capacity - collectionCount;
			for (int j = 0; j != copyCount; ++j)
			{
				mBuffer[DequeIndexToBufferIndex(writeIndex + j)] = mBuffer[DequeIndexToBufferIndex(j)];
			}

			// Rotate to the new view.
			PreDecrement(collectionCount);
		}
		else
		{
			// Inserting into the second half of the list.

			// Move higher items up: [index, count) -> [index + collectionCount, collectionCount + count).
			int copyCount = Count - index;
			int writeIndex = index + collectionCount;
			for (int j = copyCount - 1; j != -1; --j)
			{
				mBuffer[DequeIndexToBufferIndex(writeIndex + j)] = mBuffer[DequeIndexToBufferIndex(index + j)];
			}
		}

		// Copy new items into place.
		int i = index;
		foreach (T item in collection)
		{
			mBuffer[DequeIndexToBufferIndex(i)] = item;
			++i;
		}

		// Adjust valid count.
		Count += collectionCount;
	}

	/// <summary>
	/// Inserts elements from a span at the specified logical index.
	/// </summary>
	/// <param name="index">The zero-based logical index at which the new elements are inserted.</param>
	/// <param name="items">The span of elements to insert. Must not be empty.</param>
	/// <remarks>
	///     <para>
	///     <b>Precondition:</b> Capacity must be sufficient for the new elements.
	///     </para>
	///     <para>
	///     The insertion algorithm shifts existing elements to make room, choosing to move
	///     either the front or back portion depending on which requires fewer moves.
	///     </para>
	/// </remarks>
	private void DoInsertRange(int index, ReadOnlySpan<T> items)
	{
		int itemCount = items.Length;

		// Make room in the existing list.
		if (index < Count / 2)
		{
			// Inserting into the first half of the list

			// Move lower items down: [0, index) -> [Capacity - itemCount, Capacity - itemCount + index)
			int copyCount = index;
			int writeIndex = Capacity - itemCount;
			for (int j = 0; j != copyCount; ++j)
			{
				mBuffer[DequeIndexToBufferIndex(writeIndex + j)] = mBuffer[DequeIndexToBufferIndex(j)];
			}

			// Rotate to the new view.
			PreDecrement(itemCount);
		}
		else
		{
			// Inserting into the second half of the list.

			// Move higher items up: [index, count) -> [index + itemCount, itemCount + count).
			int copyCount = Count - index;
			int writeIndex = index + itemCount;
			for (int j = copyCount - 1; j != -1; --j)
			{
				mBuffer[DequeIndexToBufferIndex(writeIndex + j)] = mBuffer[DequeIndexToBufferIndex(index + j)];
			}
		}

		// Copy new items into place.
		for (int i = 0; i < itemCount; i++)
		{
			mBuffer[DequeIndexToBufferIndex(index + i)] = items[i];
		}

		// Adjust valid count.
		Count += itemCount;
	}

	#endregion

	#region Removal Operations

	/// <summary>
	/// Removes the element at the specified logical index without bounds checking.
	/// </summary>
	/// <param name="index">The zero-based logical index. Must be in the range [0, <see cref="Count"/>).</param>
	/// <remarks>
	///     <para>
	///     This method assumes the caller has already validated <paramref name="index"/>.
	///     </para>
	///     <para>
	///     Optimizes for front/back removals by delegating to <see cref="DoRemoveFromFront"/> or
	///     <see cref="DoRemoveFromBack"/>.
	///     Middle removals use <see cref="DoRemoveRange"/>.
	///     </para>
	/// </remarks>
	private void DoRemoveAt(int index)
	{
		if (index == 0)
		{
			DoRemoveFromFront();
			return;
		}

		if (index == Count - 1)
		{
			DoRemoveFromBack();
			return;
		}

		DoRemoveRange(index, 1);
	}

	/// <summary>
	/// Removes and returns the last element without emptiness checks.
	/// </summary>
	/// <returns>The removed element that was previously at the back of the deque.</returns>
	/// <remarks>
	///     <para>
	///     <b>Precondition:</b> <see cref="IsEmpty"/> must be <see langword="false"/>.
	///     </para>
	///     <para>
	///     Decrements <see cref="Count"/> after retrieving the element.
	///     For reference types, clears the slot to allow garbage collection.
	///     </para>
	/// </remarks>
	private T DoRemoveFromBack()
	{
		int bufferIndex = DequeIndexToBufferIndex(Count - 1);
		T ret = mBuffer[bufferIndex];
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			mBuffer[bufferIndex] = default!;
		}
		--Count;
		return ret;
	}

	/// <summary>
	/// Removes and returns the first element without emptiness checks.
	/// </summary>
	/// <returns>The removed element that was previously at the front of the deque.</returns>
	/// <remarks>
	///     <para>
	///     <b>Precondition:</b> <see cref="IsEmpty"/> must be <see langword="false"/>.
	///     </para>
	///     <para>
	///     Advances <see cref="mOffset"/> (with wrap-around) and decrements <see cref="Count"/>.
	///     For reference types, clears the slot to allow garbage collection.
	///     </para>
	/// </remarks>
	private T DoRemoveFromFront()
	{
		int bufferIndex = PostIncrement(1);
		T ret = mBuffer[bufferIndex];
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			mBuffer[bufferIndex] = default!;
		}
		--Count;
		return ret;
	}

	/// <summary>
	/// Removes a contiguous range of elements starting at the specified logical index.
	/// </summary>
	/// <param name="index">The zero-based logical index at which removal begins.</param>
	/// <param name="collectionCount">The number of elements to remove. Must be in the range (0, <see cref="Count"/>].</param>
	/// <remarks>
	///     <para>
	///     The algorithm shifts remaining elements to close the gap. It chooses to shift either the front
	///     or back portion, depending on which requires moving fewer elements.
	///     </para>
	///     <para>
	///     Optimizes for front/back removal by adjusting <see cref="mOffset"/> or <see cref="Count"/> directly.
	///     For reference types, clears vacated slots to allow garbage collection.
	///     </para>
	/// </remarks>
	private void DoRemoveRange(int index, int collectionCount)
	{
		if (index == 0)
		{
			// Removing from the beginning: clear slots and rotate to the new view.
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			{
				for (int j = 0; j < collectionCount; j++)
				{
					mBuffer[DequeIndexToBufferIndex(j)] = default!;
				}
			}
			PostIncrement(collectionCount);
			Count -= collectionCount;
			return;
		}

		if (index == Count - collectionCount)
		{
			// Removing from the ending: clear slots and trim the existing view.
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			{
				for (int j = 0; j < collectionCount; j++)
				{
					mBuffer[DequeIndexToBufferIndex(index + j)] = default!;
				}
			}
			Count -= collectionCount;
			return;
		}

		if (index + collectionCount / 2 < Count / 2)
		{
			// Removing from first half of list.

			// Move lower items up: [0, index) -> [collectionCount, collectionCount + index)
			int copyCount = index;
			int writeIndex = collectionCount;
			for (int j = copyCount - 1; j != -1; --j)
			{
				mBuffer[DequeIndexToBufferIndex(writeIndex + j)] = mBuffer[DequeIndexToBufferIndex(j)];
			}

			// Clear vacated slots at the front
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			{
				for (int j = 0; j < collectionCount; j++)
				{
					mBuffer[DequeIndexToBufferIndex(j)] = default!;
				}
			}

			// Rotate to new view
			PostIncrement(collectionCount);
		}
		else
		{
			// Removing from second half of list.

			// Move higher items down: [index + collectionCount, count) -> [index, count - collectionCount)
			int copyCount = Count - collectionCount - index;
			int readIndex = index + collectionCount;
			for (int j = 0; j != copyCount; ++j)
			{
				mBuffer[DequeIndexToBufferIndex(index + j)] = mBuffer[DequeIndexToBufferIndex(readIndex + j)];
			}

			// Clear vacated slots at the back
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			{
				int newCount = Count - collectionCount;
				for (int j = 0; j < collectionCount; j++)
				{
					mBuffer[DequeIndexToBufferIndex(newCount + j)] = default!;
				}
			}
		}

		// Adjust valid count
		Count -= collectionCount;
	}

	#endregion

	#region Validation Helpers

	/// <summary>
	/// Validates that <paramref name="index"/> is a valid insertion point for a source of the specified length.
	/// </summary>
	/// <param name="sourceLength">The current length of the source collection.</param>
	/// <param name="index">The index to validate.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="index"/> is less than 0 or greater than <paramref name="sourceLength"/>.
	/// </exception>
	/// <remarks>
	/// An insertion index is valid in the range [0, <paramref name="sourceLength"/>], inclusive on both ends.
	/// </remarks>
	private static void CheckNewIndexArgument(int sourceLength, int index)
	{
		if (index < 0 || index > sourceLength)
		{
			throw new ArgumentOutOfRangeException(
				nameof(index),
				$"Invalid new index {index} for source length {sourceLength}");
		}
	}

	/// <summary>
	/// Validates that <paramref name="index"/> refers to an existing element in a source of the specified length.
	/// </summary>
	/// <param name="sourceLength">The current length of the source collection.</param>
	/// <param name="index">The index to validate.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="index"/> is less than 0 or greater than or equal to <paramref name="sourceLength"/>.
	/// </exception>
	/// <remarks>
	/// An existing element index is valid in the range [0, <paramref name="sourceLength"/>), exclusive on the upper bound.
	/// </remarks>
	private static void CheckExistingIndexArgument(int sourceLength, int index)
	{
		if (index < 0 || index >= sourceLength)
		{
			throw new ArgumentOutOfRangeException(
				nameof(index),
				$"Invalid existing index {index} for source length {sourceLength}");
		}
	}

	/// <summary>
	/// Validates that the specified range [<paramref name="offset"/>, <paramref name="offset"/> + <paramref name="count"/>)
	/// is valid for a source of the specified length.
	/// </summary>
	/// <param name="sourceLength">The current length of the source collection.</param>
	/// <param name="offset">The starting index of the range.</param>
	/// <param name="count">The number of elements in the range.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="offset"/> or <paramref name="count"/> is less than 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// The range [<paramref name="offset"/>, <paramref name="offset"/> + <paramref name="count"/>)
	/// exceeds the bounds [0, <paramref name="sourceLength"/>).
	/// </exception>
	/// <remarks>
	/// Zero-element ranges are allowed, including a zero-element range at index <paramref name="sourceLength"/>.
	/// </remarks>
	private static void CheckRangeArguments(int sourceLength, int offset, int count)
	{
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(offset), $"Invalid offset {offset}");
		}

		if (count < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count), $"Invalid count {count}");
		}

		if (sourceLength - offset < count)
		{
			throw new ArgumentException(
				$"Invalid offset ({offset}) or count ({count}) for source length ({sourceLength})");
		}
	}

	#endregion
}
