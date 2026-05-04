// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Core.IO;

public abstract partial class MemoryBlockStreamTestsBase
{
	/// <summary>
	/// The size of test data sets tests juggle with.
	/// </summary>
	protected const int TestDataSize = 256 * 1024;

	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryBlockStreamTestsBase"/> class.
	/// </summary>
	/// <param name="synchronized">
	/// <see langword="true"/> if the stream is synchronized;<br/>
	/// otherwise <see langword="false"/>.
	/// </param>
	/// <param name="usePool">
	/// <see langword="true"/> if the stream uses buffer pooling;<br/>
	/// otherwise <see langword="false"/>.
	/// </param>
	protected MemoryBlockStreamTestsBase(bool synchronized, bool usePool)
	{
		StreamIsSynchronized = synchronized;
		if (usePool) BufferPool = new ArrayPoolMock();
	}

	/// <summary>
	/// Test Teardown.
	/// </summary>
	public void Dispose()
	{
		// ensure that the stream has returned rented buffers to the pool
		EnsureBuffersHaveBeenReturned(0);

		GC.SuppressFinalize(this);
	}

	#region Test Specific Overrides

	/// <summary>
	/// Creates the <see cref="MemoryBlockStream"/> to test.
	/// </summary>
	/// <param name="minimumBlockSize">Minimum size of a memory block in the stream (in bytes).</param>
	/// <returns>The stream to test.</returns>
	protected abstract MemoryBlockStream CreateStreamToTest(int minimumBlockSize = -1);

	/// <summary>
	/// Gets a value indicating whether the stream can seek.
	/// </summary>
	protected abstract bool StreamCanSeek { get; }

	/// <summary>
	/// Gets the expected size of a memory block in the stream.
	/// </summary>
	protected abstract int StreamMemoryBlockSize { get; }

	/// <summary>
	/// Gets a value indicating whether the stream is synchronized.
	/// </summary>
	protected bool StreamIsSynchronized { get; }

	/// <summary>
	/// Gets the array pool used by the stream, if the stream uses pooled buffers.
	/// </summary>
	internal ArrayPoolMock? BufferPool { get; }

	#endregion

	#region Stream Construction

	/// <summary>
	/// Creates a new stream and checks its properties.
	/// </summary>
	[Fact]
	public void CreateNewStream()
	{
		// create a new stream
		MemoryBlockStream stream = CreateStreamToTest();

		// check capabilities of the stream
		Assert.True(stream.CanRead);
		Assert.True(stream.CanWrite);
		Assert.Equal(StreamCanSeek, stream.CanSeek);

		// check position and length of the stream
		Assert.Equal(0, stream.Position);
		Assert.Equal(0, stream.Length);

		// the stream does not support timeouts
		Assert.Throws<InvalidOperationException>(() => stream.ReadTimeout);
		Assert.Throws<InvalidOperationException>(() => stream.WriteTimeout);

		// detach internal buffer and check that there is no buffer, yet
		ChainableMemoryBlock? buffer = stream.DetachBuffer();
		Assert.Null(buffer);
	}

	#endregion

	#region void Flush()

	/// <summary>
	/// Tests flushing the stream using <see cref="MemoryBlockStream.Flush"/>
	/// (should not do anything with the stream as it's purely backed by memory).
	/// </summary>
	[Fact]
	public void Flush()
	{
		MemoryBlockStream stream = CreateStreamToTest();
		stream.Flush();
	}

	#endregion

	#region Task FlushAsync()

	/// <summary>
	/// Tests flushing the stream using <see cref="MemoryBlockStream.FlushAsync(CancellationToken)"/>
	/// (should not do anything with the stream as it's purely backed by memory).
	/// </summary>
	[Fact]
	public Task FlushAsync()
	{
		MemoryBlockStream stream = CreateStreamToTest();
		return stream.FlushAsync(CancellationToken.None);
	}

	#endregion

	#region void CopyTo()

	/// <summary>
	/// Copies a random set of bytes into the stream and copies the stream to another stream
	/// using <see cref="MemoryBlockStream.CopyTo(System.IO.Stream,int)"/>.
	/// </summary>
	[Theory]
	[InlineData(0)]            // write empty buffer
	[InlineData(1)]            // write buffer with 1 byte
	[InlineData(TestDataSize)] // write huge buffer that results in multiple blocks in the stream
	public void CopyTo(int count)
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();
		// generate some test data
		using ChainableMemoryBlock? chain = GetRandomTestDataChain(count, StreamMemoryBlockSize, out List<byte> list);
		byte[] data = [.. list];

