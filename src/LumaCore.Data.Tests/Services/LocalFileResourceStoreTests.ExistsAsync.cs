// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LocalFileResourceStoreTests
{
	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.ExistsAsync"/> reports <see langword="true"/> for an
	/// existing file.
	/// </summary>
	[Fact]
	public async Task ExistsAsync_WhenFileExists_ReturnsTrue()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		await File.WriteAllTextAsync(Path.Combine(root.Path, "here.bin"), "x");

		// Act
		bool result = await sut.ExistsAsync("here.bin");

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.ExistsAsync"/> reports <see langword="false"/> for a
	/// missing file.
	/// </summary>
	[Fact]
	public async Task ExistsAsync_WhenFileMissing_ReturnsFalse()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act
		bool result = await sut.ExistsAsync("ghost.bin");

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.ExistsAsync"/> rejects whitespace-only storage paths.
	/// </summary>
	[Fact]
	public async Task ExistsAsync_WhenStoragePathIsWhitespace_ThrowsArgumentException()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.ExistsAsync("   "));
		Assert.Equal("storagePath", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.ExistsAsync"/> observes cancellation before probing
	/// the filesystem, so a canceled caller does not pay for an unnecessary <see cref="File.Exists(string)"/>
	/// syscall.
	/// </summary>
	[Fact]
	public async Task ExistsAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ExistsAsync("here.bin", cts.Token));
		Assert.Equal(cts.Token, ex.CancellationToken);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.ExistsAsync"/> rejects a path-traversal attempt
	/// rather than silently reporting <see langword="false"/>.
	/// </summary>
	/// <param name="escapingPath">The malicious storage path that escapes the storage root.</param>
	[Theory]
	[MemberData(nameof(EscapingPaths))]
	public async Task ExistsAsync_WhenPathEscapesRoot_ThrowsInvalidOperationException(string escapingPath)
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExistsAsync(
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
