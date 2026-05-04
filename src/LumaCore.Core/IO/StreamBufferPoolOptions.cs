// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace LumaCore.Core.IO;

/// <summary>
/// Configuration options for <see cref="StreamBufferPool"/>.
/// </summary>
/// <remarks>
///     <para>
///     This type is designed to be bound from a configuration section using the
///     <c>IOptions&lt;T&gt;</c> pattern:
///     </para>
///     <code>
/// services.Configure&lt;StreamBufferPoolOptions&gt;(configuration.GetSection(StreamBufferPoolOptions.SectionName));
/// services.AddSingleton&lt;IStreamBufferPool, StreamBufferPool&gt;();
///     </code>
/// </remarks>
public sealed class StreamBufferPoolOptions : IValidatableObject
{
	/// <summary>
	/// The default configuration section name for binding from <c>IConfiguration</c>.
	/// </summary>
	public const string SectionName = "StreamBufferPool";

	/// <summary>
	/// The default block size: 64 KB (65 536 bytes).
	/// Chosen to stay below the Large Object Heap threshold (~85 000 bytes) while providing efficient I/O buffering.
	/// </summary>
	public const int DefaultBlockSize = 64 * 1024;

	/// <summary>
	/// The default maximum number of pooled buffers per bucket: 8 192.
	/// Combined with <see cref="DefaultBlockSize"/>, this yields an effective pool capacity of approximately 512 MB.
	/// </summary>
	public const int DefaultMaxBufferCount = 8_192;

	/// <summary>
	/// Minimum allowed block size: 16 KB (16 384 bytes).
	/// Smaller blocks cause excessive per-block overhead in <see cref="MemoryBlockStream"/>.
	/// </summary>
	private const int MinAllowedBlockSize = 16 * 1024;

	/// <summary>
	/// Maximum allowed block size: 1 MB (1 048 576 bytes).
	/// Larger blocks waste memory due to internal fragmentation in the trailing block and reduce
	/// the granularity of <see cref="MemoryBlockStream"/> memory management.
	/// </summary>
	private const int MaxAllowedBlockSize = 1024 * 1024;

	/// <summary>
	/// Minimum allowed buffer count per bucket.
	/// </summary>
	private const int MinAllowedBufferCount = 16;

	/// <summary>
	/// Maximum allowed buffer count per bucket: 131 072.
	/// At 64 KB block size this corresponds to approximately 8 GB — a reasonable upper bound.
	/// </summary>
	private const int MaxAllowedBufferCount = 131_072;

	/// <summary>
	/// Gets or sets the block size (in bytes) used for <see cref="MemoryBlockStream"/> buffers.
	/// Must be a power of two between 16 KB and 1 MB (inclusive).
	/// </summary>
	/// <value>The default is 64 KB (<c>65 536</c> bytes).</value>
	public int BlockSize { get; set; } = DefaultBlockSize;

	/// <summary>
	/// Gets or sets the maximum number of buffers retained per bucket in the underlying
	/// <see cref="System.Buffers.ArrayPool{T}"/>.
	/// The effective pool memory is approximately <c>MaxBufferCount × BlockSize</c>.
	/// </summary>
	/// <value>The default is <c>8 192</c> (~512 MB at 64 KB block size).</value>
	public int MaxBufferCount { get; set; } = DefaultMaxBufferCount;

	/// <summary>
	/// Validates cross-property constraints that cannot be expressed with data annotations alone.
	/// </summary>
	/// <param name="validationContext">The validation context.</param>
	/// <returns>A collection of validation results; empty if validation succeeds.</returns>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (BlockSize < MinAllowedBlockSize)
		{
			yield return new ValidationResult(
				$"{nameof(BlockSize)} must be at least {MinAllowedBlockSize} bytes (16 KB), but was {BlockSize}.",
				[nameof(BlockSize)]);
		}
		else if (BlockSize > MaxAllowedBlockSize)
		{
			yield return new ValidationResult(
				$"{nameof(BlockSize)} must not exceed {MaxAllowedBlockSize} bytes (1 MB), but was {BlockSize}.",
				[nameof(BlockSize)]);
		}
		else if (!BitOperations.IsPow2(BlockSize))
		{
			yield return new ValidationResult(
				$"{nameof(BlockSize)} must be a power of two, but was {BlockSize}.",
				[nameof(BlockSize)]);
		}

		if (MaxBufferCount < MinAllowedBufferCount)
		{
			yield return new ValidationResult(
				$"{nameof(MaxBufferCount)} must be at least {MinAllowedBufferCount}, but was {MaxBufferCount}.",
				[nameof(MaxBufferCount)]);
		}
		else if (MaxBufferCount > MaxAllowedBufferCount)
		{
			yield return new ValidationResult(
				$"{nameof(MaxBufferCount)} must not exceed {MaxAllowedBufferCount}, but was {MaxBufferCount}.",
				[nameof(MaxBufferCount)]);
		}
	}

	/// <summary>
	/// Validates the options and throws a <see cref="ValidationException"/> if any constraint is violated.
	/// Delegates to <see cref="OptionsValidationHelper.ThrowIfInvalid"/> for centralized validation logic.
	/// </summary>
	/// <exception cref="ValidationException">The options are invalid.</exception>
	public void ThrowIfInvalid()
	{
		((IValidatableObject)this).ThrowIfInvalid();
	}
}
