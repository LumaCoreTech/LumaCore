// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;

namespace LumaCore.Core.IO;

/// <summary>
/// A thread-safe wrapper around <see cref="MemoryBlockStream"/>.
/// All operations acquire an internal <see cref="SemaphoreSlim"/> before delegating to the inner stream.
/// </summary>
public sealed class SynchronizedMemoryBlockStream : Stream
{
	private readonly MemoryBlockStream mStream;
	private readonly SemaphoreSlim     mLock;

	// Cached immutable properties — no locking needed after construction.
	private readonly bool mCanWrite;
	private readonly bool mCanSeek;
	private readonly bool mReleasesReadBlocks;

	// Tracks whether Dispose/DisposeAsync has already run, so subsequent calls become safe no-ops
	// (the Stream contract allows repeated disposal).
	private bool mDisposed;

	#region Construction and Disposal

	/// <summary>
	/// Initializes a new instance of the <see cref="SynchronizedMemoryBlockStream"/> class.
	/// Buffers are allocated on the heap. Block size defaults to 64 KB.
	/// </summary>
	public SynchronizedMemoryBlockStream() : this(MemoryBlockStream.DefaultBlockSize, null, false) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="SynchronizedMemoryBlockStream"/> class with buffers
	/// rented from the specified array pool.
	/// </summary>
	/// <param name="pool">Array pool to use for allocating buffers.</param>
	/// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
	public SynchronizedMemoryBlockStream(ArrayPool<byte> pool) : this(MemoryBlockStream.DefaultBlockSize, pool, false)
	{
		ArgumentNullException.ThrowIfNull(pool);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SynchronizedMemoryBlockStream"/> class with a specific block size.
	/// </summary>
	/// <param name="blockSize">Size of a block in the stream.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="blockSize"/> is less than or equal to 0.</exception>
	public SynchronizedMemoryBlockStream(int blockSize) : this(blockSize, null, false) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="SynchronizedMemoryBlockStream"/> class with a specific block size.
	/// Buffers can be allocated on the heap or rented from the specified array pool.
	/// </summary>
	/// <param name="blockSize">Size of a block in the stream.</param>
	/// <param name="pool">Array pool to rent buffers from (<see langword="null"/> to use the heap).</param>
	/// <param name="releasesReadBlocks">
	/// <see langword="true"/> to release blocks after they have been read (makes the stream unseekable).
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="blockSize"/> is less than or equal to 0.</exception>
	public SynchronizedMemoryBlockStream(
		int              blockSize          = MemoryBlockStream.DefaultBlockSize,
		ArrayPool<byte>? pool               = null,
		bool             releasesReadBlocks = false)
	{
		mStream = new MemoryBlockStream(blockSize, pool, releasesReadBlocks);
		mLock = new SemaphoreSlim(1);
		mCanWrite = mStream.CanWrite;
		mCanSeek = mStream.CanSeek;
		mReleasesReadBlocks = mStream.ReleasesReadBlocks;
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing && !mDisposed)
		{
			// Set the flag before acquiring the lock so a racing second Dispose call short-circuits
			// instead of waiting on a semaphore that is about to be disposed.
			mDisposed = true;

			try
			{
				mLock.Wait();
				mStream.Dispose();
			}
			finally
			{
				mLock.Release();
			}

			mLock.Dispose();
		}

		base.Dispose(disposing);
	}

	/// <inheritdoc/>
	public override async ValueTask DisposeAsync()
	{
		if (!mDisposed)
		{
			// See Dispose(bool) for the ordering rationale.
			mDisposed = true;

			try
			{
				await mLock.WaitAsync().ConfigureAwait(false);
				await mStream.DisposeAsync().ConfigureAwait(false);
			}
			finally
			{
				mLock.Release();
			}

			mLock.Dispose();
		}

		await base.DisposeAsync().ConfigureAwait(false);
	}

	#endregion

	#region Stream Capabilities

	/// <inheritdoc/>
	public override bool CanRead => !mDisposed;

	/// <inheritdoc/>
	public override bool CanWrite => !mDisposed && mCanWrite;

	/// <inheritdoc/>
	public override bool CanSeek => !mDisposed && mCanSeek;

	/// <summary>
	/// Gets a value indicating whether the underlying stream releases blocks after they have been read.
	/// </summary>
	public bool ReleasesReadBlocks => mReleasesReadBlocks;

	#endregion

	#region Position and Length

	/// <inheritdoc/>
	public override long Position
	{
		get
		{
			try
			{
				mLock.Wait();
				return mStream.Position;
			}
			finally
			{
				mLock.Release();
			}
		}
		set
		{
			try
			{
				mLock.Wait();
				mStream.Position = value;
			}
			finally
			{
				mLock.Release();
			}
		}
	}

	/// <inheritdoc/>
	public override long Length
	{
		get
		{
			try
			{
				mLock.Wait();
				return mStream.Length;
			}
			finally
			{
				mLock.Release();
			}
		}
	}

