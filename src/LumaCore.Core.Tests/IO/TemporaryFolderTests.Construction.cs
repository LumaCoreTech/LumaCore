// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;

using Xunit;

namespace LumaCore.Core.Tests.IO;

public sealed partial class TemporaryFolderTests
{
	#region TemporaryFolder(string?, string?) — standalone mode

	/// <summary>
	/// Test data for <see cref="Constructor_Standalone_CreatesFolderWithExpectedNamePattern"/>. Each row specifies a
	/// prefix value and the expected naming outcome.
	/// </summary>
	public static TheoryData<string, string?, string?> StandalonePrefixTestData => new()
	{
		// scenario, prefix, expectedPrefixInName (null = GUID-only)
		{ "null prefix", null, null },                 // null → GUID-only folder name
		{ "empty prefix", "", null },                  // empty → GUID-only folder name
		{ "whitespace prefix", " ", null },            // whitespace → treated as empty → GUID-only
		{ "valid prefix", "test-files", "test-files" } // valid → "test-files-{GUID}" pattern
	};

	/// <summary>
	/// Verifies that the standalone constructor creates a folder on disk whose name follows the expected pattern
	/// depending on the <paramref name="prefix"/> value.
	/// </summary>
	/// <param name="scenario">A human-readable label for the test case.</param>
	/// <param name="prefix">The prefix to pass to the constructor.</param>
	/// <param name="expectedPrefixInName">
	/// The prefix expected at the start of the folder name, or <see langword="null"/> if the name should be a
	/// GUID only.
	/// </param>
	[Theory]
	[MemberData(nameof(StandalonePrefixTestData))]
	public void Constructor_Standalone_CreatesFolderWithExpectedNamePattern(
		string  scenario,
		string? prefix,
		string? expectedPrefixInName)
	{
		_ = scenario;

		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			// Act
			var sut = new TemporaryFolder(prefix: prefix, basePath: basePath);
			try
			{
				// Assert
				Assert.True(Directory.Exists(sut.Path));
				Assert.StartsWith(basePath, sut.Path);
				string folderName = Path.GetFileName(sut.Path);
				AssertFolderNamePattern(folderName, expectedPrefixInName);
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
	/// Verifies that the standalone constructor uses <see cref="Path.GetTempPath()"/> as the parent directory when
	/// no base path is specified.
	/// </summary>
	[Fact]
	public void Constructor_StandaloneDefaultBasePath_CreatesFolderUnderTempPath()
	{
		// Arrange + Act
		var sut = new TemporaryFolder();
		try
		{
			// Assert
			string expectedParent = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
			string actualParent = Path.GetDirectoryName(sut.Path)!;
			Assert.Equal(expectedParent, actualParent);
			Assert.True(Directory.Exists(sut.Path));
		}
		finally
		{
			sut.Dispose();
		}
	}

	/// <summary>
	/// Verifies that the standalone constructor creates the folder inside the specified custom base path.
	/// </summary>
	[Fact]
	public void Constructor_StandaloneCustomBasePath_CreatesFolderUnderSpecifiedDirectory()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			// Act
			var sut = new TemporaryFolder(basePath: basePath);
			try
			{
				// Assert
				Assert.Equal(basePath, Path.GetDirectoryName(sut.Path));
				Assert.True(Directory.Exists(sut.Path));
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

	#endregion

	#region TemporaryFolder(TemporaryFolderManager, string, string?) — managed mode

	/// <summary>
	/// Verifies that the managed constructor creates both the folder and an adjacent <c>.lock</c> file.
	/// </summary>
	[Fact]
	public void Constructor_Managed_CreatesFolderAndLockFile()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager manager = CreateManagerForTesting(basePath);

			// Act
			var sut = new TemporaryFolder(manager, basePath, prefix: null);
			try
			{
				// Assert
				Assert.True(Directory.Exists(sut.Path));
				Assert.True(File.Exists(sut.Path + ".lock"));
				string folderName = Path.GetFileName(sut.Path);
				AssertFolderNamePattern(folderName, expectedPrefix: null);
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
	/// Verifies that the managed constructor includes the prefix in the folder name and creates a <c>.lock</c> file.
	/// </summary>
	[Fact]
	public void Constructor_ManagedWithPrefix_CreatesFolderWithPrefixAndLockFile()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager manager = CreateManagerForTesting(basePath);

			// Act
			var sut = new TemporaryFolder(manager, basePath, prefix: "export");
			try
			{
				// Assert
				Assert.True(Directory.Exists(sut.Path));
				Assert.True(File.Exists(sut.Path + ".lock"));
				string folderName = Path.GetFileName(sut.Path);
				AssertFolderNamePattern(folderName, expectedPrefix: "export");
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
	/// Verifies that the lock file created by the managed constructor is held with an exclusive lock, preventing
	/// other processes from opening it.
	/// </summary>
	[Fact]
	public void Constructor_Managed_LockFileIsExclusivelyHeld()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager manager = CreateManagerForTesting(basePath);
			var sut = new TemporaryFolder(manager, basePath, prefix: null);
			try
			{
				string lockFilePath = sut.Path + ".lock";

				// Act + Assert — attempt to open the lock file with exclusive access (FileShare.None).
				// Since the managed constructor already holds it exclusively, this second open must
				// fail with IOException — proving the lock is actively held and would prevent
				// CleanupOrphanedFolders() from treating this folder as orphaned.
				Assert.Throws<IOException>(() =>
				{
					using FileStream stream = File.Open(lockFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
				});
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

	#endregion
}
