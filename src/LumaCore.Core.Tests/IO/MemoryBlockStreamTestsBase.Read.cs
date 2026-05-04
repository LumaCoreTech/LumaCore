// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

namespace LumaCore.Core.IO;

public abstract partial class MemoryBlockStreamTestsBase
{
	#region int Read(byte[],int,int)

	/// <summary>
	/// Prepares a chain of memory blocks, attaches it to the stream and reads the stream
	/// using <see cref="MemoryBlockStream.Read(byte[],int,int)"/>.
	/// </summary>
	[Theory]
	[InlineData(false, 0)]            // empty stream
	[InlineData(false, 1)]            // stream with 1 byte in a single block
	[InlineData(false, TestDataSize)] // huge stream with multiple blocks, read all in single operation
	[InlineData(true, TestDataSize)]  // huge stream with multiple blocks, read in chunks
	public void Read_Buffer(bool chunkedRead, int initialLength)
	{
		TestRead(initialLength, chunkedRead, Operation);
		return;

		static int Operation(MemoryBlockStream stream, byte[] readBuffer, ref int bytesToRead)
		{
			return stream.Read(readBuffer, 0, bytesToRead);
		}
	}

	#endregion

	#region int Read(Span<byte>)

	/// <summary>
	/// Prepares a chain of memory blocks, attaches it to the stream and reads the stream
	/// using <see cref="MemoryBlockStream.Read(Span{byte})"/>.
	/// </summary>
	[Theory]
	[InlineData(false, 0)]            // empty stream
	[InlineData(false, 1)]            // stream with 1 byte in a single block
	[InlineData(false, TestDataSize)] // huge stream with multiple blocks, read all in single operation
	[InlineData(true, TestDataSize)]  // huge stream with multiple blocks, read in chunks
	public void Read_Span(bool chunkedRead, int initialLength)
	{
		TestRead(initialLength, chunkedRead, Operation);
		return;

		static int Operation(MemoryBlockStream stream, byte[] readBuffer, ref int bytesToRead)
		{
			return stream.Read(readBuffer.AsSpan(0, bytesToRead));
		}
	}

	#endregion

	#region int ReadAsync(byte[],int,int,CancellationToken)

	/// <summary>
	/// Prepares a chain of memory blocks, attaches it to the stream and reads the stream
	/// using <see cref="MemoryBlockStream.ReadAsync(byte[],int,int,CancellationToken)"/>.
	/// </summary>
	[Theory]
	[InlineData(false, 0)]            // empty stream
	[InlineData(false, 1)]            // stream with 1 byte in a single block
	[InlineData(false, TestDataSize)] // huge stream with multiple blocks, read all in single operation
	[InlineData(true, TestDataSize)]  // huge stream with multiple blocks, read in chunks
	public Task ReadAsync_Buffer(bool chunkedRead, int initialLength)
	{
		return TestReadAsync(initialLength, chunkedRead, Operation);

		static async Task<int> Operation(MemoryBlockStream stream, byte[] readBuffer, int bytesToRead)
		{
			int readByteCount = await stream.ReadAsync(
				                    readBuffer,
				                    0,
				                    bytesToRead,
				                    CancellationToken.None);
			return readByteCount;
		}
	}

	#endregion

	#region int ReadAsync(Memory{byte},CancellationToken)

	/// <summary>
	/// Prepares a chain of memory blocks, attaches it to the stream and reads the stream
	/// using <see cref="MemoryBlockStream.ReadAsync(Memory{byte},CancellationToken)"/>.
	/// </summary>
	[Theory]
	[InlineData(false, 0)]            // empty stream
	[InlineData(false, 1)]            // stream with 1 byte in a single block
	[InlineData(false, TestDataSize)] // huge stream with multiple blocks, read all in single operation
	[InlineData(true, TestDataSize)]  // huge stream with multiple blocks, read in chunks
	public Task ReadAsync_Memory(bool chunkedRead, int initialLength)
	{
		return TestReadAsync(initialLength, chunkedRead, Operation);

		static async Task<int> Operation(MemoryBlockStream stream, byte[] readBuffer, int bytesToRead)
		{
			int readByteCount = await stream.ReadAsync(
				                    readBuffer.AsMemory(0, bytesToRead),
				                    CancellationToken.None);
			return readByteCount;
		}
	}

	#endregion

	#region int ReadByte()

	/// <summary>
	/// Prepares a chain of blocks, attaches it to the stream and reads the stream
	/// using <see cref="MemoryBlockStream.ReadByte"/>.
	/// </summary>
	[Theory]
	[InlineData(0)]            // empty stream
	[InlineData(1)]            // stream with 1 byte in a single block
	[InlineData(TestDataSize)] // huge stream with multiple blocks
	public void ReadByte(int initialLength)
	{
		// test reading byte-wise
		// (use chunked reading as the operation overrides the number of bytes to read)
		TestRead(initialLength, true, Operation);
		return;

		static int Operation(MemoryBlockStream stream, byte[] readBuffer, ref int bytesToRead)
		{
			if (readBuffer.Length <= 0) return 0;
			bytesToRead = 1; // overrides the number of bytes to read, so the test does not fail...
			int readByte = stream.ReadByte();
			if (readByte < 0) return 0;
			readBuffer[0] = (byte)readByte;
			return 1;
		}
	}

