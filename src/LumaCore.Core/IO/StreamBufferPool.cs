// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Core.IO;

/// <summary>
/// Provides <see cref="MemoryBlockStream"/> instances backed by a dedicated, size-controlled
/// <see cref="ArrayPool{T}"/> of <see cref="byte"/>.
/// </summary>
/// <remarks>
///     <para>
///     Consumers must not access the underlying array pool directly. They should use
///     <see cref="CreateMemoryBlockStream()"/> to obtain pre-configured <see cref="MemoryBlockStream"/>
///     instances. This guarantees that only buffers of the configured <see cref="BlockSize"/> are
///     rented from the pool, preventing uncontrolled memory growth in other pool buckets.
///     </para>
///     <para>
///     <b>Thread safety:</b> All members of this class are thread-safe.
///     </para>
/// </remarks>
/// <seealso cref="IStreamBufferPool"/>
/// <seealso cref="StreamBufferPoolOptions"/>
/// <seealso cref="MemoryBlockStream"/>
public sealed class StreamBufferPool : IStreamBufferPool
{
	private readonly ArrayPool<byte> mPool;
	private readonly int             mBlockSize;

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamBufferPool"/> class with the specified options.
	/// </summary>
	/// <param name="options">The configuration options for the pool.</param>
	/// <param name="logger">
	/// Optional logger used to record pool creation. Pass <see langword="null"/> to suppress logging.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	/// <exception cref="ValidationException"><paramref name="options"/> is invalid.</exception>
	public StreamBufferPool(
		StreamBufferPoolOptions    options,
		ILogger<StreamBufferPool>? logger = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.ThrowIfInvalid();

		mBlockSize = options.BlockSize;
		mPool = ArrayPool<byte>.Create(
			maxArrayLength: options.BlockSize,
			maxArraysPerBucket: options.MaxBufferCount);

		logger?.LogDebug(
			"Stream buffer pool created: BlockSize = {BlockSize} bytes, MaxBufferCount = {MaxBufferCount} (effective capacity ≈ {CapacityMb} MB).",
			options.BlockSize,
			options.MaxBufferCount,
			(long)options.BlockSize * options.MaxBufferCount / (1024 * 1024));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamBufferPool"/> class from an
	/// <see cref="IOptions{TOptions}"/> wrapper. Suitable for direct DI registration.
	/// </summary>
	/// <param name="options">The options wrapper.</param>
	/// <param name="logger">Optional logger used to record pool creation.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="options"/> is <see langword="null"/>, or <see cref="IOptions{TOptions}.Value"/> is
	/// <see langword="null"/>.
	/// </exception>
	/// <exception cref="ValidationException">The options are invalid.</exception>
	public StreamBufferPool(IOptions<StreamBufferPoolOptions> options, ILogger<StreamBufferPool>? logger = null)
		: this(GetValue(options), logger) { }

	/// <inheritdoc/>
	public int BlockSize => mBlockSize;

	/// <inheritdoc/>
	public MemoryBlockStream CreateMemoryBlockStream()
	{
		return new MemoryBlockStream(mBlockSize, mPool);
	}

	/// <inheritdoc/>
	public MemoryBlockStream CreateMemoryBlockStream(bool releasesReadBlocks)
	{
		return new MemoryBlockStream(mBlockSize, mPool, releasesReadBlocks);
	}

	private static StreamBufferPoolOptions GetValue(IOptions<StreamBufferPoolOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return options.Value ?? throw new ArgumentNullException(
			       nameof(options),
			       $"{nameof(IOptions<StreamBufferPoolOptions>.Value)} must not be null.");
	}
}
