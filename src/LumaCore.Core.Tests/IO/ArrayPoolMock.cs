// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;

namespace LumaCore.Core.IO;

/// <summary>
/// Test double for <see cref="ArrayPool{T}"/> of <see cref="byte"/> that tracks rent/return calls so tests can
/// assert that pooled buffers are properly returned by the consumer.
/// </summary>
/// <remarks>
/// Buffers are allocated on the heap (no real pooling is performed). The mock only tracks how many buffers are
/// currently rented and verifies that returned buffers were previously rented from this instance.
/// </remarks>
sealed class ArrayPoolMock : ArrayPool<byte>
{
	private readonly HashSet<byte[]> mRentedBuffers = [];

	/// <summary>
	/// Gets the number of buffers that are currently rented from this pool (i.e. not yet returned).
	/// </summary>
	public int RentedBufferCount
	{
		get
		{
			lock (mRentedBuffers) return mRentedBuffers.Count;
		}
	}

	/// <inheritdoc/>
	public override byte[] Rent(int minimumLength)
	{
		byte[] buffer = new byte[minimumLength];
		lock (mRentedBuffers) mRentedBuffers.Add(buffer);
		return buffer;
	}

	/// <inheritdoc/>
	public override void Return(byte[] array, bool clearArray = false)
	{
		lock (mRentedBuffers)
		{
			if (!mRentedBuffers.Remove(array))
				throw new InvalidOperationException("The specified buffer was not rented from this pool.");
		}
	}
}
