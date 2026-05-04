// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

// ReSharper disable UseAwaitUsing

namespace LumaCore.Core.IO;

/// <summary>
/// Unit tests targeting the <see cref="MemoryBlockStream"/> class.
/// </summary>
public abstract partial class MemoryBlockStreamTestsBase : IDisposable
{
	#region void AttachBuffer()

	/// <summary>
	/// Attaches a prepared chain of memory blocks to the stream using <see cref="MemoryBlockStream.AttachBuffer"/>.
	/// </summary>
	[Fact]
	public void AttachBuffer()
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();
		// generate some test data and attach the buffer to the stream
		ChainableMemoryBlock? chain = GetRandomTestDataChain(TestDataSize, StreamMemoryBlockSize, out List<byte> data);
		stream.AttachBuffer(chain);

		// the stream's properties should reflect the new buffer
		Assert.Equal(0, stream.Position);
		Assert.Equal(data.Count, stream.Length);
	}

	#endregion

	#region Task AttachBufferAsync()

	/// <summary>
	/// Attaches a prepared chain of memory blocks to the stream using <see cref="MemoryBlockStream.AttachBufferAsync"/>.
	/// </summary>
	[Fact]
	public async Task AttachBufferAsync()
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();

		// generate some test data and attach the buffer to the stream
		ChainableMemoryBlock? chain = GetRandomTestDataChain(TestDataSize, StreamMemoryBlockSize, out List<byte> data);
		await stream.AttachBufferAsync(chain, CancellationToken.None);

		// the stream's properties should reflect the new buffer
		Assert.Equal(0, stream.Position);
		Assert.Equal(data.Count, stream.Length);
	}

	#endregion

	#region ChainableMemoryBlock DetachBuffer()

	/// <summary>
	/// Detaches the chain of memory blocks from the stream using <see cref="MemoryBlockStream.DetachBuffer"/>.
	/// </summary>
	[Fact]
	public void DetachBuffer()
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();
		// generate some test data and pass ownership to the stream
		ChainableMemoryBlock? chain = GetRandomTestDataChain(TestDataSize, StreamMemoryBlockSize, out List<byte> data);
		stream.AttachBuffer(chain);

		// the stream's properties should reflect the new buffer
		Assert.Equal(0, stream.Position);
		Assert.Equal(data.Count, stream.Length);

		// detach the buffer, should be the same as attached
		using ChainableMemoryBlock? firstBlock = stream.DetachBuffer();
		Assert.Same(chain, firstBlock);

		// the stream should be empty now
		Assert.Equal(0, stream.Position);
		Assert.Equal(0, stream.Length);

		// check whether the detached buffer contains the same data as the attached buffer
		// (ensures that the stream did not modify it during the procedure)
		Assert.Equal(data, firstBlock!.GetChainData());
	}

	#endregion

	#region ChainableMemoryBlock DetachBufferAsync(CancellationToken)

	/// <summary>
	/// Detaches the chain of memory blocks from the stream using <see cref="MemoryBlockStream.DetachBufferAsync"/>.
	/// </summary>
	[Fact]
	public async Task DetachBufferAsync()
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();

		// generate some test data and pass ownership to the stream
		ChainableMemoryBlock? chain = GetRandomTestDataChain(TestDataSize, StreamMemoryBlockSize, out List<byte> data);
		await stream.AttachBufferAsync(chain);

		// the stream's properties should reflect the new buffer
		Assert.Equal(0, stream.Position);
		Assert.Equal(data.Count, stream.Length);

		// detach the buffer, should be the same as attached
		using ChainableMemoryBlock? firstBlock = await stream.DetachBufferAsync(CancellationToken.None);
		Assert.Same(chain, firstBlock);

		// the stream should be empty now
		Assert.Equal(0, stream.Position);
		Assert.Equal(0, stream.Length);

		// check whether the detached buffer contains the same data as the attached buffer
		// (ensures that the stream did not modify it during the procedure)
		Assert.Equal(data, firstBlock!.GetChainData());
	}

	#endregion

	#region void AppendBuffer(ChainableMemoryBlock)

	/// <summary>
	/// Appends a memory block to the stream using <see cref="MemoryBlockStream.AppendBuffer"/>.
	/// The initial stream is empty.
	/// </summary>
	[Fact]
	public void AppendBuffer_EmptyStream()
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();
		// generate some test data
		ChainableMemoryBlock chain = GetRandomTestDataChain(TestDataSize, StreamMemoryBlockSize, out List<byte> data)!;

		// append the second buffer
		stream.AppendBuffer(chain);

		// the stream should contain data from both buffers
		Assert.Equal(0, stream.Position);
		Assert.Equal(data.Count, stream.Length);
		using ChainableMemoryBlock? detachedBuffer = stream.DetachBuffer();
		Assert.Equal(data, detachedBuffer!.GetChainData());
	}

	/// <summary>
	/// Appends a memory block to the stream using <see cref="MemoryBlockStream.AppendBuffer"/>.
	/// The initial stream contains some data.
	/// </summary>
	[Fact]
	public void AppendBuffer_NonEmptyStream()
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();
		// generate some test data
		ChainableMemoryBlock chain1 = GetRandomTestDataChain(
			TestDataSize,
			StreamMemoryBlockSize,
			out List<byte> data1)!;
		ChainableMemoryBlock chain2 = GetRandomTestDataChain(
			TestDataSize,
			StreamMemoryBlockSize,
			out List<byte> data2)!;

		// attach the first buffer to the stream
		stream.AttachBuffer(chain1);

		// append the second buffer
		stream.AppendBuffer(chain2);

		// the stream should contain data from both buffers
		Assert.Equal(0, stream.Position);
		Assert.Equal(data1.Count + data2.Count, stream.Length);
		using ChainableMemoryBlock? detachedBuffer = stream.DetachBuffer();
		Assert.Equal(data1.Concat(data2), detachedBuffer!.GetChainData());
	}

	#endregion

	#region Task AppendBufferAsync(ChainableMemoryBlock,CancellationToken)

	/// <summary>
	/// Appends a memory block to the stream using <see cref="MemoryBlockStream.AppendBufferAsync"/>.
	/// The initial stream is empty.
	/// </summary>
	[Fact]
	public async Task AppendBufferAsync_EmptyStream()
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();

		// generate some test data
		ChainableMemoryBlock chain = GetRandomTestDataChain(TestDataSize, StreamMemoryBlockSize, out List<byte> data)!;

		// append the second buffer
		await stream.AppendBufferAsync(chain, CancellationToken.None);

		// the stream should contain data from both buffers
		Assert.Equal(0, stream.Position);
		Assert.Equal(data.Count, stream.Length);
		using ChainableMemoryBlock? detachedBuffer = await stream.DetachBufferAsync();
		Assert.Equal(data, detachedBuffer!.GetChainData());
	}

	/// <summary>
	/// Appends a memory block to the stream using <see cref="MemoryBlockStream.AppendBufferAsync"/>.
	/// The initial stream contains some data.
	/// </summary>
	[Fact]
	public async Task AppendBufferAsync_NonEmptyStream()
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();

		// generate some test data
		ChainableMemoryBlock chain1 = GetRandomTestDataChain(
			TestDataSize,
			StreamMemoryBlockSize,
			out List<byte> data1)!;
		ChainableMemoryBlock chain2 = GetRandomTestDataChain(
			TestDataSize,
			StreamMemoryBlockSize,
			out List<byte> data2)!;

		// attach the first buffer to the stream
		await stream.AttachBufferAsync(chain1, CancellationToken.None);

		// append the second buffer
		await stream.AppendBufferAsync(chain2, CancellationToken.None);

		// the stream should contain data from both buffers
		Assert.Equal(0, stream.Position);
		Assert.Equal(data1.Count + data2.Count, stream.Length);
		using ChainableMemoryBlock? detachedBuffer = await stream.DetachBufferAsync();
		Assert.Equal(data1.Concat(data2), detachedBuffer!.GetChainData());
	}

	#endregion
}
