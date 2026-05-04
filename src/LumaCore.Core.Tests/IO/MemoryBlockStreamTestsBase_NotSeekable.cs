// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Core.IO;

/// <summary>
/// Base class for unit tests targeting the <see cref="MemoryBlockStream"/> class.
/// The stream instance is expected to be not seekable.
/// </summary>
public abstract class MemoryBlockStreamTestsBase_NotSeekable : MemoryBlockStreamTestsBase
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MemoryBlockStreamTestsBase_NotSeekable"/> class.
	/// </summary>
	/// <param name="synchronized"><see langword="true"/> if the stream is synchronized; otherwise <see langword="false"/>.</param>
	/// <param name="usePool"><see langword="true"/> if the stream uses buffer pooling; otherwise <see langword="false"/>.</param>
	protected MemoryBlockStreamTestsBase_NotSeekable(bool synchronized, bool usePool) : base(synchronized, usePool) { }

	/// <summary>
	/// Gets a value indicating whether the stream can seek.
	/// </summary>
	protected override bool StreamCanSeek => false;

	#region SetLength()

	/// <summary>
	/// Checks whether <see cref="MemoryBlockStream.SetLength"/> throws <see cref="NotSupportedException"/> as the stream does
	/// not support seeking.
	/// </summary>
	[Fact]
	public void SetLength()
	{
		MemoryBlockStream stream = CreateStreamToTest();
		Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
	}

	#endregion

	#region Seek()

	/// <summary>
	/// Checks whether <see cref="MemoryBlockStream.Seek"/> throws <see cref="NotSupportedException"/> as the stream does not
	/// support seeking.
	/// </summary>
	[Fact]
	public void Seek()
	{
		MemoryBlockStream stream = CreateStreamToTest();
		Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
	}

	#endregion
}
