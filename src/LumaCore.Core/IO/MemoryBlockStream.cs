// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;
using System.Diagnostics;

namespace LumaCore.Core.IO;

/// <summary>
/// A <see cref="Stream"/> that uses a linked list of fixed-size memory blocks as its backing store.
/// </summary>
/// <remarks>
///     <para>
///     Compared to <see cref="MemoryStream"/>, which works on a single contiguous buffer that must
///     be reallocated and copied as the stream grows, <see cref="MemoryBlockStream"/> grows by
///     appending new blocks to a chain. This avoids large reallocations and Large Object Heap
///     pressure, especially when the final size is unknown (e.g. when buffering an upload).
///     </para>
///     <para>
///     Buffers can be allocated on the heap or rented from an <see cref="ArrayPool{T}"/> to reduce
///     garbage collection pressure further. Use <see cref="StreamBufferPool"/> to obtain instances
///     backed by a dedicated, size-controlled array pool.
///     </para>
///     <para>
///     <b>Thread safety:</b> Instances are not thread-safe. Use <see cref="SynchronizedMemoryBlockStream"/>
///     for shared concurrent access.
///     </para>
/// </remarks>
public sealed class MemoryBlockStream : Stream
{
	/// <summary>
	/// Default block size: 64 KB. A power of two, so array pools return buffers of exactly this size
	/// without rounding up, and below the Large Object Heap threshold (~85 000 bytes).
	/// </summary>
	internal const int DefaultBlockSize = 64 * 1024;

	private          long                  mPosition;
	private          long                  mLength;
	private readonly int                   mBlockSize;
	private          long                  mCurrentBlockStartIndex;
	private          long                  mFirstBlockOffset;
	private readonly ArrayPool<byte>?      mArrayPool;
	private          ChainableMemoryBlock? mFirstBlock;
	private          ChainableMemoryBlock? mCurrentBlock;
	private          ChainableMemoryBlock? mLastBlock;
	private          bool                  mDisposed;

	#region Construction and Disposal

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryBlockStream"/> class.
	/// Buffers are allocated on the heap. Block size defaults to 64 KB.
	/// The stream is seekable and grows as data is written.
	/// </summary>
	public MemoryBlockStream() : this(DefaultBlockSize, null, false) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryBlockStream"/> class.
	/// Buffers are rented from the specified array pool. Block size defaults to 64 KB.
	/// The stream is seekable and grows as data is written.
	/// </summary>
	/// <param name="pool">Array pool to rent buffers from.</param>
	/// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
	public MemoryBlockStream(ArrayPool<byte> pool) : this(DefaultBlockSize, pool, false)
	{
		ArgumentNullException.ThrowIfNull(pool);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryBlockStream"/> class with a specific block size.
	/// Buffers are allocated on the heap. The stream is seekable and grows as data is written.
	/// </summary>
	/// <param name="blockSize">Size of a block in the stream.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="blockSize"/> is less than or equal to 0.</exception>
	public MemoryBlockStream(int blockSize) : this(blockSize, null, false) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryBlockStream"/> class with a specific block size.
	/// Buffers can be allocated on the heap or rented from the specified array pool. The stream can be
	/// configured to release buffers as data is read (which makes the stream unseekable).
	/// </summary>
	/// <param name="blockSize">
	/// Size of a block in the stream. The actual block size may be greater than the specified size if the
	/// buffer is rented from an array pool.
	/// </param>
	/// <param name="pool">
	/// Array pool to rent buffers from (<see langword="null"/> to allocate buffers on the heap).
	/// </param>
	/// <param name="releasesReadBlocks">
	/// <see langword="true"/> to release memory blocks that have been read (makes the stream unseekable);<br/>
	/// <see langword="false"/> to keep written memory blocks, enabling seeking and length changes.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="blockSize"/> is less than or equal to 0.</exception>
	public MemoryBlockStream(
		int              blockSize          = DefaultBlockSize,
		ArrayPool<byte>? pool               = null,
		bool             releasesReadBlocks = false)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockSize);

