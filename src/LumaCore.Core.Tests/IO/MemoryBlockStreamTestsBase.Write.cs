// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

namespace LumaCore.Core.IO;

public abstract partial class MemoryBlockStreamTestsBase
{
	#region void Write(byte[],int,int)

	/// <summary>
	/// Writes a random set of bytes into the stream using <see cref="MemoryBlockStream.Write(byte[],int,int)"/>.
	/// The write is one in a single operation.
	/// </summary>
	[Theory]
	[InlineData(0)]            // write empty buffer
	[InlineData(1)]            // write buffer with 1 byte
	[InlineData(TestDataSize)] // write huge buffer that results in multiple blocks in the stream
	public void Write_Buffer_SingleOperation(int count)
	{
		TestWrite(count, Operation);
		return;

		static void Operation(MemoryBlockStream stream, byte[] data)
		{
			stream.Write(data, 0, data.Length);
		}
	}

	/// <summary>
	/// Writes a random set of bytes into the stream using <see cref="MemoryBlockStream.Write(byte[],int,int)"/>.
	/// The write operation is done with multiple smaller write operations.
	/// </summary>
	[Theory]
	[InlineData(TestDataSize, 8 * 1024)] // chunk size is power of 2 => fills up full blocks
	[InlineData(TestDataSize, 999)]      // chunk size is odd => write spans blocks
	public void Write_Buffer_MultipleOperations(int count, int chunkSize)
	{
		TestWrite(count, Operation);
		return;

		void Operation(MemoryBlockStream stream, byte[] data)
		{
			int offset = 0;
			do
			{
				int bytesToWrite = Math.Min(data.Length - offset, chunkSize);
				stream.Write(data, offset, bytesToWrite);
				offset += bytesToWrite;
			} while (offset < data.Length);
		}
	}

	#endregion

	#region void Write(ReadOnlySpan<byte>)

	/// <summary>
	/// Writes a random set of bytes into the stream using <see cref="MemoryBlockStream.Write(ReadOnlySpan{byte})"/>.
	/// The write is one in a single operation.
	/// </summary>
	[Theory]
	[InlineData(0)]            // write empty buffer
	[InlineData(1)]            // write buffer with 1 byte
	[InlineData(TestDataSize)] // write huge buffer that results in multiple blocks in the stream
	public void Write_ReadOnlySpan_SingleOperation(int count)
	{
		TestWrite(count, Operation);
		return;

		static void Operation(MemoryBlockStream stream, byte[] data)
		{
			stream.Write(data.AsSpan(0, data.Length));
		}
	}

	/// <summary>
	/// Writes a random set of bytes into the stream using <see cref="MemoryBlockStream.Write(ReadOnlySpan{byte})"/>.
	/// The write operation is done with multiple smaller write operations.
	/// </summary>
	[Theory]
	[InlineData(TestDataSize, 8 * 1024)] // chunk size is power of 2 => fills up full blocks
	[InlineData(TestDataSize, 999)]      // chunk size is odd => write spans blocks
	public void Write_ReadOnlySpan_MultipleOperations(int count, int chunkSize)
	{
		TestWrite(count, Operation);
		return;

		void Operation(MemoryBlockStream stream, byte[] data)
		{
			// write the buffer in chunks
			int offset = 0;
			do
			{
				int bytesToWrite = Math.Min(data.Length - offset, chunkSize);
				stream.Write(data.AsSpan(offset, bytesToWrite));
				offset += bytesToWrite;
			} while (offset < data.Length);
		}
	}

	#endregion

	#region long Write(Stream)

	/// <summary>
	/// Writes a random set of bytes into the stream using <see cref="MemoryBlockStream.Write(Stream)"/>.
	/// </summary>
	[Theory]
	[InlineData(0)]            // write empty buffer
	[InlineData(1)]            // write buffer with 1 byte
	[InlineData(TestDataSize)] // write huge buffer that results in multiple blocks in the stream
	public void Write_Stream(int count)
	{
		TestWrite(count, Operation);
		return;

		static void Operation(MemoryBlockStream stream, byte[] data)
		{
			long bytesWritten = stream.Write(new MemoryStream(data));
			Assert.Equal(data.Length, bytesWritten);
		}
	}

	#endregion

	#region WriteByte()

	/// <summary>
	/// Writes a random set of bytes into the stream using <see cref="MemoryBlockStream.WriteByte"/>.
	/// </summary>
	[Theory]
	[InlineData(0)]            // write empty buffer
	[InlineData(1)]            // write buffer with 1 byte
	[InlineData(TestDataSize)] // write huge buffer that results in multiple blocks in the stream
	public void WriteByte(int count)
	{
		TestWrite(count, Operation);
		return;

		static void Operation(MemoryBlockStream stream, byte[] data)
		{
			foreach (byte x in data) stream.WriteByte(x);
		}
	}

	#endregion

	#region Task WriteAsync(byte[],int,int,CancellationToken)

	/// <summary>
	/// Writes a random set of bytes into the stream using
	/// <see cref="MemoryBlockStream.WriteAsync(byte[],int,int,CancellationToken)"/>.
	/// The write is one in a single operation.
	/// </summary>
	[Theory]
	[InlineData(0)]            // write empty buffer
	[InlineData(1)]            // write buffer with 1 byte
	[InlineData(TestDataSize)] // write huge buffer that results in multiple blocks in the stream
	public Task WriteAsync_Buffer_SingleOperation(int count)
	{
		return TestWriteAsync(count, Operation);

		static Task Operation(MemoryBlockStream stream, byte[] data)
		{
			return stream.WriteAsync(data, 0, data.Length, CancellationToken.None);
		}
	}

