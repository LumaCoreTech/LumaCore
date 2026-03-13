// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;

using Xunit;

namespace LumaCore.Core.Tests.IO;

/// <summary>
/// Unit tests for <see cref="TemporaryFolder"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify both standalone and managed modes of the temporary folder, including construction,
///     file path operations, file creation, and disposal behavior.
///     </para>
///     <para>
///     Test files are organized by public API member:
///     <list type="bullet">
///         <item>
///         <c>TemporaryFolderTests.Construction.cs</c> — Constructor tests for standalone and managed modes
///         </item>
///         <item>
///         <c>TemporaryFolderTests.CreateFile.cs</c> — <see cref="TemporaryFolder.CreateFile"/> method tests
///         </item>
///         <item>
///         <c>TemporaryFolderTests.Dispose.cs</c> — <see cref="TemporaryFolder.Dispose"/> method tests
///         </item>
///         <item><c>TemporaryFolderTests.Helpers.cs</c> — Shared test helpers and assertion utilities</item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "IO")]
public sealed partial class TemporaryFolderTests
{
	#region GetFilePath()

	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.GetFilePath"/> returns the combined path of the folder and the
	/// specified file name.
	/// </summary>
	[Fact]
	public void GetFilePath_ValidFileName_ReturnsCombinedPath()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			try
			{
				// Act
				string result = sut.GetFilePath("test.db");

				// Assert
				Assert.Equal(Path.Combine(sut.Path, "test.db"), result);
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
	/// Verifies that <see cref="TemporaryFolder.GetFilePath"/> throws <see cref="ArgumentNullException"/> when the
	/// file name is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void GetFilePath_NullFileName_ThrowsArgumentNullException()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			try
			{
				// Act + Assert
				var ex = Assert.Throws<ArgumentNullException>(() => sut.GetFilePath(null!));
				Assert.Equal("fileName", ex.ParamName);
				Assert.Equal("Value cannot be null. (Parameter 'fileName')", ex.Message);
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
	/// Verifies that <see cref="TemporaryFolder.GetFilePath"/> throws <see cref="ArgumentException"/> when the
	/// file name is empty or whitespace-only.
	/// </summary>
	/// <param name="fileName">The invalid file name to test.</param>
	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("  ")]
	public void GetFilePath_EmptyOrWhitespaceFileName_ThrowsArgumentException(string fileName)
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			try
			{
				// Act + Assert
				var ex = Assert.Throws<ArgumentException>(() => sut.GetFilePath(fileName));
				Assert.Equal("fileName", ex.ParamName);
				Assert.Equal(
					"The value cannot be an empty string or composed entirely of whitespace. (Parameter 'fileName')",
					ex.Message);
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
