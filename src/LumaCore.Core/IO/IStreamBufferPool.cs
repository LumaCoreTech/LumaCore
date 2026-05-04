// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;

namespace LumaCore.Core.IO;

/// <summary>
/// Provides <see cref="MemoryBlockStream"/> instances backed by a dedicated, size-controlled
/// <see cref="ArrayPool{T}"/> of <see cref="byte"/>.
/// </summary>
/// <remarks>
///     <para>
///     Consumers must not access the underlying array pool directly. They should use
///     <see cref="CreateMemoryBlockStream()"/> (or the overload accepting <c>releasesReadBlocks</c>)
///     to obtain pre-configured streams. This guarantees that only buffers of the configured
///     <see cref="BlockSize"/> are rented from the pool, preventing uncontrolled memory growth
///     in other pool buckets.
///     </para>
///     <para>
///     The default implementation is <see cref="StreamBufferPool"/>. For DI scenarios, register it
///     as a singleton: <c>services.AddSingleton&lt;IStreamBufferPool, StreamBufferPool&gt;()</c>.
///     </para>
/// </remarks>
/// <seealso cref="StreamBufferPool"/>
/// <seealso cref="StreamBufferPoolOptions"/>
public interface IStreamBufferPool
{
	/// <summary>
	/// Gets the configured block size (in bytes) used for <see cref="MemoryBlockStream"/> buffers.
	/// </summary>
	int BlockSize { get; }

	/// <summary>
	/// Creates a new <see cref="MemoryBlockStream"/> backed by the buffer pool.
	/// The stream is seekable and retains all written blocks until disposed.
	/// </summary>
	/// <returns>
	/// A new <see cref="MemoryBlockStream"/> instance.
	/// The caller must dispose the stream after use to return pooled buffers.
	/// </returns>
	MemoryBlockStream CreateMemoryBlockStream();

	/// <summary>
	/// Creates a new <see cref="MemoryBlockStream"/> backed by the buffer pool.
	/// </summary>
	/// <param name="releasesReadBlocks">
	/// <see langword="true"/> to release memory blocks after they have been read, making the stream
	/// unseekable but reducing memory pressure for large forward-only reads;<br/>
	/// <see langword="false"/> to keep all written blocks, enabling seeking and length changes.
	/// </param>
	/// <returns>
	/// A new <see cref="MemoryBlockStream"/> instance.
	/// The caller must dispose the stream after use to return pooled buffers.
	/// </returns>
	MemoryBlockStream CreateMemoryBlockStream(bool releasesReadBlocks);
}