		mArrayPool = pool;
		mBlockSize = blockSize;
		ReleasesReadBlocks = releasesReadBlocks;
		CanWrite = true;
		CanSeek = !releasesReadBlocks;
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			DisposeInternal();
		}

		base.Dispose(disposing);
	}

	/// <inheritdoc/>
	public override async ValueTask DisposeAsync()
	{
		DisposeInternal();
		await base.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Performs the actual disposal logic, releasing all memory blocks and returning rented buffers to the pool.
	/// </summary>
	private void DisposeInternal()
	{
		if (mFirstBlock != null)
		{
			mFirstBlock.ReleaseChain();
			mFirstBlock = null;
			mCurrentBlock = null;
			mLastBlock = null;
			mFirstBlockOffset = 0;
			mCurrentBlockStartIndex = 0;
			mLength = 0;
			mPosition = 0;
		}

		mDisposed = true;
	}

	#endregion

	#region Stream Capabilities

	/// <inheritdoc/>
	public override bool CanRead => true;

	/// <inheritdoc/>
	public override bool CanWrite { get; }

	/// <inheritdoc/>
	public override bool CanSeek { get; }

	/// <summary>
	/// Gets a value indicating whether the stream releases blocks after they have been read.
	/// When <see langword="true"/>, the stream is forward-only and not seekable.
	/// </summary>
	public bool ReleasesReadBlocks { get; }

	#endregion

	#region Position and Length

	/// <inheritdoc/>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="value"/> is negative — or — <paramref name="value"/> is greater than <see cref="Length"/>.
	/// </exception>
	public override long Position
	{
		get => mPosition;
		set => Seek(value, SeekOrigin.Begin);
	}

	/// <inheritdoc/>
	public override long Length => mLength;

	/// <inheritdoc/>
	/// <remarks>
	/// If the specified length is less than the current length, the stream is truncated.
	/// If the specified length is greater than the current length, the stream is extended and the
	/// new region is initialized with zeros. The stream position is not changed unless the truncation
	/// makes it exceed the new length, in which case it is moved to the new end of the stream.
	/// </remarks>
	public override void SetLength(long value)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(value);

		if (!CanSeek)
			throw new NotSupportedException("The stream does not support seeking.");

		ObjectDisposedException.ThrowIf(mDisposed, this);

		// Determine the capacity of the chain of memory blocks backing the stream.
		long capacity = mLastBlock != null
			                ? mLength + mLastBlock.Capacity - mLastBlock.Length
			                : 0;

		if (value > capacity)
		{
			// Requested size exceeds current capacity → grow by appending zero-initialized blocks.
			long additionallyNeededSpace = value - capacity;
			long lengthOfLastBlock = value - capacity;
			while (true)
			{
				var newBlock = new ChainableMemoryBlock(mBlockSize, mArrayPool, true);
				if (mFirstBlock == null)
				{
					mFirstBlock = newBlock;
					mCurrentBlock = newBlock;
					mLastBlock = newBlock;
				}
				else
				{
					Debug.Assert(mLastBlock != null);
					mLastBlock!.Length = mLastBlock.Capacity;
					mLastBlock.Next = newBlock;
					mLastBlock = newBlock;
				}

				additionallyNeededSpace -= newBlock.Capacity;
				if (additionallyNeededSpace <= 0) break;

				lengthOfLastBlock -= newBlock.Capacity;
			}

			Debug.Assert(lengthOfLastBlock <= int.MaxValue);
			Debug.Assert(mLastBlock != null);
			mLastBlock!.Length = (int)lengthOfLastBlock;
			mLength = value;
		}
		else
		{
			if (value == 0)
			{
				mFirstBlock?.ReleaseChain();
				mFirstBlock = null;
				mCurrentBlock = null;
				mLastBlock = null;
				mFirstBlockOffset = 0;
				mCurrentBlockStartIndex = 0;
				mLength = 0;
				mPosition = 0;
			}
			else
			{
				// Shrink → release blocks past the new length and zero the trailing slack.
				long remaining = value;
				long lastBlockStartIndex = 0;
				ChainableMemoryBlock? block = mFirstBlock;
				while (true)
				{
					Debug.Assert(block != null);
					remaining -= Math.Min(remaining, block.Next != null ? block.Length : block.Capacity);
					if (remaining == 0) break;
					lastBlockStartIndex += block.Length;
					block = block.Next;
				}

				block.Next?.ReleaseChain();
				block.Next = null;
				mLastBlock = block;
				mLength = value;

				int startIndex = Math.Min(mLastBlock.Length, (int)(value - lastBlockStartIndex));
				int bytesToClear = mLastBlock.Capacity - startIndex;
				Array.Clear(mLastBlock.Buffer, startIndex, bytesToClear);
				mLastBlock.Length = (int)(value - lastBlockStartIndex);

				if (mPosition < mLength) return;
				mPosition = mLength;
				mCurrentBlock = mLastBlock;
				mCurrentBlockStartIndex = lastBlockStartIndex;
				Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
			}
		}
	}

	#endregion

	#region Seek

	/// <inheritdoc/>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="offset"/> would move the position before the start of the stream or past its end.
	/// </exception>
	/// <exception cref="ArgumentException"><paramref name="origin"/> is not a valid <see cref="SeekOrigin"/> value.</exception>
	/// <exception cref="NotSupportedException">The stream does not support seeking.</exception>
	/// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
	public override long Seek(long offset, SeekOrigin origin)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		if (!CanSeek)
			throw new NotSupportedException("The stream does not support seeking.");

		switch (origin)
		{
			case SeekOrigin.Begin when offset < 0:
				throw new ArgumentOutOfRangeException(
					nameof(offset),
					"Position must be positive when seeking from the beginning of the stream.");

			case SeekOrigin.Begin when offset > mLength:
				throw new ArgumentOutOfRangeException(
					nameof(offset),
					"Position exceeds the length of the stream.");

			case SeekOrigin.Begin:
			{
				mPosition = offset;
				mCurrentBlockStartIndex = 0;
				long remaining = mPosition;
				mCurrentBlock = mFirstBlock;

				while (mCurrentBlock != null)
				{
					remaining -= Math.Min(remaining, mCurrentBlock.Length);
					if (remaining == 0) break;
					mCurrentBlockStartIndex += mCurrentBlock.Length;
					mCurrentBlock = mCurrentBlock.Next;
					Debug.Assert(
						mCurrentBlock == null ||
						mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
				}
				break;
			}

			case SeekOrigin.Current when offset < 0 && -offset > mPosition:
				throw new ArgumentOutOfRangeException(
					nameof(offset),
					"The target position is before the start of the stream.");

			case SeekOrigin.Current when offset > 0 && offset > mLength - mPosition:
				throw new ArgumentOutOfRangeException(
					nameof(offset),
					"The target position is after the end of the stream.");

			case SeekOrigin.Current:
			{
				mPosition += offset;
				mCurrentBlockStartIndex = 0;
				long remaining = mPosition;
				mCurrentBlock = mFirstBlock;

				while (mCurrentBlock != null)
				{
					remaining -= Math.Min(remaining, mCurrentBlock.Length);
					if (remaining == 0) break;
					mCurrentBlockStartIndex += mCurrentBlock.Length;
					mCurrentBlock = mCurrentBlock.Next;
					Debug.Assert(
						mCurrentBlock == null || mCurrentBlockStartIndex ==
						mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
				}
				break;
			}

			case SeekOrigin.End when offset > 0:
				throw new ArgumentOutOfRangeException(
					nameof(offset),
					"Position must be negative when seeking from the end of the stream.");

			case SeekOrigin.End when -offset > mLength:
				throw new ArgumentOutOfRangeException(
					nameof(offset),
					"Position exceeds the start of the stream.");

			case SeekOrigin.End:
			{
				if (mLength > 0)
				{
					long targetPosition = mLength + offset;
					mPosition = mLength + offset;
					mCurrentBlock = mLastBlock;
					Debug.Assert(mCurrentBlock != null);
					mCurrentBlockStartIndex = mLength - mCurrentBlock!.Length;
					Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
					while (mPosition != targetPosition)
					{
						if (targetPosition > mCurrentBlockStartIndex)
						{
							mPosition -= mCurrentBlock!.Length;
							mCurrentBlock = mCurrentBlock.Previous;
							Debug.Assert(mCurrentBlock != null);
							mCurrentBlockStartIndex = mPosition - mCurrentBlock!.Length;
							Debug.Assert(
								mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
						}
						else
						{
							mPosition = targetPosition;
						}
					}
				}
				break;
			}

			default:
				throw new ArgumentException("The specified seek origin is invalid.", nameof(origin));
		}

		return mPosition;
	}

	#endregion

	#region Read

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count)
	{
		ArgumentNullException.ThrowIfNull(buffer);
		ArgumentOutOfRangeException.ThrowIfNegative(offset);
		ArgumentOutOfRangeException.ThrowIfNegative(count);

		if (offset + count > buffer.Length)
			throw new ArgumentException("The buffer's length is less than offset + count.", nameof(count));

		return ReadInternal(buffer, offset, count);
	}

	/// <inheritdoc/>
	public override Task<int> ReadAsync(
		byte[]            buffer,
		int               offset,
		int               count,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(buffer);
		ArgumentOutOfRangeException.ThrowIfNegative(offset);
		ArgumentOutOfRangeException.ThrowIfNegative(count);

		if (offset + count > buffer.Length)
			throw new ArgumentException("The buffer's length is less than offset + count.", nameof(count));

		return cancellationToken.IsCancellationRequested
			       ? Task.FromException<int>(new OperationCanceledException(cancellationToken))
			       : Task.FromResult(ReadInternal(buffer, offset, count));
	}

	/// <inheritdoc/>
	public override int Read(Span<byte> buffer)
	{
		return ReadInternal(buffer);
	}

	/// <inheritdoc/>
	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		return cancellationToken.IsCancellationRequested
			       ? new ValueTask<int>(Task.FromException<int>(new OperationCanceledException(cancellationToken)))
			       : new ValueTask<int>(ReadInternal(buffer.Span));
	}

	/// <inheritdoc/>
	public override int ReadByte()
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		if (mPosition == mLength)
			return -1;

		int index = PrepareReadingBlock(1, out int _);
		mPosition++;
		Debug.Assert(mCurrentBlock != null);
		return mCurrentBlock!.Buffer[index];
	}

	/// <summary>
	/// Performs the actual reading logic, copying data from the chain of memory blocks into the provided buffer.
	/// </summary>
	/// <param name="buffer">The buffer to copy data into.</param>
	/// <param name="offset">The offset in the buffer at which to start writing.</param>
	/// <param name="count">The maximum number of bytes to read.</param>
	/// <returns>The number of bytes actually read.</returns>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	private int ReadInternal(byte[] buffer, int offset, int count)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		int bytesToRead = (int)Math.Min(mLength - mPosition, count);
		Debug.Assert(bytesToRead >= 0);

		if (bytesToRead == 0)
			return 0;

		int remaining = bytesToRead;
		while (remaining > 0)
		{
			int index = PrepareReadingBlock(remaining, out int bytesToCopy);
			Debug.Assert(mCurrentBlock != null);
			Array.Copy(mCurrentBlock!.Buffer, index, buffer, offset, bytesToCopy);
			offset += bytesToCopy;
			remaining -= bytesToCopy;
			mPosition += bytesToCopy;
		}

		return bytesToRead;
	}

	/// <summary>
	/// Performs the actual reading logic for the <see cref="Span{T}"/> overload,
	/// copying data from the chain of memory blocks into the provided buffer.
	/// </summary>
	/// <param name="buffer">The buffer to copy data into.</param>
	/// <returns>The number of bytes actually read.</returns>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	private int ReadInternal(Span<byte> buffer)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		int bytesToRead = (int)Math.Min(mLength - mPosition, buffer.Length);
		Debug.Assert(bytesToRead >= 0);

		if (bytesToRead == 0)
			return 0;

		int offset = 0;
		int remaining = bytesToRead;
		while (remaining > 0)
		{
			int index = PrepareReadingBlock(remaining, out int bytesToCopy);
			Debug.Assert(mCurrentBlock != null);
			var source = new Span<byte>(mCurrentBlock!.Buffer, index, bytesToCopy);
			Span<byte> destination = buffer.Slice(offset);
			source.CopyTo(destination);
			offset += bytesToCopy;
			remaining -= bytesToCopy;
			mPosition += bytesToCopy;
		}

		return bytesToRead;
	}

	/// <summary>
	/// Prepares reading from the chain of buffers by advancing to the next block if the current one is at its end.
	/// </summary>
	/// <param name="remaining">Number of bytes still to be read.</param>
	/// <param name="bytesToCopy">Receives the number of bytes that can be copied from the current block.</param>
	/// <returns>The byte index in the current block at which to start reading.</returns>
	private int PrepareReadingBlock(long remaining, out int bytesToCopy)
	{
		Debug.Assert(mCurrentBlock != null);
		int index = (int)(mPosition - mCurrentBlockStartIndex);
		int bytesToEnd = mCurrentBlock!.Length - index;

		if (bytesToEnd == 0)
		{
			if (ReleasesReadBlocks)
			{
				// Forward-only mode: drop the just-finished block to release its buffer.
				Debug.Assert(mFirstBlock == mCurrentBlock);
				ChainableMemoryBlock? nextBlock = mCurrentBlock.Next;
				mFirstBlockOffset += mCurrentBlock.Length;
				mCurrentBlock.Next = null;
				mCurrentBlock.Release();
				mCurrentBlock = nextBlock;
				mCurrentBlockStartIndex = mPosition;
				Debug.Assert(mCurrentBlock != null);
				Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock!.IndexOfFirstByteInBlock);
				mFirstBlock = mCurrentBlock;
			}
			else
			{
				mCurrentBlock = mCurrentBlock.Next;
				mCurrentBlockStartIndex = mPosition;
				Debug.Assert(mCurrentBlock != null);
				Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock!.IndexOfFirstByteInBlock);
			}

			Debug.Assert(mCurrentBlock != null);
			index = (int)(mPosition - mCurrentBlockStartIndex);
		}

		bytesToCopy = (int)Math.Min(mCurrentBlock!.Length - index, remaining);
		return index;
	}

	#endregion

	#region Write

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		ArgumentNullException.ThrowIfNull(buffer);
		ArgumentOutOfRangeException.ThrowIfNegative(offset);
		ArgumentOutOfRangeException.ThrowIfNegative(count);

		if (count > 0 && offset >= buffer.Length)
			throw new ArgumentException("The offset exceeds the end of the buffer.", nameof(offset));

		if (offset + count > buffer.Length)
		{
			throw new ArgumentException(
				"The sum of offset + count is greater than the buffer's length.",
				nameof(count));
		}

		WriteInternal(buffer, offset, count);
	}

	/// <inheritdoc/>
	public override Task WriteAsync(
		byte[]            buffer,
		int               offset,
		int               count,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(buffer);
		ArgumentOutOfRangeException.ThrowIfNegative(offset);
		ArgumentOutOfRangeException.ThrowIfNegative(count);

		if (count > 0 && offset >= buffer.Length)
			throw new ArgumentException("The offset exceeds the end of the buffer.", nameof(offset));

		if (offset + count > buffer.Length)
		{
			throw new ArgumentException(
				"The sum of offset + count is greater than the buffer's length.",
				nameof(count));
		}

		if (cancellationToken.IsCancellationRequested)
			return Task.FromException(new OperationCanceledException(cancellationToken));

		WriteInternal(buffer, offset, count);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		WriteInternal(buffer);
	}

	/// <inheritdoc/>
	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
			return new ValueTask(Task.FromException(new OperationCanceledException(cancellationToken)));

		WriteInternal(buffer.Span);
		return default;
	}

	/// <inheritdoc/>
	public override void WriteByte(byte value)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		int index = PrepareWritingBlock(out int _);
		Debug.Assert(mCurrentBlock != null);
		mCurrentBlock!.Buffer[index] = value;
		mCurrentBlock.Length = Math.Max(mCurrentBlock.Length, index + 1);
		mPosition++;
		mLength = Math.Max(mLength, mPosition);
	}

	/// <summary>
	/// Reads all available data from the source stream and appends it to this stream.
	/// Buffering happens in newly allocated blocks before being injected into the chain, to keep the
	/// current stream consistent if the read fails.
	/// </summary>
	/// <param name="stream">Stream to read data from.</param>
	/// <returns>Number of bytes written.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="NotSupportedException"><paramref name="stream"/> does not support reading.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	public long Write(Stream stream)
	{
		ArgumentNullException.ThrowIfNull(stream);

		if (!stream.CanRead)
			throw new NotSupportedException("The source stream does not support reading.");

		long bytesInSourceStream = stream.CanSeek ? stream.Length - stream.Position : -1;
		if (bytesInSourceStream == 0)
			return 0;

		ObjectDisposedException.ThrowIf(mDisposed, this);

		long count = 0;
		ChainableMemoryBlock? firstBlock = null;
		ChainableMemoryBlock? previousBlock = null;
		try
		{
			while (true)
			{
				var block = new ChainableMemoryBlock(mBlockSize, mArrayPool);
				firstBlock ??= block;
				previousBlock?.Next = block;

				int bytesRead = stream.Read(block.Buffer, 0, block.Capacity);
				if (bytesRead == 0)
				{
					if (firstBlock == block) firstBlock = null;
					previousBlock?.Next = null;
					block.Release();
					break;
				}

				count += bytesRead;
				block.Length = bytesRead;
				previousBlock = block;
			}

			if (firstBlock != null)
			{
				InjectBufferAtCurrentPosition(firstBlock, true, true);
			}
		}
		catch
		{
			firstBlock?.ReleaseChain();
			throw;
		}

		return count;
	}

	/// <summary>
	/// Asynchronously reads all available data from the source stream and appends it to this stream.
	/// </summary>
	/// <param name="stream">Stream to read data from.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>Number of bytes written.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="NotSupportedException"><paramref name="stream"/> does not support reading.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	public async ValueTask<long> WriteAsync(Stream stream, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(stream);

		if (!stream.CanRead)
			throw new NotSupportedException("The source stream does not support reading.");

		cancellationToken.ThrowIfCancellationRequested();

		long bytesInSourceStream = stream.CanSeek ? stream.Length - stream.Position : -1;
		if (bytesInSourceStream == 0)
			return 0;

		ObjectDisposedException.ThrowIf(mDisposed, this);

		long count = 0;
		ChainableMemoryBlock? firstBlock = null;
		ChainableMemoryBlock? previousBlock = null;
		try
		{
			while (true)
			{
				var block = new ChainableMemoryBlock(mBlockSize, mArrayPool);
				firstBlock ??= block;
				previousBlock?.Next = block;

				int bytesRead = await stream
					                .ReadAsync(block.Buffer.AsMemory(0, block.Capacity), cancellationToken)
					                .ConfigureAwait(false);

				if (bytesRead == 0)
				{
					if (firstBlock == block) firstBlock = null;
					previousBlock?.Next = null;
					block.Release();
					break;
				}

				count += bytesRead;
				block.Length = bytesRead;
				previousBlock = block;
			}

			if (firstBlock != null)
			{
				// ReSharper disable once MethodHasAsyncOverloadWithCancellation
				InjectBufferAtCurrentPosition(firstBlock, true, true);
			}
		}
		catch
		{
			firstBlock?.ReleaseChain();
			throw;
		}

		return count;
	}

	/// <summary>
	/// Performs the actual writing logic for the byte array overloads, copying data from the provided buffer
	/// into the chain of memory blocks.
	/// </summary>
	/// <param name="buffer">The buffer containing the data to write.</param>
	/// <param name="offset">
	/// The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes.
	/// </param>
	/// <param name="count">
	/// The number of bytes to write. This is the maximum number of bytes that will be copied from the buffer;
	/// the actual number of bytes written may be less if the end of the stream is reached.
	/// </param>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	private void WriteInternal(byte[] buffer, int offset, int count)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		int bytesRemaining = count;
		while (bytesRemaining > 0)
		{
			int index = PrepareWritingBlock(out int bytesToEndOfBlock);
			int bytesToCopy = Math.Min(bytesToEndOfBlock, bytesRemaining);
			Debug.Assert(mCurrentBlock != null);
			Array.Copy(buffer, offset, mCurrentBlock!.Buffer, index, bytesToCopy);
			offset += bytesToCopy;
			bytesRemaining -= bytesToCopy;
			mPosition += bytesToCopy;
			mCurrentBlock.Length = Math.Max(mCurrentBlock.Length, index + bytesToCopy);
		}

		mLength = Math.Max(mLength, mPosition);
	}

	/// <summary>
	/// Performs the actual writing logic for the span overloads, copying data from the provided buffer
	/// into the chain of memory blocks.
	/// </summary>
	/// <param name="buffer">The buffer containing the data to write.</param>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	private void WriteInternal(ReadOnlySpan<byte> buffer)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		int bytesRemaining = buffer.Length;
		int offset = 0;
		while (bytesRemaining > 0)
		{
			int index = PrepareWritingBlock(out int bytesToEndOfBlock);
			int bytesToCopy = Math.Min(bytesToEndOfBlock, bytesRemaining);
			ReadOnlySpan<byte> source = buffer.Slice(offset, bytesToCopy);
			Debug.Assert(mCurrentBlock != null);
			var destination = new Span<byte>(mCurrentBlock!.Buffer, index, bytesToCopy);
			source.CopyTo(destination);
			bytesRemaining -= bytesToCopy;
			offset += bytesToCopy;
			mPosition += bytesToCopy;
			mCurrentBlock.Length = Math.Max(mCurrentBlock.Length, index + bytesToCopy);
		}

		mLength = Math.Max(mLength, mPosition);
	}

	/// <summary>
	/// Prepares writing to the chain of buffers by advancing to (or appending) the next block if needed.
	/// </summary>
	/// <param name="bytesToEndOfBlock">
	/// Receives the number of bytes from the current position to the end of the writable region in the current block.
	/// </param>
	/// <returns>The byte index in the current block at which to start writing.</returns>
	private int PrepareWritingBlock(out int bytesToEndOfBlock)
	{
		int index = (int)(mPosition - (mCurrentBlock != null ? mCurrentBlockStartIndex : 0));

		bytesToEndOfBlock = 0;
		if (mCurrentBlock != null)
		{
			bytesToEndOfBlock = mCurrentBlock.Next != null
				                    ? mCurrentBlock.Length - index
				                    : mCurrentBlock.Capacity - index;
		}

		if (bytesToEndOfBlock != 0)
			return index;

		if (mCurrentBlock?.Next != null)
		{
			mCurrentBlock = mCurrentBlock.Next;
			bytesToEndOfBlock = mCurrentBlock.Length;
		}
		else
		{
			if (!AppendNewBuffer()) mCurrentBlock = mCurrentBlock!.Next;
			Debug.Assert(mCurrentBlock != null);
			bytesToEndOfBlock = mCurrentBlock!.Capacity;
		}

		mCurrentBlockStartIndex = mPosition;
		Debug.Assert(mCurrentBlock != null);
		Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock!.IndexOfFirstByteInBlock);
		return 0;
	}

	/// <summary>
	/// Appends a freshly allocated buffer to the end of the chain.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if the appended buffer is the first buffer in the chain;<br/>
	/// <see langword="false"/> otherwise.
	/// </returns>
	private bool AppendNewBuffer()
	{
		bool isFirstBuffer = mFirstBlock == null;
		var block = new ChainableMemoryBlock(mBlockSize, mArrayPool, false);

		if (mFirstBlock == null)
		{
			mCurrentBlock = block;
			mFirstBlock = block;
		}
		else
		{
			mLastBlock!.Next = block;
		}

		mLastBlock = block;
		return isFirstBuffer;
	}

	#endregion

	#region CopyTo / Flush

	/// <inheritdoc/>
	public override void CopyTo(Stream destination, int bufferSize)
	{
		ArgumentNullException.ThrowIfNull(destination);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

		if (!destination.CanWrite)
			throw new NotSupportedException("The destination stream does not support writing.");

		ObjectDisposedException.ThrowIf(mDisposed, this);

		long bytesToRead = mLength - mPosition;
		Debug.Assert(bytesToRead >= 0);
		long remaining = bytesToRead;
		while (remaining > 0)
		{
			int index = PrepareReadingBlock(remaining, out int bytesToCopy);
			Debug.Assert(mCurrentBlock != null);
			destination.Write(mCurrentBlock!.Buffer, index, bytesToCopy);
			remaining -= bytesToCopy;
			mPosition += bytesToCopy;
		}
	}

	/// <inheritdoc/>
	public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(destination);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

		if (!destination.CanWrite)
			throw new NotSupportedException("The destination stream does not support writing.");

		cancellationToken.ThrowIfCancellationRequested();

		ObjectDisposedException.ThrowIf(mDisposed, this);

		long bytesToRead = mLength - mPosition;
		Debug.Assert(bytesToRead >= 0);
		long remaining = bytesToRead;
		while (remaining > 0)
		{
			int index = PrepareReadingBlock(remaining, out int bytesToCopy);
			Debug.Assert(mCurrentBlock != null);
			await destination
				.WriteAsync(mCurrentBlock!.Buffer.AsMemory(index, bytesToCopy), cancellationToken)
				.ConfigureAwait(false);
			remaining -= bytesToCopy;
			mPosition += bytesToCopy;
		}
	}

	/// <inheritdoc/>
	public override void Flush() { }

	/// <inheritdoc/>
	public override Task FlushAsync(CancellationToken cancellationToken)
	{
		return cancellationToken.IsCancellationRequested
			       ? Task.FromException(new OperationCanceledException(cancellationToken))
			       : Task.CompletedTask;
	}

	#endregion

	#region Buffer Append/Attach/Detach

	/// <summary>
	/// Appends a memory block (or chain of blocks) to the end of the stream.
	/// </summary>
	/// <param name="buffer">Memory block to append.</param>
	/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	/// <remarks>
	/// The specified buffer must not be accessed by the caller after this operation. The stream takes
	/// ownership and will return any pool-rented buffers to their pool when disposed.
	/// </remarks>
	public void AppendBuffer(ChainableMemoryBlock buffer)
	{
		ArgumentNullException.ThrowIfNull(buffer);
		AppendBufferInternal(buffer);
	}

	/// <summary>
	/// Asynchronously appends a memory block (or chain of blocks) to the end of the stream.
	/// </summary>
	/// <param name="buffer">Memory block to append.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous append operation.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	public Task AppendBufferAsync(ChainableMemoryBlock buffer, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(buffer);

		if (cancellationToken.IsCancellationRequested)
			return Task.FromException(new OperationCanceledException(cancellationToken));

		AppendBufferInternal(buffer);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Performs the actual logic of appending a memory block (or chain of blocks) to the end of the stream.
	/// </summary>
	/// <param name="buffer">The memory block to append.</param>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	private void AppendBufferInternal(ChainableMemoryBlock buffer)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		if (mLastBlock != null)
		{
			mLastBlock.Next = buffer;

			ChainableMemoryBlock? block = buffer;
			while (block != null)
			{
				mLength += block.Length;
				mLastBlock = block;
				block = block.Next;
			}
		}
		else
		{
			mCurrentBlock = buffer;
			mFirstBlock = buffer;
			mCurrentBlockStartIndex = mPosition;
			Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);

			ChainableMemoryBlock? block = mFirstBlock;
			while (block != null)
			{
				mLength += block.Length;
				mLastBlock = block;
				block = block.Next;
			}
		}
	}

	/// <summary>
	/// Attaches a memory block (or chain of blocks) as the new backing buffer of the stream, replacing
	/// any existing data. The position is reset to <c>0</c>.
	/// </summary>
	/// <param name="buffer">Memory block to attach (<see langword="null"/> to clear the stream).</param>
	/// <exception cref="ArgumentException"><paramref name="buffer"/> has a predecessor.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	/// <remarks>
	/// The specified buffer must not be accessed by the caller after this operation. The stream takes
	/// ownership and will return any pool-rented buffers to their pool when disposed.
	/// </remarks>
	public void AttachBuffer(ChainableMemoryBlock? buffer)
	{
		if (buffer?.Previous != null)
			throw new ArgumentException("The specified block must not have a predecessor.", nameof(buffer));

		AttachBufferInternal(buffer);
	}

	/// <summary>
	/// Asynchronously attaches a memory block (or chain of blocks) as the new backing buffer of the stream.
	/// </summary>
	/// <param name="buffer">Memory block to attach (<see langword="null"/> to clear the stream).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous attach operation.</returns>
	/// <exception cref="ArgumentException"><paramref name="buffer"/> has a predecessor.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	public Task AttachBufferAsync(ChainableMemoryBlock? buffer, CancellationToken cancellationToken = default)
	{
		if (buffer?.Previous != null)
			throw new ArgumentException("The specified block must not have a predecessor.", nameof(buffer));

		if (cancellationToken.IsCancellationRequested)
			return Task.FromException(new OperationCanceledException(cancellationToken));

		AttachBufferInternal(buffer);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Performs the actual logic of attaching a memory block (or chain of blocks) as the new backing buffer of the stream,
	/// replacing any existing data. The position is reset to <c>0</c>.
	/// </summary>
	/// <param name="buffer">Memory block to attach (<see langword="null"/> to clear the stream).</param>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	private void AttachBufferInternal(ChainableMemoryBlock? buffer)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		mFirstBlock?.ReleaseChain();

		mFirstBlock = buffer;
		mFirstBlockOffset = 0;
		mCurrentBlock = mFirstBlock;
		mCurrentBlockStartIndex = 0;
		mPosition = 0;

		mLength = 0;
		mLastBlock = null;
		ChainableMemoryBlock? block = mFirstBlock;
		while (block != null)
		{
			mLength += block.Length;
			mLastBlock = block;
			block = block.Next;
		}
	}

	/// <summary>
	/// Detaches the underlying memory-block chain from the stream and returns it. The stream is empty afterward.
	/// </summary>
	/// <returns>The detached memory-block chain, or <see langword="null"/> if the stream was empty.</returns>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	/// <remarks>
	/// If any block contains a pool-rented buffer, the caller is responsible for disposing the chain
	/// to return those buffers.
	/// </remarks>
	public ChainableMemoryBlock? DetachBuffer()
	{
		return DetachBufferInternal();
	}

	/// <summary>
	/// Asynchronously detaches the underlying memory-block chain from the stream.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The detached memory-block chain, or <see langword="null"/> if the stream was empty.</returns>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	public Task<ChainableMemoryBlock?> DetachBufferAsync(CancellationToken cancellationToken = default)
	{
		return cancellationToken.IsCancellationRequested
			       ? Task.FromException<ChainableMemoryBlock?>(new OperationCanceledException(cancellationToken))
			       : Task.FromResult(DetachBufferInternal());
	}

	/// <summary>
	/// Performs the actual logic of detaching the underlying memory-block chain from the stream and returning it.
	/// </summary>
	/// <returns>
	/// The detached memory-block chain, or <see langword="null"/> if the stream was empty.
	/// </returns>
	private ChainableMemoryBlock? DetachBufferInternal()
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		ChainableMemoryBlock? buffer = mFirstBlock;
		mFirstBlock = null;
		mCurrentBlock = null;
		mLastBlock = null;
		mFirstBlockOffset = 0;
		mCurrentBlockStartIndex = 0;
		mPosition = 0;
		mLength = 0;
		return buffer;
	}

	#endregion

	#region InjectBufferAtCurrentPosition

	/// <summary>
	/// Injects a chain of memory blocks at the current position. Optionally overwrites existing data.
	/// </summary>
	/// <param name="buffer">Chain of blocks to inject (must not have a predecessor).</param>
	/// <param name="overwrite">
	/// <see langword="true"/> to overwrite existing data; <see langword="false"/> to insert.
	/// </param>
	/// <param name="advancePosition">
	/// <see langword="true"/> to advance the position to the end of the injected data; <see langword="false"/> to keep it.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="buffer"/> has a predecessor block.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	public void InjectBufferAtCurrentPosition(ChainableMemoryBlock buffer, bool overwrite, bool advancePosition)
	{
		ArgumentNullException.ThrowIfNull(buffer);

		if (buffer.Previous != null)
			throw new ArgumentException("The specified block must not have a predecessor.", nameof(buffer));

		InjectBufferAtCurrentPositionInternal(buffer, overwrite, advancePosition);
	}

	/// <summary>
	/// Asynchronously injects a chain of memory blocks at the current position. Optionally overwrites existing data.
	/// </summary>
	/// <param name="buffer">Chain of blocks to inject (must not have a predecessor).</param>
	/// <param name="overwrite">
	/// <see langword="true"/> to overwrite existing data; <see langword="false"/> to insert.
	/// </param>
	/// <param name="advancePosition">
	/// <see langword="true"/> to advance the position to the end of the injected data; <see langword="false"/> to keep it.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous inject operation.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="buffer"/> has a predecessor block.</exception>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	public Task InjectBufferAtCurrentPositionAsync(
		ChainableMemoryBlock buffer,
		bool                 overwrite,
		bool                 advancePosition,
		CancellationToken    cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
			return Task.FromCanceled(cancellationToken);

		try
		{
			InjectBufferAtCurrentPosition(buffer, overwrite, advancePosition);
			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			return Task.FromException(ex);
		}
	}

	/// <summary>
	/// Performs the actual logic of injecting a chain of memory blocks at the current position,
	/// optionally overwriting existing data.
	/// </summary>
	/// <param name="buffer">Chain of blocks to inject (must not have a predecessor).</param>
	/// <param name="overwrite">
	/// <see langword="true"/> to overwrite existing data;<br/>
	/// <see langword="false"/> to insert.
	/// </param>
	/// <param name="advancePosition">
	/// <see langword="true"/> to advance the position to the end of the injected data;<br/>
	/// <see langword="false"/> to keep it.
	/// </param>
	/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
	private void InjectBufferAtCurrentPositionInternal(
		ChainableMemoryBlock buffer,
		bool                 overwrite,
		bool                 advancePosition)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);

		if (mLastBlock != null)
		{
			if (mPosition == mLength)
			{
				// Position is at the end of the stream — append the chain.
				mLastBlock.Next = buffer;

				ChainableMemoryBlock? block = buffer;
				while (block != null)
				{
					mLength += block.Length;
					mLastBlock = block;
					block = block.Next;
				}

				if (!advancePosition) return;
				mPosition = mLength;
				mCurrentBlock = mLastBlock;
				mCurrentBlockStartIndex = mLength - mCurrentBlock!.Length;
				Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
			}
			else
			{
				Debug.Assert(mCurrentBlock != null);

				int indexOfPositionInCurrentBlock = (int)(mPosition - mCurrentBlockStartIndex);
				if (indexOfPositionInCurrentBlock == mCurrentBlock!.Length)
				{
					Debug.Assert(mCurrentBlock.Next != null);
					mCurrentBlockStartIndex += mCurrentBlock.Length;
					mCurrentBlock = mCurrentBlock.Next;
					indexOfPositionInCurrentBlock = (int)(mPosition - mCurrentBlockStartIndex);
					Debug.Assert(mCurrentBlock != null);
					Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock!.IndexOfFirstByteInBlock);
				}

				ChainableMemoryBlock endOfChainToInsert = buffer.GetEndOfChain(out long chainToInsertLength);
				if (indexOfPositionInCurrentBlock == 0)
				{
					ChainableMemoryBlock block = mCurrentBlock!;
					if (mCurrentBlock!.Previous != null)
						mCurrentBlock.Previous.Next = buffer;
					else
						mFirstBlock = buffer;

					endOfChainToInsert.Next = block;

					if (overwrite)
					{
						RemoveDataFromChain(block, chainToInsertLength);
						mLength = Math.Max(mLength, mPosition + chainToInsertLength);
						if (endOfChainToInsert.Next == null)
							mLastBlock = endOfChainToInsert;
					}
					else
					{
						mLength += chainToInsertLength;
					}

					if (advancePosition)
					{
						mPosition += chainToInsertLength;
						mCurrentBlock = endOfChainToInsert;
						mCurrentBlockStartIndex += chainToInsertLength - endOfChainToInsert.Length;
						Debug.Assert(
							mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
					}
					else
					{
						mCurrentBlock = buffer;
						Debug.Assert(
							mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
					}
				}
				else
				{
					int bytesToEndOfCurrentBlock = mCurrentBlock!.Length - indexOfPositionInCurrentBlock;
					if (overwrite)
					{
						if (chainToInsertLength >= bytesToEndOfCurrentBlock)
						{
							mCurrentBlock.Length -= bytesToEndOfCurrentBlock;
							ChainableMemoryBlock? block = mCurrentBlock.Next;
							mCurrentBlock.Next = buffer;
							endOfChainToInsert.Next = block;

							if (block != null)
								RemoveDataFromChain(block, chainToInsertLength - bytesToEndOfCurrentBlock);

							if (endOfChainToInsert.Next == null)
								mLastBlock = endOfChainToInsert;

							mLength = Math.Max(mLength, mPosition + chainToInsertLength);

							if (!advancePosition) return;
							mPosition += chainToInsertLength;
							mCurrentBlock = endOfChainToInsert;
							mCurrentBlockStartIndex += indexOfPositionInCurrentBlock + chainToInsertLength -
							                           endOfChainToInsert.Length;
							Debug.Assert(
								mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
						}
						else
						{
							ChainableMemoryBlock? block = buffer;
							int offset = indexOfPositionInCurrentBlock;
							while (block != null)
							{
								Array.Copy(
									block.Buffer,
									0,
									mCurrentBlock.Buffer,
									offset,
									block.Length);

								offset += block.Length;
								ChainableMemoryBlock? next = block.Next;
								block.Next = null;
								Debug.Assert(block.Previous == null);
								Debug.Assert(block.Next == null);
								block.Release();
								block = next;
							}

							if (advancePosition)
								mPosition += chainToInsertLength;
						}
					}
					else
					{
						ChainableMemoryBlock adjustedEndOfChainToInsert = endOfChainToInsert;
						int lengthOfEndOfChainToInsert = endOfChainToInsert.Length;
						if (bytesToEndOfCurrentBlock > 0)
						{
							int unusedSpace = adjustedEndOfChainToInsert.Capacity - adjustedEndOfChainToInsert.Length;
							if (bytesToEndOfCurrentBlock > unusedSpace)
							{
								int remainingBytesToAllocate = bytesToEndOfCurrentBlock - unusedSpace;
								while (remainingBytesToAllocate > 0)
								{
									var block = new ChainableMemoryBlock(mBlockSize, mArrayPool);
									adjustedEndOfChainToInsert.Next = block;
									adjustedEndOfChainToInsert = block;
									remainingBytesToAllocate -= block.Capacity;
								}
							}

							ChainableMemoryBlock? blockReceivingMovedData = endOfChainToInsert;
							int remainingBytesToCopy = bytesToEndOfCurrentBlock;
							int offset = indexOfPositionInCurrentBlock;
							while (blockReceivingMovedData != null)
							{
								int bytesToCopy = Math.Min(
									remainingBytesToCopy,
									blockReceivingMovedData.Capacity - blockReceivingMovedData.Length);
								if (bytesToCopy > 0)
								{
									Array.Copy(
										mCurrentBlock.Buffer,
										offset,
										blockReceivingMovedData.Buffer,
										blockReceivingMovedData.Length,
										bytesToCopy);

									blockReceivingMovedData.Length += bytesToCopy;
									remainingBytesToCopy -= bytesToCopy;
									offset += bytesToCopy;
								}

								blockReceivingMovedData = blockReceivingMovedData.Next;
							}

							Debug.Assert(remainingBytesToCopy == 0);
						}

						if (mCurrentBlock.Next == null)
							mLastBlock = adjustedEndOfChainToInsert;

						mCurrentBlock.Length = indexOfPositionInCurrentBlock;
						adjustedEndOfChainToInsert.Next = mCurrentBlock.Next;
						mCurrentBlock.Next = buffer;

						mLength += chainToInsertLength;

						if (!advancePosition) return;
						mPosition += chainToInsertLength;
						mCurrentBlock = endOfChainToInsert;
						mCurrentBlockStartIndex += indexOfPositionInCurrentBlock + chainToInsertLength -
						                           lengthOfEndOfChainToInsert;
						Debug.Assert(
							mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
					}
				}
			}
		}
		else
		{
			mCurrentBlock = buffer;
			mFirstBlock = buffer;
			mCurrentBlockStartIndex = mPosition;
			Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);

			ChainableMemoryBlock? block = mFirstBlock;
			while (block != null)
			{
				mLength += block.Length;
				mLastBlock = block;
				block = block.Next;
			}

			if (!advancePosition) return;
			Debug.Assert(mLastBlock != null);
			mCurrentBlock = mLastBlock;
			mPosition = mLength;
			mCurrentBlockStartIndex = mLength - mCurrentBlock!.Length;
			Debug.Assert(mCurrentBlockStartIndex == mFirstBlockOffset + mCurrentBlock.IndexOfFirstByteInBlock);
		}
	}

	/// <summary>
	/// Removes the specified number of bytes starting at byte 0 of the specified block, removing entire
	/// blocks from the chain as needed.
	/// </summary>
	/// <param name="block">First block of the chain to remove data from.</param>
	/// <param name="length">Number of bytes to remove.</param>
	private static void RemoveDataFromChain(ChainableMemoryBlock block, long length)
	{
		long remainingBytesToRemove = length;
		ChainableMemoryBlock? current = block;
		while (current != null && remainingBytesToRemove > 0)
		{
			int bytesToRemove = (int)Math.Min(current.Length, remainingBytesToRemove);
			ChainableMemoryBlock? next = current.Next;
			ChainableMemoryBlock? previous = current.Previous;
			if (bytesToRemove == current.Length)
			{
				if (previous != null)
				{
					previous.Next = next;
					current.Next = null;
				}
				else
				{
					next?.Previous = null;
				}

				Debug.Assert(current.Previous == null);
				Debug.Assert(current.Next == null);
				current.Release();
			}
			else
			{
				Debug.Assert(current.Length > 0);
				Debug.Assert(bytesToRemove > 0);
				Array.Copy(
					current.Buffer,
					bytesToRemove,
					current.Buffer,
					0,
					current.Length - bytesToRemove);

				current.Length -= bytesToRemove;
			}

			current = next;
			remainingBytesToRemove -= bytesToRemove;
		}
	}

	#endregion
}