	#endregion

	#region Helpers: Read Test Frames

	private delegate int ReadOperation(MemoryBlockStream stream, byte[] readBuffer, ref int bytesToRead);

	/// <summary>
	/// Common test frame for synchronous read operations.
	/// </summary>
	/// <param name="initialLength">Initial length of the stream.</param>
	/// <param name="randomChunks">
	/// <see langword="true"/> to read in chunks of random size;
	/// <see langword="false"/> to read the entire stream at once.
	/// </param>
	/// <param name="operation">Operation that performs the read operation.</param>
	private void TestRead(int initialLength, bool randomChunks, ReadOperation operation)
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();
		// generate some test data and attach it to the stream
		ChainableMemoryBlock chain = GetRandomTestDataChain(
			initialLength,
			StreamMemoryBlockSize,
			out List<byte> expectedData)!;
		stream.AttachBuffer(chain);

		// try to read zero bytes (should work as well and do nothing)
		int bytesToRead = 0;
		int bytesRead = operation(stream, [], ref bytesToRead);
		Assert.Equal(0, bytesRead);

		var readData = new List<byte>(expectedData.Count);
		if (randomChunks)
		{
			// read data in chunks of random size
			var random = new Random(0);
			byte[] readBuffer = new byte[8 * 1024];
			int remaining = expectedData.Count;
			while (true)
			{
				bytesToRead = random.Next(1, readBuffer.Length);
				bytesRead = operation(stream, readBuffer, ref bytesToRead);
				int expectedByteRead = Math.Min(bytesToRead, remaining);
				Assert.Equal(expectedByteRead, bytesRead);
				if (bytesRead == 0) break;
				readData.AddRange(readBuffer.Take(bytesRead));
				remaining -= bytesRead;
			}
		}
		else
		{
			// read entire stream at once
			byte[] readBuffer = new byte[expectedData.Count + 1];
			bytesToRead = readBuffer.Length;
			bytesRead = operation(stream, readBuffer, ref bytesToRead); // operation should not override bytesToRead
			readData.AddRange(readBuffer.Take(bytesRead));
		}

		// the stream has been read to the end
		// it should be empty now and read data should equal the expected test data
		Assert.Equal(expectedData.Count, stream.Position);
		Assert.Equal(expectedData.Count, stream.Length);
		Assert.Equal(expectedData, readData);

		// the stream should have returned its buffers to the pool, if release-after-read is enabled
		if (initialLength > 0)
		{
			if (stream.ReleasesReadBlocks) EnsureBuffersHaveBeenReturned(1);
			else EnsureBuffersHaveNotBeenReturned();
		}
		else
		{
			EnsureBuffersHaveBeenReturned(0);
		}
	}

	/// <summary>
	/// Common test frame for asynchronous read operations.
	/// </summary>
	/// <param name="initialLength">Initial length of the stream.</param>
	/// <param name="randomChunks">
	/// <see langword="true"/> to read in chunks of random size;
	/// <see langword="false"/> to read the entire stream at once.
	/// </param>
	/// <param name="operation">Operation that performs the read operation.</param>
	private async Task TestReadAsync(
		int                                             initialLength,
		bool                                            randomChunks,
		Func<MemoryBlockStream, byte[], int, Task<int>> operation)
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();

		// generate some test data and attach it to the stream
		ChainableMemoryBlock chain = GetRandomTestDataChain(
			initialLength,
			StreamMemoryBlockSize,
			out List<byte> expectedData)!;
		await stream.AttachBufferAsync(chain).ConfigureAwait(false);

		// try to read zero bytes (should work as well and do nothing)
		int bytesToRead = 0;
		int bytesRead = await operation(stream, [], bytesToRead).ConfigureAwait(false);
		Assert.Equal(0, bytesRead);

		var readData = new List<byte>(expectedData.Count);
		if (randomChunks)
		{
			// read data in chunks of random size
			var random = new Random(0);
			byte[] readBuffer = new byte[8 * 1024];
			while (true)
			{
				bytesToRead = random.Next(1, readBuffer.Length);
				bytesRead = await operation(stream, readBuffer, bytesToRead).ConfigureAwait(false);
				if (bytesRead == 0) break;
				readData.AddRange(readBuffer.Take(bytesRead));
			}
		}
		else
		{
			// read entire stream at once
			byte[] readBuffer = new byte[expectedData.Count + 1];
			bytesToRead = readBuffer.Length;
			bytesRead = await operation(stream, readBuffer, bytesToRead).ConfigureAwait(false);
			readData.AddRange(readBuffer.Take(bytesRead));
		}

		// the stream has been read to the end
		// it should be empty now and read data should equal the expected test data
		Assert.Equal(expectedData.Count, stream.Position);
		Assert.Equal(expectedData.Count, stream.Length);
		Assert.Equal(expectedData, readData);

		// the stream should have returned its buffers to the pool, if release-after-read is enabled
		if (initialLength > 0)
		{
			if (stream.ReleasesReadBlocks) EnsureBuffersHaveBeenReturned(1);
			else EnsureBuffersHaveNotBeenReturned();
		}
		else
		{
			EnsureBuffersHaveBeenReturned(0);
		}
	}

	#endregion
}
