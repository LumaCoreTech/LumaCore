// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;

using Xunit;

namespace LumaCore.Core.Tests.IO;

public sealed partial class TemporaryFolderManagerTests
{
	/// <summary>
	/// Regex pattern matching a 32-character lowercase hexadecimal GUID string (format "N").
	/// </summary>
	private const string GuidPattern = "^[0-9a-f]{32}$";

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CreateFolder"/> without a prefix returns a folder whose name
	/// is a GUID and which resides under the manager's <see cref="TemporaryFolderManager.BasePath"/>.
	/// </summary>
	[Fact]
	public void CreateFolder_NoPrefix_ReturnsFolderInBasePath()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Act
			ITemporaryFolder folder = sut.CreateFolder();

			// Assert
			Assert.True(Directory.Exists(folder.Path));
			Assert.Equal(sut.BasePath, Path.GetDirectoryName(folder.Path));
			string folderName = Path.GetFileName(folder.Path);
			Assert.Matches(GuidPattern, folderName);
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CreateFolder"/> with a prefix returns a folder whose name
	/// starts with the prefix followed by a GUID.
	/// </summary>
	[Fact]
	public void CreateFolder_WithPrefix_ReturnsFolderWithPrefixedName()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Act
			ITemporaryFolder folder = sut.CreateFolder("export");

			// Assert
			Assert.True(Directory.Exists(folder.Path));
			string folderName = Path.GetFileName(folder.Path);
			Assert.StartsWith("export-", folderName);
			string guidPart = folderName["export-".Length..];
			Assert.Matches(GuidPattern, guidPart);
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CreateFolder"/> creates an adjacent <c>.lock</c> file for
	/// each managed folder.
	/// </summary>
	[Fact]
	public void CreateFolder_CreatesAdjacentLockFile()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Act
			ITemporaryFolder folder = sut.CreateFolder();

			// Assert
			Assert.True(File.Exists(folder.Path + ".lock"));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CreateFolder"/> can create multiple independent folders, each
	/// with a unique path and its own lock file.
	/// </summary>
	[Fact]
	public void CreateFolder_MultipleFolders_AllIndependentWithUniquePaths()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Act
			ITemporaryFolder folder1 = sut.CreateFolder("a");
			ITemporaryFolder folder2 = sut.CreateFolder("b");
			ITemporaryFolder folder3 = sut.CreateFolder("c");

			// Assert
			Assert.NotEqual(folder1.Path, folder2.Path);
			Assert.NotEqual(folder2.Path, folder3.Path);
			Assert.NotEqual(folder1.Path, folder3.Path);
			Assert.True(Directory.Exists(folder1.Path));
			Assert.True(Directory.Exists(folder2.Path));
			Assert.True(Directory.Exists(folder3.Path));
			Assert.True(File.Exists(folder1.Path + ".lock"));
			Assert.True(File.Exists(folder2.Path + ".lock"));
			Assert.True(File.Exists(folder3.Path + ".lock"));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CreateFolder"/> throws
	/// <see cref="ObjectDisposedException"/> when the manager has already been disposed.
	/// </summary>
	[Fact]
	public void CreateFolder_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);
			sut.Dispose();

			// Act + Assert
			var ex = Assert.Throws<ObjectDisposedException>(() => sut.CreateFolder());
			Assert.Contains(nameof(TemporaryFolderManager), ex.ObjectName);
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}
}
