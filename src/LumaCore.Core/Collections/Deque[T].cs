// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace LumaCore.Core.Collections;

/// <summary>
/// A double-ended queue (deque) backed by a circular buffer.
/// </summary>
/// <remarks>
///     <para>
///     This data structure provides the following time complexities:
///     </para>
///     <list type="bullet">
///         <item>
///             <description><b>O(1)</b> indexed access via <see cref="this[int]"/>.</description>
///         </item>
///         <item>
///             <description>
///             <b>O(1)</b> insertion/removal at front via <see cref="AddToFront"/>/
///             <see cref="RemoveFromFront"/>.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>O(1)</b> amortized insertion/removal at back via <see cref="AddToBack"/>/
///             <see cref="RemoveFromBack"/>.
///             </description>
///         </item>
///         <item>
///             <description><b>O(N)</b> insertion/removal elsewhere (slower as the index approaches the middle).</description>
///         </item>
///     </list>
/// </remarks>
/// <typeparam name="T">The type of elements contained in the deque.</typeparam>
public sealed partial class Deque<T> : IList<T>, IReadOnlyList<T>, IList
{
	/// <summary>
	/// The default initial capacity used when no capacity is specified.
	/// </summary>
	/// <remarks>
	/// This value is chosen as a power of 2 for efficient modulo operations during index calculations.
	/// </remarks>
	private const int DefaultCapacity = 8;

	/// <summary>
	/// The circular buffer that stores the deque elements.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Elements are stored in a "virtual" contiguous sequence that may wrap around the end of the array.
	///     The actual start position is determined by <see cref="mOffset"/>.
	///     </para>
	///     <para>
	///     The length of this array equals <see cref="Capacity"/>, not <see cref="Count"/>.
	///     </para>
	/// </remarks>
	private T[] mBuffer;

	/// <summary>
	/// The zero-based index into <see cref="mBuffer"/> where the logical first element resides.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This value is always in the range [0, <see cref="Capacity"/>).
	///     </para>
	///     <para>
	///     When elements are added to the front, this offset decreases (wrapping around if necessary).
	///     When elements are removed from the front, this offset increases.
	///     </para>
	/// </remarks>
	private int mOffset;

