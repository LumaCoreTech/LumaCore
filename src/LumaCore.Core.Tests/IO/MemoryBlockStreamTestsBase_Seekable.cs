// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

using Xunit;

// ReSharper disable UseAwaitUsing

namespace LumaCore.Core.IO;

/// <summary>
/// Base class for unit tests targeting the <see cref="MemoryBlockStream"/> class.
/// The stream instance is expected to be seekable.
/// </summary>
public abstract class MemoryBlockStreamTestsBase_Seekable : MemoryBlockStreamTestsBase
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryBlockStreamTestsBase_Seekable"/> class.
	/// </summary>
	/// <param name="synchronized"><see langword="true"/> if the stream is synchronized; otherwise <see langword="false"/>.</param>
	/// <param name="usePool"><see langword="true"/> if the stream uses buffer pooling; otherwise <see langword="false"/>.</param>
	protected MemoryBlockStreamTestsBase_Seekable(bool synchronized, bool usePool) : base(synchronized, usePool) { }

	/// <summary>
	/// Gets a value indicating whether the stream can seek.
	/// </summary>
	protected override bool StreamCanSeek => true;

	#region SetLength()

	/// <summary>
	/// Tests setting the length of the stream using <see cref="MemoryBlockStream.SetLength"/>.
	/// </summary>
	[Theory]
	[InlineData(0, 0, 0)]            // empty stream, remains as is
	[InlineData(0, 1, 0)]            // empty stream, enlarge to 1 byte (zero), position remains unmodified
	[InlineData(1, 1, 0)]            // stream with 1 byte, stream length remains as is, position remains unmodified
	[InlineData(1, 1, 1)]            // stream with 1 byte, stream length remains as is, position remains unmodified
	[InlineData(1, 0, 0)]            // stream with 1 byte, stream is cleared, position remains unmodified
	[InlineData(1, 0, 1)]            // stream with 1 byte, stream is cleared, position is adjusted
	[InlineData(1, 2, 0)]            // stream with 1 byte, enlarge to 2 bytes (zero), position remains unmodified
	[InlineData(TestDataSize, 0, 0)] // huge stream with multiple blocks, stream is cleared, position remains unmodified
	[InlineData(TestDataSize, 0, 1)] // huge stream with multiple blocks, stream is cleared, position is adjusted
	[InlineData(
		TestDataSize,
		0,
		TestDataSize)] // huge stream with multiple blocks, stream is cleared, position is adjusted
	[InlineData(
		TestDataSize,
		1,
		0)] // huge stream with multiple blocks, stream is shrunk to 1 byte, position remains unmodified
	[InlineData(
		TestDataSize,
		1,
		1)] // huge stream with multiple blocks, stream is shrunk to 1 byte, position remains unmodified
	[InlineData(
		TestDataSize,
		1,
		2)] // huge stream with multiple blocks, stream is shrunk to 1 byte, position is adjusted
	[InlineData(
		TestDataSize,
		1,
		TestDataSize)] // huge stream with multiple blocks, stream is shrunk to 1 byte, position is adjusted
	[InlineData(
		TestDataSize,
		TestDataSize + 1,
		0)] // huge stream with multiple blocks, stream is enlarged by 1 byte (zero), position remains unmodified
	[InlineData(
		TestDataSize,
		TestDataSize + 1,
		1)] // huge stream with multiple blocks, stream is enlarged by 1 byte (zero), position remains unmodified
	[InlineData(
		TestDataSize,
		TestDataSize + 1,
		TestDataSize)] // huge stream with multiple blocks, stream is enlarged by 1 byte (zero), position remains unmodified
	public void SetLength(int oldLength, int newLength, int initialPosition)
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();

		// populate the stream with some initial data, if necessary
		var expectedData = new List<byte>();
		if (oldLength > 0)
		{
			// generate some test data and attach it to the stream
			ChainableMemoryBlock chain = GetRandomTestDataChain(oldLength, StreamMemoryBlockSize, out expectedData)!;
			stream.AttachBuffer(chain);
		}

		// seek stream to the initial position
		stream.Position = initialPosition;
		Assert.Equal(initialPosition, stream.Position);

		// set new length
		stream.SetLength(newLength);

		// the stream should reflect the new length now
		Assert.Equal(newLength, stream.Length);

		// the position should have changed only, if it had been outside the stream bounds now
		long expectedPosition = Math.Min(initialPosition, newLength);
		Assert.Equal(expectedPosition, stream.Position);

		// adjust expected data
		// (shrink buffer, if the stream gets shorter, add zero bytes, if the stream gets longer)
		if (newLength < oldLength) expectedData.RemoveRange(newLength, expectedData.Count - newLength);
		else expectedData.AddRange(Enumerable.Repeat((byte)0, newLength - oldLength));

		// detach the underlying buffer and check its state
		using ChainableMemoryBlock firstBlock = stream.DetachBuffer()!;
		if (newLength > 0)
		{
			byte[] buffer = firstBlock.GetChainData();
			Assert.Equal(expectedData, buffer);
		}
		else
		{
			Assert.Null(firstBlock);
		}
	}

	#endregion

	#region Seek()

	/// <summary>
	/// Test data for seeking tests.
	/// </summary>
	public static TheoryData<int, int, SeekOrigin, int, int> Seek_TestData
	{
		get
		{
			var origins = new[] { SeekOrigin.Begin, SeekOrigin.Current, SeekOrigin.End };

			// HashSet of value tuples assists with de-duplicating test cases produced by the nested loops below.
			var records =
				new HashSet<(int InitialLength, int InitialPosition, SeekOrigin Origin, int Offset, int Expected
					)>();

			// ----------------------------------------------------------------------------------------------------------------
			// empty string, don't change position
			// ----------------------------------------------------------------------------------------------------------------
			foreach (SeekOrigin origin in origins)
			{
				records.Add((0, 0, origin, 0, 0));
			}

			// ----------------------------------------------------------------------------------------------------------------
			// stream with a single block (1 byte only) and a huge stream with multiple blocks
			// (check border positions)
			// ----------------------------------------------------------------------------------------------------------------
			{
				const int length = 1;
				foreach (int initialPosition in new[] { 0, 1 })
				{
					// seek from the beginning and the end of the stream
					foreach (int offset in new[] { 0, 1 })
					{
						records.Add((length, initialPosition, SeekOrigin.Begin, offset, offset));
						records.Add((length, initialPosition, SeekOrigin.End, -offset, length - offset));
					}

					// seek with 0 bytes => do not move stream position
					records.Add((length, initialPosition, SeekOrigin.Current, 0, initialPosition));
				}
				// seek forward from the current position (1 byte)
				records.Add((length, 0, SeekOrigin.Current, 1, 1));

				// seek backwards from the current position
				records.Add((length, 1, SeekOrigin.Current, -1, 0));
			}

			// ----------------------------------------------------------------------------------------------------------------
			// stream with a single block (10 bytes) and a huge stream with multiple blocks
			// (check border positions)
			// ----------------------------------------------------------------------------------------------------------------

			foreach (int length in new[] { 10, TestDataSize })
			{
				foreach (int initialPosition in new[] { 0, 1, length / 2, length - 1, length })
				{
					// seek from the beginning and the end of the stream
					foreach (int offset in new[] { 0, 1, length - 1, length })
					{
						records.Add((length, initialPosition, SeekOrigin.Begin, offset, offset));
						records.Add((length, initialPosition, SeekOrigin.End, -offset, length - offset));
					}

					// seek with 0 bytes => do not move stream position
					records.Add((length, initialPosition, SeekOrigin.Current, 0, initialPosition));

					// seek forward from the current position
					foreach (int offset in new[] { 1, length - initialPosition - 1, length - initialPosition })
					{
						if (initialPosition + offset <= length)
						{
							records.Add(
								(length, initialPosition, SeekOrigin.Current, offset, initialPosition + offset));
						}
					}

					// seek backwards from the current position
					foreach (int offset in new[] { 1, initialPosition - 1, initialPosition })
					{
						if (initialPosition - offset >= 0)
						{
							records.Add(
								(length, initialPosition, SeekOrigin.Current, -offset, initialPosition - offset));
						}
					}
				}
			}

			// transfer the de-duplicated records into a strongly typed TheoryData set
			var data = new TheoryData<int, int, SeekOrigin, int, int>();
			foreach ((int InitialLength, int InitialPosition, SeekOrigin Origin, int Offset, int Expected) record in
			         records)
			{
				data.Add(
					record.InitialLength,
					record.InitialPosition,
					record.Origin,
					record.Offset,
					record.Expected);
			}

			return data;
		}
	}

	/// <summary>
	/// Prepares a stream and changes the current stream position using <see cref="MemoryBlockStream.Seek"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(Seek_TestData))]
	public void Seek(
		int        initialLength,
		int        initialPosition,
		SeekOrigin seekOrigin,
		int        seekOffset,
		int        expectedPosition)
	{
		// create a new stream
		using MemoryBlockStream stream = CreateStreamToTest();

		// generate some test data and attach it to the stream
		ChainableMemoryBlock chain = GetRandomTestDataChain(
			initialLength,
			StreamMemoryBlockSize,
			out List<byte> expectedData)!;
		stream.AttachBuffer(chain);

		// change position (already involves seeking)
		stream.Position = initialPosition;
		Assert.Equal(initialPosition, stream.Position);

		// seek to the specified position
		stream.Seek(seekOffset, seekOrigin);

		// the position of the stream should now be as expected
		Assert.Equal(expectedPosition, stream.Position);

		// the length should not have been changed
		Assert.Equal(expectedData.Count, stream.Length);
	}

	#endregion

	#region InjectBufferAtCurrentPosition()

	/// <summary>
	/// Test data for the <see cref="InjectBufferAtCurrentPosition"/> test method.
	/// </summary>
	public static TheoryData<int, int, int, int, int> InjectBufferAtCurrentPosition_TestData
	{
		get
		{
			const int streamBlockSize = 1000;
			var data = new TheoryData<int, int, int, int, int>();

			foreach (int blocksToInsertSize in new[]
			         {
				         streamBlockSize / 4, streamBlockSize / 3, streamBlockSize / 2,
				         streamBlockSize, 3 * streamBlockSize / 2, 2 * streamBlockSize
			         })
			foreach (int blocksToInsertCount in new[] { 1, 2 })
			foreach (int initialLength in new[]
			         {
				         0, 1, 500, 999, 1000, 1001, 1500, 1999, 2000,
				         2001, 2500, 2999, 3000, 3001, 3500, 3999, 4000
			         })
			{
				// inject at the beginning of the stream
				data.Add(streamBlockSize, initialLength, 0, blocksToInsertCount, blocksToInsertSize);

				// inject in the middle of the stream
				if (initialLength > 2)
				{
					data.Add(
						streamBlockSize,
						initialLength,
						initialLength / 2,
						blocksToInsertCount,
						blocksToInsertSize);
				}

				// inject at the end of the stream
				// (it may be in the middle of the last block, so the injected block fits into the remaining space of the block)
				if (initialLength > 1)
				{
					data.Add(
						streamBlockSize,
						initialLength,
						initialLength - 1,
						blocksToInsertCount,
						blocksToInsertSize);
				}
			}

			return data;
		}
	}

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPosition"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(InjectBufferAtCurrentPosition_TestData))]
	public void InjectBufferAtCurrentPosition_Insert_KeepPosition(
		int streamBlockSize,
		int initialLength,
		int position,
		int blockToInsertCount,
		int blockToInsertSize)
	{
		const bool overwrite = false;
		const bool advancePosition = false;
		InjectBufferAtCurrentPosition(
			streamBlockSize,
			initialLength,
			position,
			overwrite,
			advancePosition,
			blockToInsertCount,
			blockToInsertSize);
	}

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPosition"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(InjectBufferAtCurrentPosition_TestData))]
	public void InjectBufferAtCurrentPosition_Insert_AdvancePosition(
		int streamBlockSize,
		int initialLength,
		int position,
		int blockToInsertCount,
		int blockToInsertSize)
	{
		const bool overwrite = false;
		const bool advancePosition = true;
		InjectBufferAtCurrentPosition(
			streamBlockSize,
			initialLength,
			position,
			overwrite,
			advancePosition,
			blockToInsertCount,
			blockToInsertSize);
	}

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPosition"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(InjectBufferAtCurrentPosition_TestData))]
	public void InjectBufferAtCurrentPosition_Overwrite_KeepPosition(
		int streamBlockSize,
		int initialLength,
		int position,
		int blockToInsertCount,
		int blockToInsertSize)
	{
		const bool overwrite = true;
		const bool advancePosition = false;
		InjectBufferAtCurrentPosition(
			streamBlockSize,
			initialLength,
			position,
			overwrite,
			advancePosition,
			blockToInsertCount,
			blockToInsertSize);
	}

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPosition"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(InjectBufferAtCurrentPosition_TestData))]
	public void InjectBufferAtCurrentPosition_Overwrite_AdvancePosition(
		int streamBlockSize,
		int initialLength,
		int position,
		int blockToInsertCount,
		int blockToInsertSize)
	{
		const bool overwrite = true;
		const bool advancePosition = true;
		InjectBufferAtCurrentPosition(
			streamBlockSize,
			initialLength,
			position,
			overwrite,
			advancePosition,
			blockToInsertCount,
			blockToInsertSize);
	}

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPosition"/>.
	/// </summary>
	private void InjectBufferAtCurrentPosition(
		int  streamBlockSize,
		int  initialLength,
		int  position,
		bool overwrite,
		bool advancePosition,
		int  blockToInsertCount,
		int  blockToInsertSize)
	{
		Debug.Assert(streamBlockSize > 0);
		Debug.Assert(blockToInsertCount > 0);
		Debug.Assert(blockToInsertSize > 0);

		using MemoryBlockStream stream = CreateStreamToTest(streamBlockSize);

		// generate some test data and attach it to the stream
		ChainableMemoryBlock chain = GetRandomTestDataChain(
			initialLength,
			streamBlockSize,
			out List<byte> initialStreamData)!;
		stream.AttachBuffer(chain);

		// set position of the stream to inject the blocks into
		stream.Position = position;

		// inject a chain of blocks into the existing chain of blocks backing the stream
		ChainableMemoryBlock chainToInsert = GetRandomTestDataChain(
			blockToInsertCount * blockToInsertSize,
			blockToInsertSize,
			out List<byte> dataToInsert)!;
		stream.InjectBufferAtCurrentPosition(
			chainToInsert,
			overwrite,
			advancePosition);

		// check whether the stream length reflects the change
		long expectedLength = overwrite
			                      ? Math.Max(initialStreamData.Count, position + dataToInsert.Count)
			                      : initialStreamData.Count + dataToInsert.Count;
		Assert.Equal(expectedLength, stream.Length);

		// check whether the stream position has changed as expected
		long expectedPosition = advancePosition
			                        ? position + dataToInsert.Count
			                        : position;
		Assert.Equal(expectedPosition, stream.Position);

		// check whether the data in the stream has changed as expected
		var expectedData = new List<byte>();
		expectedData.AddRange(initialStreamData.Take(position));
		expectedData.AddRange(dataToInsert);
		expectedData.AddRange(initialStreamData.Skip(position));
		if (overwrite)
		{
			expectedData.RemoveRange(
				position + dataToInsert.Count,
				Math.Min(dataToInsert.Count, expectedData.Count - position - dataToInsert.Count));
		}
		using ChainableMemoryBlock detachedBuffer = stream.DetachBuffer()!;
		byte[] data = detachedBuffer.GetChainData();
		Assert.Equal(expectedData.Count, data.Length);
		Assert.Equal(expectedData, data);
	}

	#endregion

	#region InjectBufferAtCurrentPositionAsync()

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPositionAsync"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(InjectBufferAtCurrentPosition_TestData))]
	public Task InjectBufferAtCurrentPositionAsync_Insert_KeepPosition(
		int streamBlockSize,
		int initialLength,
		int position,
		int blockToInsertCount,
		int blockToInsertSize)
	{
		const bool overwrite = false;
		const bool advancePosition = false;
		return InjectBufferAtCurrentPositionAsync(
			streamBlockSize,
			initialLength,
			position,
			overwrite,
			advancePosition,
			blockToInsertCount,
			blockToInsertSize);
	}

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPositionAsync"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(InjectBufferAtCurrentPosition_TestData))]
	public Task InjectBufferAtCurrentPositionAsync_Insert_AdvancePosition(
		int streamBlockSize,
		int initialLength,
		int position,
		int blockToInsertCount,
		int blockToInsertSize)
	{
		const bool overwrite = false;
		const bool advancePosition = true;
		return InjectBufferAtCurrentPositionAsync(
			streamBlockSize,
			initialLength,
			position,
			overwrite,
			advancePosition,
			blockToInsertCount,
			blockToInsertSize);
	}

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPositionAsync"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(InjectBufferAtCurrentPosition_TestData))]
	public Task InjectBufferAtCurrentPositionAsync_Overwrite_KeepPosition(
		int streamBlockSize,
		int initialLength,
		int position,
		int blockToInsertCount,
		int blockToInsertSize)
	{
		const bool overwrite = true;
		const bool advancePosition = false;
		return InjectBufferAtCurrentPositionAsync(
			streamBlockSize,
			initialLength,
			position,
			overwrite,
			advancePosition,
			blockToInsertCount,
			blockToInsertSize);
	}

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPositionAsync"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(InjectBufferAtCurrentPosition_TestData))]
	public Task InjectBufferAtCurrentPositionAsync_Overwrite_AdvancePosition(
		int streamBlockSize,
		int initialLength,
		int position,
		int blockToInsertCount,
		int blockToInsertSize)
	{
		const bool overwrite = true;
		const bool advancePosition = true;
		return InjectBufferAtCurrentPositionAsync(
			streamBlockSize,
			initialLength,
			position,
			overwrite,
			advancePosition,
			blockToInsertCount,
			blockToInsertSize);
	}

	/// <summary>
	/// Injects a chain of memory blocks at the current position of the stream using
	/// <see cref="MemoryBlockStream.InjectBufferAtCurrentPositionAsync"/>.
	/// </summary>
	private async Task InjectBufferAtCurrentPositionAsync(
		int  streamBlockSize,
		int  initialLength,
		int  position,
		bool overwrite,
		bool advancePosition,
		int  blockToInsertCount,
		int  blockToInsertSize)
	{
		Debug.Assert(streamBlockSize > 0);
		Debug.Assert(blockToInsertCount > 0);
		Debug.Assert(blockToInsertSize > 0);

		using MemoryBlockStream stream = CreateStreamToTest(streamBlockSize);

		// generate some test data and attach it to the stream
		ChainableMemoryBlock chain = GetRandomTestDataChain(
			initialLength,
			streamBlockSize,
			out List<byte> initialStreamData)!;
		await stream.AttachBufferAsync(chain).ConfigureAwait(false);

		// set position of the stream to inject the blocks into
		stream.Position = position;

		// inject a chain of blocks into the existing chain of blocks backing the stream
		ChainableMemoryBlock chainToInsert = GetRandomTestDataChain(
			blockToInsertCount * blockToInsertSize,
			blockToInsertSize,
			out List<byte> dataToInsert)!;
		await stream.InjectBufferAtCurrentPositionAsync(
				chainToInsert,
				overwrite,
				advancePosition,
				CancellationToken.None)
			.ConfigureAwait(false);

		// check whether the stream length reflects the change
		long expectedLength = overwrite
			                      ? Math.Max(initialStreamData.Count, position + dataToInsert.Count)
			                      : initialStreamData.Count + dataToInsert.Count;
		Assert.Equal(expectedLength, stream.Length);

		// check whether the stream position has changed as expected
		long expectedPosition = advancePosition
			                        ? position + dataToInsert.Count
			                        : position;
		Assert.Equal(expectedPosition, stream.Position);

		// check whether the data in the stream has changed as expected
		var expectedData = new List<byte>();
		expectedData.AddRange(initialStreamData.Take(position));
		expectedData.AddRange(dataToInsert);
		expectedData.AddRange(initialStreamData.Skip(position));
		if (overwrite)
		{
			expectedData.RemoveRange(
				position + dataToInsert.Count,
				Math.Min(dataToInsert.Count, expectedData.Count - position - dataToInsert.Count));
		}
		using ChainableMemoryBlock detachedBuffer = (await stream
			                                             .DetachBufferAsync(CancellationToken.None)
			                                             .ConfigureAwait(false))!;
		byte[] data = detachedBuffer.GetChainData();
		Assert.Equal(expectedData.Count, data.Length);
		Assert.Equal(expectedData, data);
	}

	#endregion
}