	/// <inheritdoc/>
	public override void SetLength(long value)
	{
		try
		{
			mLock.Wait();
			mStream.SetLength(value);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin)
	{
		try
		{
			mLock.Wait();
			return mStream.Seek(offset, origin);
		}
		finally
		{
			mLock.Release();
		}
	}

	#endregion

	#region Read

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count)
	{
		try
		{
			mLock.Wait();
			return mStream.Read(buffer, offset, count);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override async Task<int> ReadAsync(
		byte[]            buffer,
		int               offset,
		int               count,
		CancellationToken cancellationToken)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CA1835 // Use Memory overloads for Stream.ReadAsync
			return await mStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1835
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override int Read(Span<byte> buffer)
	{
		try
		{
			mLock.Wait();
			return mStream.Read(buffer);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			return await mStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override int ReadByte()
	{
		try
		{
			mLock.Wait();
			return mStream.ReadByte();
		}
		finally
		{
			mLock.Release();
		}
	}

	#endregion

	#region Write

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		try
		{
			mLock.Wait();
			mStream.Write(buffer, offset, count);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override async Task WriteAsync(
		byte[]            buffer,
		int               offset,
		int               count,
		CancellationToken cancellationToken)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CA1835
			await mStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1835
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		try
		{
			mLock.Wait();
			mStream.Write(buffer);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override async ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken    cancellationToken = default)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			await mStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override void WriteByte(byte value)
	{
		try
		{
			mLock.Wait();
			mStream.WriteByte(value);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc cref="MemoryBlockStream.Write(Stream)"/>
	public long Write(Stream stream)
	{
		try
		{
			mLock.Wait();
			return mStream.Write(stream);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc cref="MemoryBlockStream.WriteAsync(Stream, CancellationToken)"/>
	public async ValueTask<long> WriteAsync(Stream stream, CancellationToken cancellationToken = default)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			return await mStream.WriteAsync(stream, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			mLock.Release();
		}
	}

	#endregion

	#region CopyTo / Flush

	/// <inheritdoc/>
	public override void CopyTo(Stream destination, int bufferSize)
	{
		try
		{
			mLock.Wait();
			mStream.CopyTo(destination, bufferSize);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			await mStream.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override void Flush()
	{
		try
		{
			mLock.Wait();
			mStream.Flush();
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc/>
	public override async Task FlushAsync(CancellationToken cancellationToken)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			await mStream.FlushAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			mLock.Release();
		}
	}

	#endregion

	#region Buffer Append/Attach/Detach

	/// <inheritdoc cref="MemoryBlockStream.AppendBuffer(ChainableMemoryBlock)"/>
	public void AppendBuffer(ChainableMemoryBlock buffer)
	{
		try
		{
			mLock.Wait();
			mStream.AppendBuffer(buffer);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc cref="MemoryBlockStream.AppendBufferAsync(ChainableMemoryBlock, CancellationToken)"/>
	public async Task AppendBufferAsync(ChainableMemoryBlock buffer, CancellationToken cancellationToken = default)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			await mStream.AppendBufferAsync(buffer, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc cref="MemoryBlockStream.AttachBuffer(ChainableMemoryBlock)"/>
	public void AttachBuffer(ChainableMemoryBlock? buffer)
	{
		try
		{
			mLock.Wait();
			mStream.AttachBuffer(buffer);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc cref="MemoryBlockStream.AttachBufferAsync(ChainableMemoryBlock, CancellationToken)"/>
	public async Task AttachBufferAsync(ChainableMemoryBlock? buffer, CancellationToken cancellationToken = default)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			await mStream.AttachBufferAsync(buffer, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc cref="MemoryBlockStream.DetachBuffer"/>
	public ChainableMemoryBlock? DetachBuffer()
	{
		try
		{
			mLock.Wait();
			return mStream.DetachBuffer();
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc cref="MemoryBlockStream.DetachBufferAsync(CancellationToken)"/>
	public async Task<ChainableMemoryBlock?> DetachBufferAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			return await mStream.DetachBufferAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			mLock.Release();
		}
	}

	#endregion

	#region InjectBufferAtCurrentPosition

	/// <inheritdoc cref="MemoryBlockStream.InjectBufferAtCurrentPosition(ChainableMemoryBlock, bool, bool)"/>
	public void InjectBufferAtCurrentPosition(ChainableMemoryBlock buffer, bool overwrite, bool advancePosition)
	{
		try
		{
			mLock.Wait();
			mStream.InjectBufferAtCurrentPosition(buffer, overwrite, advancePosition);
		}
		finally
		{
			mLock.Release();
		}
	}

	/// <inheritdoc
	///     cref="MemoryBlockStream.InjectBufferAtCurrentPositionAsync(ChainableMemoryBlock, bool, bool, CancellationToken)"/>
	public async Task InjectBufferAtCurrentPositionAsync(
		ChainableMemoryBlock buffer,
		bool                 overwrite,
		bool                 advancePosition,
		CancellationToken    cancellationToken = default)
	{
		try
		{
			await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			await mStream.InjectBufferAtCurrentPositionAsync(buffer, overwrite, advancePosition, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			mLock.Release();
		}
	}

	#endregion
}