		// attach chain of blocks to the stream
		stream.AttachBuffer(chain);

		// copy the stream to another stream
		var otherStream = new MemoryStream();
		stream.CopyTo(otherStream, 8 * 1024);

		// the read stream should be at its end now
		Assert.Equal(data.Length, stream.Position);
		Assert.Equal(data.Length, stream.Length);

		// the other stream should contain the written data now
		Assert.Equal(data.Length, otherStream.Position);
		Assert.Equal(data.Length, otherStream.Length);
		otherStream.Position = 0;
		Assert.Equal(data, otherStream.ToArray());
	}

	#endregion

	#region Task CopyToAsync()

	/// <summary>
	/// Copies a random set of bytes into the stream and copies the stream to another stream
	/// using <see cref="MemoryBlockStream.CopyToAsync(Stream,int,CancellationToken)"/>.
	/// </summary>
	[Theory]
	[InlineData(0)]            // write empty buffer
	[InlineData(1)]            // write buffer with 1 byte
	[InlineData(TestDataSize)] // write huge buffer that results in multiple blocks in the stream
	public async Task CopyToAsync(int count)
	{
		// create a new stream
		await using MemoryBlockStream stream = CreateStreamToTest();

		// generate some test data
		using ChainableMemoryBlock? chain = GetRandomTestDataChain(count, StreamMemoryBlockSize, out List<byte> list);
		byte[] data = [.. list];

		// attach chain of blocks to the stream
		await stream.AttachBufferAsync(chain);

		// copy the stream to another stream
		var otherStream = new MemoryStream();
		await stream.CopyToAsync(
			otherStream,
			8 * 1024,
			CancellationToken.None);

		// the read stream should be at its end now
		Assert.Equal(data.Length, stream.Position);
		Assert.Equal(data.Length, stream.Length);

		// the other stream should contain the written data now
		Assert.Equal(data.Length, otherStream.Position);
		Assert.Equal(data.Length, otherStream.Length);
		otherStream.Position = 0;
		Assert.Equal(data, otherStream.ToArray());
	}

	#endregion

	#region Helpers: Test Data Generation

	/// <summary>
	/// Gets some random test data packed into a chain of memory blocks that can be attached to a memory block stream.
	/// </summary>
	/// <param name="count">Number of random bytes to get.</param>
	/// <param name="blockSize">Minimum size of memory blocks the returned chain of memory blocks should have.</param>
	/// <param name="data">The generated test data.</param>
	/// <returns>First block in the chain of created memory blocks</returns>
	protected ChainableMemoryBlock? GetRandomTestDataChain(int count, int blockSize, out List<byte> data)
	{
		if (count == 0)
		{
			data = [];
			return null;
		}

		ChainableMemoryBlock? firstBlock = null;
		ChainableMemoryBlock? previousBlock = null;
		var random = new Random(0);
		data = new List<byte>(count);
		int remaining = count;
		while (remaining > 0)
		{
			// allocate or rent a buffer
			// (buffer may be larger than requested, if rented from the pool)
			var block = new ChainableMemoryBlock(blockSize, BufferPool, false);
			random.NextBytes(block.Buffer);
			block.Length = Math.Min(remaining, block.Capacity);
			previousBlock?.Next = block;
			data.AddRange(block.Buffer.Take(block.Length));
			remaining -= block.Length;
			firstBlock ??= block;
			previousBlock = block;
		}

		return firstBlock;
	}

	#endregion

	#region Helpers: Rented Buffer Checks

	/// <summary>
	/// Checks whether all buffers have been returned to the array pool, if applicable.
	/// </summary>
	protected void EnsureBuffersHaveBeenReturned(int expectedBufferCount)
	{
		if (BufferPool != null)
		{
			Assert.Equal(expectedBufferCount, BufferPool.RentedBufferCount);
		}
	}

	/// <summary>
	/// Checks whether not all buffers have been returned to the array pool, if applicable.
	/// </summary>
	protected void EnsureBuffersHaveNotBeenReturned()
	{
		if (BufferPool != null)
		{
			Assert.True(BufferPool.RentedBufferCount > 0);
		}
	}

	#endregion
}
