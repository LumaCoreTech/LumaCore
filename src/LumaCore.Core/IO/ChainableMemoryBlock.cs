// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;

namespace LumaCore.Core.IO;

/// <summary>
/// A block of memory that can be chained with other blocks to form a linked list of buffers.
/// Used as the storage backing for <see cref="MemoryBlockStream"/>.
/// </summary>
/// <remarks>
///     <para>
///     The buffer can be allocated on the heap or rented from an <see cref="ArrayPool{T}"/>.
///     If a pool is supplied, calling <see cref="Release"/> or <see cref="ReleaseChain"/> returns
///     the buffer to that pool.
///     </para>
///     <para>
///     Instances are not thread-safe. The owning stream is responsible for synchronizing access.
///     </para>
/// </remarks>
public sealed class ChainableMemoryBlock : IDisposable
{
	private int                   mLength;
	private ChainableMemoryBlock? mPreviousBlock;
	private ChainableMemoryBlock? mNextBlock;
	private byte[]?               mBuffer;
	private ArrayPool<byte>?      mBufferPool;

	/// <summary>
	/// Initializes a new instance of the <see cref="ChainableMemoryBlock"/> class with the specified capacity.
	/// The buffer is allocated on the heap.
	/// </summary>
	/// <param name="capacity">Capacity of the memory block to create.</param>
	public ChainableMemoryBlock(int capacity) : this(capacity, null, false) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="ChainableMemoryBlock"/> class with the specified capacity.
	/// The buffer can be rented from the specified array pool or allocated on the heap.
	/// </summary>
	/// <param name="capacity">Capacity of the memory block to create.</param>
	/// <param name="pool">Array pool to rent the buffer from (<see langword="null"/> to use the regular heap).</param>
	/// <param name="clear"><see langword="true"/> to initialize the buffer with zeros; otherwise <see langword="false"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
	public ChainableMemoryBlock(int capacity, ArrayPool<byte>? pool, bool clear = false)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(capacity);

		mBufferPool = pool;
		mBuffer = pool != null ? pool.Rent(capacity) : new byte[capacity];
		mLength = 0;