	/// <summary>
	/// Writes a random set of bytes into the stream using
	/// <see cref="MemoryBlockStream.WriteAsync(byte[],int,int,CancellationToken)"/>.
	/// The write operation is done with multiple smaller write operations.
	/// </summary>
	[Theory]
	[InlineData(TestDataSize, 8 * 1024)] // chunk size is power of 2 => fills up full blocks
	[InlineData(TestDataSize, 999)]      // chunk size is odd => write spans blocks
	public Task WriteAsync_Buffer_MultipleOperations(int count, int chunkSize)
	{
		return TestWriteAsync(count, Operation);

		async Task Operation(MemoryBlockStream stream, byte[] data)
		{
			int offset = 0;
			do
			{
				int bytesToWrite = Math.Min(data.Length - offset, chunkSize);

				await stream.WriteAsync(
					data,
					offset,
					bytesToWrite,
					CancellationToken.None);

				offset += bytesToWrite;
			} while (offset < data.Length);
		}
	}

	#endregion

	#region Task WriteAsync(ReadOnlyMemory<byte>,CancellationToken)

	/// <summary>
	/// Writes a random set of bytes into the stream using
	/// <see cref="MemoryBlockStream.WriteAsync(ReadOnlyMemory{byte},CancellationToken)"/>.
	/// The write is one in a single operation.
	/// </summary>
	[Theory]
	[InlineData(0)]            // write empty buffer
	[InlineData(1)]            // write buffer with 1 byte
	[InlineData(TestDataSize)] // write huge buffer that results in multiple blocks in the stream
	public Task WriteAsync_ReadOnlyMemory_SingleOperation(int count)
	{
		return TestWriteAsync(count, Operation);

		static async Task Operation(MemoryBlockStream stream, byte[] data)
		{
			await stream.WriteAsync(
				data.AsMemory(0, data.Length),
				CancellationToken.None);
		}
	}

	/// <summary>
	/// Writes a random set of bytes into the stream using
	/// <see cref="MemoryBlockStream.WriteAsync(ReadOnlyMemory{byte},CancellationToken)"/>.
	/// The write operation is done with multiple smaller write operations.
	/// </summary>
	[Theory]
	[InlineData(TestDataSize, 8 * 1024)] // chunk size is power of 2 => fills up full blocks
	[InlineData(TestDataSize, 999)]      // chunk size is odd => write spans blocks
	public Task WriteAsync_ReadOnlyMemory_MultipleOperations(int count, int chunkSize)
	{
		return TestWriteAsync(count, Operation);

		async Task Operation(MemoryBlockStream stream, byte[] data)
		{
			int offset = 0;
			do
			{
				int bytesToWrite = Math.Min(data.Length - offset, chunkSize);

				// write the buffer
				await stream.WriteAsync(
					data.AsMemory(offset, bytesToWrite),
					CancellationToken.None);

				offset += bytesToWrite;
			} while (offset < data.Length);
		}
	}

	#endregion

	#region Task WriteAsync(Stream,CancellationToken)

	/// <summary>
	/// Writes a random set of bytes into the stream using <see cref="MemoryBlockStream.WriteAsync(Stream,CancellationToken)"/>
	/// .
	/// </summary>
	[Theory]
	[InlineData(0)]            // write empty buffer
	[InlineData(1)]            // write buffer with 1 byte
	[InlineData(TestDataSize)] // write huge buffer that results in multiple blocks in the stream
	public Task WriteAsync_Stream(int count)
	{
		return TestWriteAsync(count, Operation);

		static async Task Operation(MemoryBlockStream stream, byte[] data)
		{
			await stream.WriteAsync(
				new MemoryStream(data),
				CancellationToken.None);
		}
	}

	#endregion

	#region [[ Write Test Frames ]]

	/// <summary>
	/// Common test frame for synchronous write operations.
	/// </summary>
	/// <param name="count">Number of bytes to write to the stream.</param>
	/// <param name="operation">Operation that performs the write operation.</param>
	private void TestWrite(int count, Action<MemoryBlockStream, byte[]> operation)
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();

		// generate some test data
		using (GetRandomTestDataChain(count, StreamMemoryBlockSize, out List<byte> list))
		{
			byte[] data = [.. list];

			// write data to the stream
			operation(stream, data);

			// the stream should contain the written data now
			Assert.Equal(data.Length, stream.Position);
			Assert.Equal(data.Length, stream.Length);
			using (ChainableMemoryBlock? detachedBuffer = stream.DetachBuffer())
			{
				if (count > 0) Assert.Equal(data, detachedBuffer!.GetChainData());
				else Assert.Null(detachedBuffer);
			}
		}
	}

	/// <summary>
	/// Common test frame for asynchronous write operations.
	/// </summary>
	/// <param name="count">Number of bytes to write to the stream.</param>
	/// <param name="operation">Operation that performs the write operation.</param>
	private async Task TestWriteAsync(int count, Func<MemoryBlockStream, byte[], Task> operation)
	{
		// create a new stream
		await using MemoryBlockStream stream = CreateStreamToTest();

		// generate some test data
		using (GetRandomTestDataChain(count, StreamMemoryBlockSize, out List<byte> list))
		{
			byte[] data = [.. list];

			// write the buffer
			await operation(stream, data).ConfigureAwait(false);

			// the stream should contain the written data now
			Assert.Equal(data.Length, stream.Position);
			Assert.Equal(data.Length, stream.Length);
			using (ChainableMemoryBlock? detachedBuffer = await stream.DetachBufferAsync().ConfigureAwait(false))
			{
				if (count > 0) Assert.Equal(data, detachedBuffer!.GetChainData());
				else Assert.Null(detachedBuffer);
			}
		}
	}

	#endregion
}
