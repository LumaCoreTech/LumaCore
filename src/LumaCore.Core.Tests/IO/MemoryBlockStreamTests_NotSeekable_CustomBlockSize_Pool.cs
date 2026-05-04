// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;

namespace LumaCore.Core.IO;

/// <summary>
/// Unit tests targeting the <see cref="MemoryBlockStream"/> class.
/// The tests create a stream using <see cref="MemoryBlockStream(int,ArrayPool{byte},bool)"/> constructor.
/// The stream is not seekable and uses a custom block size and rents buffers from an array pool.
/// The stream does not synchronize accesses.
/// </summary>
public class MemoryBlockStreamTests_NotSeekable_CustomBlockSize_Pool : MemoryBlockStreamTestsBase_NotSeekable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryBlockStreamTests_NotSeekable_CustomBlockSize_Pool"/> class.
	/// </summary>
	public MemoryBlockStreamTests_NotSeekable_CustomBlockSize_Pool() : base(false, true) { }

	/// <summary>
	/// Gets the expected size of a memory block in the stream.
	/// </summary>
	protected override int StreamMemoryBlockSize => 8 * 1024;

	/// <summary>
	/// Creates the <see cref="MemoryBlockStream"/> to test.
	/// </summary>
	/// <param name="minimumBlockSize">Minimum size of a memory block in the stream (in bytes).</param>
	/// <returns>The created stream.</returns>
	protected override MemoryBlockStream CreateStreamToTest(int minimumBlockSize = -1)
	{
		if (minimumBlockSize < 0) minimumBlockSize = StreamMemoryBlockSize;
		return new MemoryBlockStream(minimumBlockSize, BufferPool, true);
	}
}