		// Pool buffers may contain stale data; heap buffers are zero-initialized by the runtime.
		if (pool != null && clear)
			Array.Clear(mBuffer, 0, mBuffer.Length);
	}

	/// <summary>
	/// Releases the current block and all chained blocks, returning rented buffers to the appropriate array pools
	/// (same as <see cref="ReleaseChain"/>).
	/// </summary>
	public void Dispose()
	{
		ReleaseChain();
	}

	/// <summary>
	/// Releases the current block, returning the rented buffer to the array pool, if any.
	/// The block is unlinked from any neighbours before the buffer is returned.
	/// </summary>
	public void Release()
	{
		if (mBuffer == null)
			return;

		Previous = null;
		Next = null;
		mBufferPool?.Return(mBuffer);
		mBufferPool = null;
		mBuffer = null;
		mLength = -1;
	}

	/// <summary>
	/// Releases the current block and all blocks following it in the chain, returning rented buffers to their
	/// array pools, if any.
	/// </summary>
	public void ReleaseChain()
	{
		ChainableMemoryBlock? current = this;
		while (current != null)
		{
			ChainableMemoryBlock? next = current.mNextBlock;
			current.Release();
			current = next;
		}
	}

	/// <summary>
	/// Gets the array pool the buffer was rented from, or <see langword="null"/> if the buffer was allocated on the heap.
	/// </summary>
	public ArrayPool<byte>? BufferPool => mBufferPool;

	/// <summary>
	/// Gets the underlying buffer.
	/// </summary>
	/// <remarks>
	/// The buffer is set to <see langword="null"/> after <see cref="Release"/> has been called.
	/// </remarks>
	public byte[] Buffer => mBuffer ?? throw new ObjectDisposedException(nameof(ChainableMemoryBlock));

	/// <summary>
	/// Gets the capacity of the memory block (i.e. the size of the underlying buffer).
	/// </summary>
	public int Capacity => Buffer.Length;

	/// <summary>
	/// Gets or sets the length of the memory block. Must not exceed the capacity.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The value is negative or exceeds the capacity of the memory block.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The block has been released.</exception>
	public int Length
	{
		get
		{
			ObjectDisposedException.ThrowIf(mBuffer == null, this);
			return mLength;
		}
		set
		{
			ArgumentOutOfRangeException.ThrowIfNegative(value);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, Buffer.Length);
			mLength = value;
		}
	}

	/// <summary>
	/// Gets the accumulated length of the current block and all following blocks in the chain.
	/// </summary>
	/// <exception cref="ObjectDisposedException">One or more blocks in the chain have been released.</exception>
	public long ChainLength
	{
		get
		{
			long length = 0;
			ChainableMemoryBlock? current = this;
			while (current != null)
			{
				ObjectDisposedException.ThrowIf(current.mBuffer == null, this);
				length += current.mLength;
				current = current.mNextBlock;
			}

			return length;
		}
	}

	/// <summary>
	/// Gets or sets the previous memory block in the chain (<see langword="null"/> if this is the first block).
	/// </summary>
	/// <remarks>
	///     <para>
	///     Setting this property has side effects on the old and new neighbours to maintain the
	///     doubly-linked list invariant:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             The old previous block (if any) has its <see cref="Next"/> reference cleared.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             The new previous block (if any) is unlinked from its current <see cref="Next"/>
	///             neighbour, then linked to this block via its <see cref="Next"/> property.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     Callers must ensure that setting <see cref="Previous"/> and <see cref="Next"/> in the
	///     correct order does not leave the chain in an inconsistent state. Typically, link a new
	///     block by setting its <see cref="Next"/> first, then the neighbour's <see cref="Previous"/>.
	///     </para>
	/// </remarks>
	public ChainableMemoryBlock? Previous
	{
		get => mPreviousBlock;
		set
		{
			mPreviousBlock?.mNextBlock = null;

			if (value != null)
			{
				value.mNextBlock?.mPreviousBlock = null;

				mPreviousBlock = value;
				mPreviousBlock.mNextBlock = this;
			}
			else
			{
				mPreviousBlock = null;
			}
		}
	}

	/// <summary>
	/// Gets or sets the next memory block in the chain (<see langword="null"/> if this is the last block).
	/// </summary>
	/// <remarks>
	///     <para>
	///     Setting this property has side effects on the old and new neighbours to maintain the
	///     doubly-linked list invariant:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             The old next block (if any) has its <see cref="Previous"/> reference cleared.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             The new next block (if any) is unlinked from its current <see cref="Previous"/>
	///             neighbour, then linked to this block via its <see cref="Previous"/> property.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     Callers must ensure that setting <see cref="Previous"/> and <see cref="Next"/> in the
	///     correct order does not leave the chain in an inconsistent state. Typically, link a new
	///     block by setting its <see cref="Next"/> first, then the neighbour's <see cref="Previous"/>.
	///     </para>
	/// </remarks>
	public ChainableMemoryBlock? Next
	{
		get => mNextBlock;
		set
		{
			mNextBlock?.mPreviousBlock = null;

			if (value != null)
			{
				value.mPreviousBlock?.mNextBlock = null;

				mNextBlock = value;
				mNextBlock.mPreviousBlock = this;
			}
			else
			{
				mNextBlock = null;
			}
		}
	}

	/// <summary>
	/// Gets the block at the start of the chain.
	/// </summary>
	/// <returns>The first block in the chain.</returns>
	public ChainableMemoryBlock GetStartOfChain()
	{
		ChainableMemoryBlock current = this;
		while (current.mPreviousBlock != null)
		{
			current = current.mPreviousBlock;
		}

		return current;
	}

	/// <summary>
	/// Gets the block at the start of the chain together with the accumulated length of all blocks from the
	/// head of the chain up to and including this block.
	/// </summary>
	/// <param name="length">On return, contains the sum of lengths from the first block through this block.</param>
	/// <returns>The first block in the chain.</returns>
	public ChainableMemoryBlock GetStartOfChain(out long length)
	{
		length = 0;
		ChainableMemoryBlock? current = this;
		ChainableMemoryBlock first = this;
		while (current != null)
		{
			first = current;
			length += current.mLength;
			if (current.mPreviousBlock == null) break;
			current = current.mPreviousBlock;
		}

		return first;
	}

	/// <summary>
	/// Gets the block at the end of the chain.
	/// </summary>
	/// <returns>The last block in the chain.</returns>
	public ChainableMemoryBlock GetEndOfChain()
	{
		ChainableMemoryBlock current = this;
		while (current.mNextBlock != null)
		{
			current = current.mNextBlock;
		}

		return current;
	}

	/// <summary>
	/// Gets the block at the end of the chain together with the accumulated length of the current block
	/// and all following blocks.
	/// </summary>
	/// <param name="length">Receives the accumulated length of the current block and all following blocks.</param>
	/// <returns>The last block in the chain.</returns>
	public ChainableMemoryBlock GetEndOfChain(out long length)
	{
		length = 0;
		ChainableMemoryBlock? current = this;
		ChainableMemoryBlock last = this;
		while (current != null)
		{
			last = current;
			length += current.mLength;
			if (current.mNextBlock == null) break;
			current = current.mNextBlock;
		}

		return last;
	}

	/// <summary>
	/// Gets all data stored in the current block and all following blocks as a single contiguous byte array.
	/// </summary>
	/// <returns>The concatenated data of the chain.</returns>
	/// <remarks>
	/// Limited to chains with a total length of at most <see cref="int.MaxValue"/>.
	/// </remarks>
	public byte[] GetChainData()
	{
		byte[] buffer = new byte[ChainLength];
		GetChainData(buffer, 0);
		return buffer;
	}

	/// <summary>
	/// Copies the data of the current block and all following blocks into the specified buffer.
	/// Iterative to avoid stack overflow on long chains.
	/// </summary>
	/// <param name="buffer">Buffer to copy data into.</param>
	/// <param name="offset">Offset in <paramref name="buffer"/> at which to start copying.</param>
	private void GetChainData(byte[] buffer, int offset)
	{
		ChainableMemoryBlock? current = this;
		while (current != null)
		{
			Array.Copy(current.Buffer, 0, buffer, offset, current.mLength);
			offset += current.mLength;
			current = current.mNextBlock;
		}
	}

	/// <summary>
	/// Gets the index of the first byte of this block within the entire chain (for internal use).
	/// </summary>
	internal long IndexOfFirstByteInBlock
	{
		get
		{
			GetStartOfChain(out long length);
			length -= mLength;
			return length;
		}
	}
}