	/// <summary>
	/// Initializes a new instance of the <see cref="Deque{T}"/> class with the specified initial capacity.
	/// </summary>
	/// <param name="capacity">The initial capacity of the internal buffer.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="capacity"/> is less than 0.
	/// </exception>
	/// <remarks>
	/// Use this constructor when the approximate number of elements is known in advance to avoid
	/// unnecessary buffer reallocations.
	/// </remarks>
	public Deque(int capacity)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 0);
		mBuffer = new T[capacity];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Deque{T}"/> class with elements copied from the specified collection.
	/// </summary>
	/// <param name="collection">The collection whose elements are copied to the new deque.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="collection"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     The capacity is set to exactly match the number of elements in <paramref name="collection"/>,
	///     or to <see cref="DefaultCapacity"/> if the collection is empty.
	///     </para>
	///     <para>
	///     Uses <see cref="CollectionHelpers.ReifyCollection{T}"/> to efficiently determine the collection size.
	///     </para>
	/// </remarks>
	public Deque(IEnumerable<T> collection)
	{
		ArgumentNullException.ThrowIfNull(collection);

		IReadOnlyCollection<T> source = CollectionHelpers.ReifyCollection(collection);
		int count = source.Count;
		if (count > 0)
		{
			mBuffer = new T[count];
			DoInsertRange(0, source);
		}
		else
		{
			mBuffer = new T[DefaultCapacity];
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Deque{T}"/> class with the default initial capacity.
	/// </summary>
	/// <remarks>
	/// The default capacity is <see cref="DefaultCapacity"/> (8 elements).
	/// </remarks>
	public Deque()
		: this(DefaultCapacity) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="Deque{T}"/> class with elements copied from the specified span.
	/// </summary>
	/// <param name="items">The span whose elements are copied to the new deque.</param>
	/// <remarks>
	///     <para>
	///     The capacity is set to exactly match the length of <paramref name="items"/>,
	///     or to <see cref="DefaultCapacity"/> if the span is empty.
	///     </para>
	///     <para>
	///     This constructor provides a high-performance, allocation-free way to initialize a deque
	///     from stack-allocated arrays or other span sources.
	///     </para>
	/// </remarks>
	public Deque(ReadOnlySpan<T> items)
	{
		if (items.Length > 0)
		{
			mBuffer = new T[items.Length];
			items.CopyTo(mBuffer);
			Count = items.Length;
		}
		else
		{
			mBuffer = new T[DefaultCapacity];
		}
	}

	/// <summary>
	/// Gets or sets the capacity for this deque.
	/// </summary>
	/// <remarks>
	/// This value must always be greater than zero and cannot be set to a value less than <see cref="Count"/>.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when attempting to set a value less than <see cref="Count"/>.
	/// </exception>
	public int Capacity
	{
		get => mBuffer.Length;

		set
		{
			if (value < Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(value),
					"Capacity cannot be set to a value less than Count");
			}
			if (value == mBuffer.Length) return;

			// Create the new internal buffer and copy our existing range.
			var newBuffer = new T[value];
			CopyToArray(newBuffer);

			// Set up to use the new InternalBuffer.
			mBuffer = newBuffer;
			mOffset = 0;
		}
	}

	/// <summary>
	/// Gets the number of elements contained in this deque.
	/// </summary>
	/// <value>The number of elements currently stored in the deque.</value>
	public int Count { get; private set; }

	/// <summary>
	/// Gets a value indicating whether this deque contains no elements.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if <see cref="Count"/> is 0; otherwise, <see langword="false"/>.
	/// </value>
	public bool IsEmpty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Count == 0;
	}

	/// <summary>
	/// Gets a value indicating whether the internal buffer has reached its current capacity.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if <see cref="Count"/> equals <see cref="Capacity"/>; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	///     <para>
	///     When <see langword="true"/>, the next insertion will trigger an automatic buffer resize (capacity doubling).
	///     </para>
	///     <para>
	///     This property is primarily informative. The deque automatically grows as needed,
	///     so checking this value before insertion is not required.
	///     </para>
	///     <para>
	///     Can be useful for performance monitoring or when managing memory usage with pre-allocated capacity.
	///     </para>
	/// </remarks>
	public bool IsFull
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Count == Capacity;
	}

	/// <summary>
	/// Gets or sets the item at the specified index.
	/// </summary>
	/// <param name="index">The index of the item to get or set.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="index"/> is not a valid index in this deque.
	/// </exception>
	public T this[int index]
	{
		get
		{
			CheckExistingIndexArgument(Count, index);
			return DoGetItem(index);
		}

		set
		{
			CheckExistingIndexArgument(Count, index);
			DoSetItem(index, value);
		}
	}

	/// <summary>
	/// Gets or sets the item at the specified index using <see cref="Index"/> syntax.
	/// </summary>
	/// <param name="index">The index of the item to get or set, supporting <c>^</c> (from-end) syntax.</param>
	/// <returns>The element at the specified index.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="index"/> resolves to an invalid position in this deque.
	/// </exception>
	/// <example>
	///     <code>
	/// var deque = new Deque&lt;int&gt;([1, 2, 3, 4, 5]);
	/// int last = deque[^1];      // Returns 5
	/// int secondLast = deque[^2]; // Returns 4
	/// </code>
	/// </example>
	public T this[Index index]
	{
		get => this[index.GetOffset(Count)];
		set => this[index.GetOffset(Count)] = value;
	}

	/// <summary>
	/// Gets a value indicating whether the logical element sequence wraps around the end of <see cref="mBuffer"/>.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the elements span from <see cref="mOffset"/> to the end of the buffer
	/// and continue from index 0; otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	///     <para>
	///     This condition is checked using <c>mOffset > Capacity - Count</c>, which is an overflow-safe
	///     equivalent of <c>(mOffset + Count) > Capacity</c>.
	///     </para>
	///     <para>
	///     When split, copy operations must handle two separate array regions.
	///     </para>
	/// </remarks>
	private bool IsSplit
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => mOffset > Capacity - Count;
	}

	/// <summary>
	/// Inserts a single element at the back of this deque.
	/// </summary>
	/// <param name="value">The element to insert.</param>
	/// <remarks>
	/// This operation has <b>O(1)</b> amortized time complexity. The buffer is doubled when
	/// <see cref="Capacity"/> is exceeded, which ensures efficient insertions over time.
	/// </remarks>
	public void AddToBack(T value)
	{
		EnsureCapacityForOneElement();
		DoAddToBack(value);
	}

	/// <summary>
	/// Inserts a single element at the front of this deque.
	/// </summary>
	/// <param name="value">The element to insert.</param>
	/// <remarks>
	/// This operation has <b>O(1)</b> amortized time complexity. The buffer is doubled when
	/// <see cref="Capacity"/> is exceeded, which ensures efficient insertions over time.
	/// </remarks>
	public void AddToFront(T value)
	{
		EnsureCapacityForOneElement();
		DoAddToFront(value);
	}

	/// <summary>
	/// Inserts a collection of elements into this deque at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index at which the new elements should be inserted.</param>
	/// <param name="collection">The collection of elements to insert. Cannot be <see langword="null"/>.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="collection"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="index"/> is less than 0 or greater than <see cref="Count"/>.
	/// </exception>
	public void InsertRange(int index, IEnumerable<T> collection)
	{
		// Validate arguments.
		ArgumentNullException.ThrowIfNull(collection);
		CheckNewIndexArgument(Count, index);

		// Reify the collection to determine its size.
		// If the source is this deque, create a copy to avoid issues during insertion.
		// This handles cases like deque.InsertRange(2, deque);
		IReadOnlyCollection<T> source = CollectionHelpers.ReifyCollection(collection);
		if (ReferenceEquals(source, this)) source = [.. source];
		int collectionCount = source.Count;

		// Overflow-safe check for "Count + collectionCount > Capacity"
		if (collectionCount > Capacity - Count)
		{
			Capacity = checked(Count + collectionCount);
		}

		// No-op if the collection is empty.
		if (collectionCount == 0)
			return;

		// Perform the insertion.
		DoInsertRange(index, source);
	}

	/// <summary>
	/// Inserts a span of elements into this deque at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index at which the new elements should be inserted.</param>
	/// <param name="items">The span of elements to insert.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="index"/> is less than 0 or greater than <see cref="Count"/>.
	/// </exception>
	/// <remarks>
	/// This overload provides a high-performance, allocation-free way to insert elements
	/// from stack-allocated arrays or other span sources.
	/// </remarks>
	public void InsertRange(int index, ReadOnlySpan<T> items)
	{
		// Validate arguments.
		CheckNewIndexArgument(Count, index);

		int itemCount = items.Length;

		// No-op if the span is empty.
		if (itemCount == 0)
			return;

		// Overflow-safe check for "Count + itemCount > Capacity"
		if (itemCount > Capacity - Count)
		{
			Capacity = checked(Count + itemCount);
		}

		// Perform the insertion.
		DoInsertRange(index, items);
	}

	/// <summary>
	/// Removes a range of elements from this deque.
	/// </summary>
	/// <param name="offset">The zero-based index at which the range begins.</param>
	/// <param name="count">The number of elements to remove.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="offset"/> or <paramref name="count"/> is less than 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when the range [<paramref name="offset"/>, <paramref name="offset"/> + <paramref name="count"/>)
	/// exceeds the bounds [0, <see cref="Count"/>).
	/// </exception>
	public void RemoveRange(int offset, int count)
	{
		// Validate arguments.
		CheckRangeArguments(Count, offset, count);

		// No-op if count is zero.
		if (count == 0) return;

		// Perform the removal.
		DoRemoveRange(offset, count);
	}

	/// <summary>
	/// Removes and returns the last element of this deque.
	/// </summary>
	/// <returns>The former last element.</returns>
	/// <exception cref="InvalidOperationException">The deque is empty.</exception>
	public T RemoveFromBack()
	{
		if (IsEmpty) throw new InvalidOperationException("The deque is empty.");
		return DoRemoveFromBack();
	}

	/// <summary>
	/// Removes and returns the first element of this deque.
	/// </summary>
	/// <returns>The former first element.</returns>
	/// <exception cref="InvalidOperationException">The deque is empty.</exception>
	public T RemoveFromFront()
	{
		if (IsEmpty) throw new InvalidOperationException("The deque is empty.");
		return DoRemoveFromFront();
	}

	/// <summary>
	/// Attempts to remove and return the first element of this deque.
	/// </summary>
	/// <param name="result">
	/// When this method returns <see langword="true"/>, contains the removed element;
	/// otherwise, the default value of <typeparamref name="T"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if an element was successfully removed; otherwise, <see langword="false"/>.
	/// </returns>
	public bool TryRemoveFromFront([MaybeNullWhen(false)] out T result)
	{
		if (IsEmpty)
		{
			result = default;
			return false;
		}

		result = DoRemoveFromFront();
		return true;
	}

	/// <summary>
	/// Attempts to remove and return the last element of this deque.
	/// </summary>
	/// <param name="result">
	/// When this method returns <see langword="true"/>, contains the removed element;
	/// otherwise, the default value of <typeparamref name="T"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if an element was successfully removed; otherwise, <see langword="false"/>.
	/// </returns>
	public bool TryRemoveFromBack([MaybeNullWhen(false)] out T result)
	{
		if (IsEmpty)
		{
			result = default;
			return false;
		}

		result = DoRemoveFromBack();
		return true;
	}

	/// <summary>
	/// Returns the first element of this deque without removing it.
	/// </summary>
	/// <returns>The first element.</returns>
	/// <exception cref="InvalidOperationException">The deque is empty.</exception>
	public T PeekFront()
	{
		if (IsEmpty) throw new InvalidOperationException("The deque is empty.");
		return DoGetItem(0);
	}

	/// <summary>
	/// Returns the last element of this deque without removing it.
	/// </summary>
	/// <returns>The last element.</returns>
	/// <exception cref="InvalidOperationException">The deque is empty.</exception>
	public T PeekBack()
	{
		if (IsEmpty) throw new InvalidOperationException("The deque is empty.");
		return DoGetItem(Count - 1);
	}

	/// <summary>
	/// Attempts to return the first element of this deque without removing it.
	/// </summary>
	/// <param name="result">
	/// When this method returns <see langword="true"/>, contains the first element;
	/// otherwise, the default value of <typeparamref name="T"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the deque is not empty; otherwise, <see langword="false"/>.
	/// </returns>
	public bool TryPeekFront([MaybeNullWhen(false)] out T result)
	{
		if (IsEmpty)
		{
			result = default;
			return false;
		}

		result = DoGetItem(0);
		return true;
	}

	/// <summary>
	/// Attempts to return the last element of this deque without removing it.
	/// </summary>
	/// <param name="result">
	/// When this method returns <see langword="true"/>, contains the last element;
	/// otherwise, the default value of <typeparamref name="T"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the deque is not empty; otherwise, <see langword="false"/>.
	/// </returns>
	public bool TryPeekBack([MaybeNullWhen(false)] out T result)
	{
		if (IsEmpty)
		{
			result = default;
			return false;
		}

		result = DoGetItem(Count - 1);
		return true;
	}

	/// <summary>
	/// Removes all items from this deque.
	/// </summary>
	/// <remarks>
	/// For reference types, this method clears the internal buffer to allow garbage collection
	/// of the removed elements.
	/// </remarks>
	public void Clear()
	{
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			ClearBuffer();
		}

		mOffset = 0;
		Count = 0;
	}

	/// <summary>
	/// Creates and returns a new array containing the elements in this deque.
	/// </summary>
	/// <returns>An array containing copies of the elements of this deque in the same order.</returns>
	public T[] ToArray()
	{
		var result = new T[Count];
		CopyTo(result);
		return result;
	}

	/// <summary>
	/// Copies the elements of this deque to a <see cref="Span{T}"/>.
	/// </summary>
	/// <param name="destination">The destination span. Must have a length of at least <see cref="Count"/>.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="destination"/> is too small to hold all elements.
	/// </exception>
	/// <remarks>
	/// This method provides a high-performance, allocation-free way to copy deque contents.
	/// </remarks>
	public void CopyTo(Span<T> destination)
	{
		if (destination.Length < Count)
		{
			throw new ArgumentException("Destination span is too small.", nameof(destination));
		}

		CopyToSpan(destination);
	}

	/// <summary>
	/// Attempts to copy the elements of this deque to a <see cref="Span{T}"/>.
	/// </summary>
	/// <param name="destination">The destination span.</param>
	/// <returns>
	/// <see langword="true"/> if the elements were successfully copied;
	/// <see langword="false"/> if <paramref name="destination"/> is too small to hold all elements.
	/// </returns>
	/// <remarks>
	/// This method provides a safe, allocation-free way to copy deque contents when the
	/// destination size is not guaranteed to be sufficient.
	/// </remarks>
	public bool TryCopyTo(Span<T> destination)
	{
		if (destination.Length < Count)
		{
			return false;
		}

		CopyToSpan(destination);
		return true;
	}

	/// <summary>
	/// Copies the elements of this deque to an array, starting at the specified array index.
	/// </summary>
	/// <param name="array">The destination array.</param>
	/// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="array"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="arrayIndex"/> is less than 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when the available space from <paramref name="arrayIndex"/> to the end of <paramref name="array"/> is
	/// insufficient.
	/// </exception>
	public void CopyTo(T[] array, int arrayIndex)
	{
		ArgumentNullException.ThrowIfNull(array);
		CheckRangeArguments(array.Length, arrayIndex, Count);
		CopyToArray(array, arrayIndex);
	}

	#region Implementation of IList<T>

	/// <summary>
	/// Gets a value indicating whether this list is read-only.
	/// This implementation always returns <see langword="false"/>.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if this list is read-only;
	/// otherwise <see langword="false"/>.
	/// </returns>
	bool ICollection<T>.IsReadOnly => false;

	/// <summary>
	/// Inserts an item to this list at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
	/// <param name="item">The object to insert into this list.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="index"/> is not a valid index in this list.
	/// </exception>
	public void Insert(int index, T item)
	{
		CheckNewIndexArgument(Count, index);
		DoInsert(index, item);
	}

	/// <summary>
	/// Removes the item at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index of the item to remove.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="index"/> is not a valid index in this list.
	/// </exception>
	public void RemoveAt(int index)
	{
		CheckExistingIndexArgument(Count, index);
		DoRemoveAt(index);
	}

	/// <summary>
	/// Determines the index of a specific item in this list.
	/// </summary>
	/// <param name="item">The object to locate in this list.</param>
	/// <returns>
	/// The index of <paramref name="item"/> if found in this list;
	/// otherwise -1.
	/// </returns>
	public int IndexOf(T item)
	{
		var comparer = EqualityComparer<T>.Default;

		int index = 0;
		foreach (T sourceItem in this)
		{
			if (comparer.Equals(item, sourceItem))
				return index;

			++index;
		}

		return -1;
	}

	/// <summary>
	/// Determines whether this deque contains a specific value.
	/// </summary>
	/// <param name="item">The object to locate in this deque.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="item"/> is found in this deque;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public bool Contains(T item) => IndexOf(item) >= 0;

	/// <summary>
	/// Adds an item to the end of this list.
	/// </summary>
	/// <param name="item">The object to add to this list.</param>
	void ICollection<T>.Add(T item)
	{
		DoInsert(Count, item);
	}

	/// <summary>
	/// Determines whether this list contains a specific value.
	/// </summary>
	/// <param name="item">The object to locate in this list.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="item"/> is found in this list;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	bool ICollection<T>.Contains(T item) => Contains(item);


	/// <summary>
	/// Removes the first occurrence of a specific object from this list.
	/// </summary>
	/// <param name="item">The object to remove from this list.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="item"/> was successfully removed from the list;
	/// <see langword="false"/> if the list does not contain <paramref name="item"/>.
	/// </returns>
	public bool Remove(T item)
	{
		int index = IndexOf(item);
		if (index == -1)
			return false;

		DoRemoveAt(index);
		return true;
	}

	/// <summary>
	/// Returns an enumerator that iterates through the collection.
	/// </summary>
	/// <returns>
	/// An <see cref="Enumerator"/> that can be used to iterate through the collection.
	/// </returns>
	/// <remarks>
	/// This method returns a struct enumerator to avoid heap allocations during enumeration.
	/// </remarks>
	public Enumerator GetEnumerator() => new(this);

	/// <summary>
	/// Returns an enumerator that iterates through the collection.
	/// </summary>
	/// <returns>
	/// A <see cref="IEnumerator{T}"/> that can be used to iterate through the collection.
	/// </returns>
	IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

	/// <summary>
	/// Returns an enumerator that iterates through a collection.
	/// </summary>
	/// <returns>
	/// An <see cref="IEnumerator"/> object that can be used to iterate through the collection.
	/// </returns>
	IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

	#endregion

	#region Implementation of IList

	/// <summary>
	/// Gets a value indicating whether access to this deque is synchronized (thread-safe).
	/// </summary>
	/// <value>
	/// Always <see langword="false"/>. This deque is not thread-safe.
	/// Use external synchronization for concurrent access.
	/// </value>
	bool ICollection.IsSynchronized => false;

	/// <summary>
	/// Gets an object that can be used to synchronize access to this deque.
	/// </summary>
	/// <value>
	/// The current instance. For proper thread-safety, lock on this object before accessing the deque.
	/// </value>
	/// <remarks>
	/// Consider using <see cref="System.Threading.Lock"/> or other synchronization primitives
	/// for thread-safe access patterns.
	/// </remarks>
	object ICollection.SyncRoot => this;

	/// <summary>
	/// Gets a value indicating whether this deque has a fixed size.
	/// </summary>
	/// <value>Always <see langword="false"/> because this deque can grow dynamically.</value>
	bool IList.IsFixedSize => false;

	/// <summary>
	/// Gets a value indicating whether this deque is read-only.
	/// </summary>
	/// <value>Always <see langword="false"/> because this deque supports modifications.</value>
	bool IList.IsReadOnly => false;

	/// <summary>
	/// Adds an item to the end of this deque.
	/// </summary>
	/// <param name="value">The object to add. Must be compatible with type <typeparamref name="T"/>.</param>
	/// <returns>The index at which the item was added (always <see cref="Count"/> - 1 after insertion).</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="value"/> is <see langword="null"/> and <typeparamref name="T"/> is a non-nullable value
	/// type.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="value"/> is not compatible with type <typeparamref name="T"/>.
	/// </exception>
	int IList.Add(object? value)
	{
		// ReSharper disable once CompareNonConstrainedGenericWithNull
		if (value == null && default(T) != null)
			throw new ArgumentNullException(nameof(value), "Value cannot be null.");

		if (!IsT(value))
			throw new ArgumentException("Value is of incorrect type.", nameof(value));

		AddToBack((T)value!);
		return Count - 1;
	}

	/// <summary>
	/// Determines whether this deque contains a specific value.
	/// </summary>
	/// <param name="value">The object to locate.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="value"/> is of type <typeparamref name="T"/> and is found in this deque;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	bool IList.Contains(object? value)
	{
		return IsT(value) && ((ICollection<T>)this).Contains((T)value!);
	}

	/// <summary>
	/// Determines the index of a specific item in this deque.
	/// </summary>
	/// <param name="value">The object to locate.</param>
	/// <returns>
	/// The index of <paramref name="value"/> if it is of type <typeparamref name="T"/> and is found in this deque;
	/// otherwise, -1.
	/// </returns>
	int IList.IndexOf(object? value)
	{
		return IsT(value) ? IndexOf((T)value!) : -1;
	}

	/// <summary>
	/// Inserts an item into this deque at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index at which <paramref name="value"/> should be inserted.</param>
	/// <param name="value">The object to insert. Must be compatible with type <typeparamref name="T"/>.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="value"/> is <see langword="null"/> and <typeparamref name="T"/> is a non-nullable value
	/// type.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="value"/> is not compatible with type <typeparamref name="T"/>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="index"/> is less than 0 or greater than <see cref="Count"/>.
	/// </exception>
	void IList.Insert(int index, object? value)
	{
		// ReSharper disable once CompareNonConstrainedGenericWithNull
		if (value == null && default(T) != null)
			throw new ArgumentNullException(nameof(value), "Value cannot be null.");

		if (!IsT(value))
			throw new ArgumentException("Value is of incorrect type.", nameof(value));

		Insert(index, (T)value!);
	}

	/// <summary>
	/// Removes the first occurrence of a specific object from this deque.
	/// </summary>
	/// <param name="value">The object to remove.</param>
	/// <remarks>
	/// If <paramref name="value"/> is not of type <typeparamref name="T"/>, no action is taken.
	/// </remarks>
	void IList.Remove(object? value)
	{
		if (IsT(value))
			Remove((T)value!);
	}

	/// <summary>
	/// Gets or sets the element at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index of the element to get or set.</param>
	/// <returns>The element at the specified index.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="index"/> is less than 0 or greater than or equal to <see cref="Count"/>.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// Thrown when setting and the value is <see langword="null"/> while <typeparamref name="T"/> is a non-nullable value
	/// type.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when setting and the value is not compatible with type <typeparamref name="T"/>.
	/// </exception>
	object? IList.this[int index]
	{
		get => this[index];

		set
		{
			// ReSharper disable once CompareNonConstrainedGenericWithNull
			if (value == null && default(T) != null)
				throw new ArgumentNullException(nameof(value), "Value cannot be null.");

			if (!IsT(value))
				throw new ArgumentException("Value is of incorrect type.", nameof(value));

			this[index] = (T)value!;
		}
	}

	/// <summary>
	/// Copies the elements of this deque to an <see cref="Array"/>, starting at the specified index.
	/// </summary>
	/// <param name="array">The one-dimensional destination array. Must have zero-based indexing.</param>
	/// <param name="index">The zero-based index in <paramref name="array"/> at which copying begins.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="array"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="index"/> is less than 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="array"/> is of an incompatible type, is multidimensional,
	/// or when the available space from <paramref name="index"/> to the end of <paramref name="array"/>
	/// is insufficient.
	/// </exception>
	void ICollection.CopyTo(Array array, int index)
	{
		ArgumentNullException.ThrowIfNull(array);

		CheckRangeArguments(array.Length, index, Count);

		try
		{
			CopyToArray(array, index);
		}
		catch (ArrayTypeMismatchException ex)
		{
			throw new ArgumentException("Destination array is of incorrect type.", nameof(array), ex);
		}
		catch (RankException ex)
		{
			throw new ArgumentException("Destination array must be single dimensional.", nameof(array), ex);
		}
	}

	/// <summary>
	/// Determines whether the specified value is compatible with type <typeparamref name="T"/>.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="value"/> is of type <typeparamref name="T"/>,
	/// or if <paramref name="value"/> is <see langword="null"/> and <typeparamref name="T"/> is a reference type
	/// or nullable value type; otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// Used by non-generic <see cref="IList"/> interface implementations to validate incoming values.
	/// </remarks>
	private static bool IsT(object? value)
	{
		if (value is T) return true;
		if (value != null) return false;
		// ReSharper disable once CompareNonConstrainedGenericWithNull
		return default(T) == null;
	}

	#endregion
}
