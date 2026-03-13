// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;

using Xunit;

namespace LumaCore.Core.Tests.IO;

public sealed partial class TemporaryFolderTests
{
	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.CreateFile"/> creates an empty file on disk and
	/// returns the expected absolute path.
	/// </summary>
	[Fact]
	public void CreateFile_ValidFileName_CreatesFileAndReturnsPath()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			try
			{
				// Act
				string result = sut.CreateFile("data.db");

				// Assert
				string expectedPath = Path.Combine(sut.Path, "data.db");
				Assert.Equal(expectedPath, result);
				Assert.True(File.Exists(result));
				Assert.Equal(0L, new FileInfo(result).Length);
			}
			finally
			{
				sut.Dispose();
			}
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="TemporaryFolder.CreateFile"/> twice with the same file name succeeds
	/// (overwrites the file) without throwing.
	/// </summary>
	[Fact]
	public void CreateFile_CalledTwiceWithSameFileName_Succeeds()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			try
			{
				sut.CreateFile("data.db");

				// Act
				string result = sut.CreateFile("data.db");

				// Assert
				Assert.True(File.Exists(result));
			}
			finally
			{
				sut.Dispose();
			}
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.CreateFile"/> throws <see cref="ObjectDisposedException"/>
	/// when the folder has already been disposed.
	/// </summary>
	[Fact]
	public void CreateFile_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			sut.Dispose();

			// Act + Assert
			var ex = Assert.Throws<ObjectDisposedException>(() => sut.CreateFile("file.txt"));
			Assert.Contains(nameof(TemporaryFolder), ex.ObjectName);
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.CreateFile"/> throws <see cref="ArgumentNullException"/>
	/// when the file name is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void CreateFile_NullFileName_ThrowsArgumentNullException()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			try
			{
				// Act + Assert
				var ex = Assert.Throws<ArgumentNullException>(() => sut.CreateFile(null!));
				Assert.Equal("fileName", ex.ParamName);
			}
			finally
			{
				sut.Dispose();
			}
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.CreateFile"/> throws <see cref="ArgumentException"/>
	/// when the file name is empty or whitespace-only.
	/// </summary>
	/// <param name="fileName">The invalid file name to test.</param>
	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void CreateFile_EmptyOrWhitespaceFileName_ThrowsArgumentException(string fileName)
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			try
			{
				// Act + Assert
				var ex = Assert.Throws<ArgumentException>(() => sut.CreateFile(fileName));
				Assert.Equal("fileName", ex.ParamName);
			}
			finally
			{
				sut.Dispose();
			}
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}
}
