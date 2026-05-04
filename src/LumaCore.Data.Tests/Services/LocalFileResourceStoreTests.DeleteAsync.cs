// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LocalFileResourceStoreTests
{
	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.DeleteAsync"/> removes an existing file and reports
	/// <see langword="true"/>. This is the contract-honouring path that the audit identified as previously
	/// always reporting <see langword="true"/> regardless of whether a file existed.
	/// </summary>
	[Fact]
	public async Task DeleteAsync_WhenFileExists_DeletesFileAndReturnsTrue()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		string absolute = Path.Combine(root.Path, "victim.bin");
		await File.WriteAllTextAsync(absolute, "delete me");

		// Act
		bool result = await sut.DeleteAsync("victim.bin");

		// Assert
		Assert.True(result);
		Assert.False(File.Exists(absolute));
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.DeleteAsync"/> returns <see langword="false"/> when
	/// the file does not exist (storage root is present, file is not). This is the regression test for the
	/// audit finding — the previous implementation incorrectly returned <see langword="true"/> here.
	/// </summary>
	[Fact]
	public async Task DeleteAsync_WhenFileDoesNotExist_ReturnsFalse()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act
		bool result = await sut.DeleteAsync("ghost.bin");

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.DeleteAsync"/> returns <see langword="false"/> when
	/// the parent directory has been removed (e.g. by a concurrent operation), keeping the operation
	/// idempotent for GC retry safety.
	/// </summary>
	[Fact]
	public async Task DeleteAsync_WhenParentDirectoryMissing_ReturnsFalse()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		// Note: we never create the "absent" subdirectory, so File.Delete will throw
		// DirectoryNotFoundException, which the implementation must convert to a "false" result.

		// Act
		bool result = await sut.DeleteAsync("absent/file.bin");

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.DeleteAsync"/> rejects whitespace-only storage paths.
	/// </summary>
	[Fact]
	public async Task DeleteAsync_WhenStoragePathIsWhitespace_ThrowsArgumentException()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.DeleteAsync("   "));
		Assert.Equal("storagePath", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.DeleteAsync"/> is idempotent — a second invocation on
	/// the same path returns <see langword="false"/> without throwing.
	/// </summary>
	[Fact]
	public async Task DeleteAsync_WhenCalledTwice_ReturnsTrueThenFalse()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		await File.WriteAllTextAsync(Path.Combine(root.Path, "twice.bin"), "x");

		// Act
		bool first = await sut.DeleteAsync("twice.bin");
		bool second = await sut.DeleteAsync("twice.bin");

		// Assert
		Assert.True(first);
		Assert.False(second);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.DeleteAsync"/> rejects a path-traversal attempt.
	/// </summary>
	/// <param name="escapingPath">The malicious storage path that escapes the storage root.</param>
	[Theory]
	[MemberData(nameof(EscapingPaths))]
	public async Task DeleteAsync_WhenPathEscapesRoot_ThrowsInvalidOperationException(string escapingPath)
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DeleteAsync(
			         escapingPath,
			         CancellationToken.None));

		// Assert: exact message including the absolute path the SUT computed.
		string expectedAbsolute = Path.GetFullPath(
			Path.Combine(
				Path.GetFullPath(root.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				+ Path.DirectorySeparatorChar,
				escapingPath));
		Assert.Equal(
			$"Path traversal detected: resolved path '{expectedAbsolute}' escapes storage root.",
			ex.Message);
	}
}
