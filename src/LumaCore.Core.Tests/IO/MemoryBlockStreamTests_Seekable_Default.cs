// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.IO;

/// <summary>
/// Unit tests targeting the <see cref="MemoryBlockStream"/> class.
/// The tests create a stream using <see cref="MemoryBlockStream()"/> constructor.
/// The stream is seekable and uses the default block size and allocates buffers on the heap.
/// </summary>
public class MemoryBlockStreamTests_Seekable_Default : MemoryBlockStreamTestsBase_Seekable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryBlockStreamTests_Seekable_Default"/> class.
	/// </summary>
	public MemoryBlockStreamTests_Seekable_Default() : base(false, false) { }

	/// <summary>
	/// Gets the expected size of a memory block in the stream.
	/// </summary>
	protected override int StreamMemoryBlockSize => 64 * 1024;

	/// <summary>
	/// Creates the <see cref="MemoryBlockStream"/> to test.
	/// </summary>
	/// <param name="minimumBlockSize">Minimum size of a memory block in the stream (in bytes).</param>
	/// <returns>The created stream.</returns>
	protected override MemoryBlockStream CreateStreamToTest(int minimumBlockSize = -1)
	{
		return new MemoryBlockStream();
	}
}
