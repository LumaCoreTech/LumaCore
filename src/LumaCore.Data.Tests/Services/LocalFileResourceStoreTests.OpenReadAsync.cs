// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LocalFileResourceStoreTests
{
	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.OpenReadAsync"/> returns a readable stream positioned
	/// at the beginning of the file content.
	/// </summary>
	[Fact]
	public async Task OpenReadAsync_WhenFileExists_ReturnsReadableStreamWithContent()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		await File.WriteAllTextAsync(Path.Combine(root.Path, "readable.bin"), "open me");

		// Act
		await using Stream? stream = await sut.OpenReadAsync("readable.bin");

		// Assert
		Assert.NotNull(stream);
		using var reader = new StreamReader(stream);
		string content = await reader.ReadToEndAsync();
		Assert.Equal("open me", content);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.OpenReadAsync"/> returns <see langword="null"/> when
	/// the file is missing — without throwing — so the caller can serve a 404 cleanly.
	/// </summary>
	[Fact]
	public async Task OpenReadAsync_WhenFileDoesNotExist_ReturnsNull()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act
		Stream? stream = await sut.OpenReadAsync("ghost.bin");

		// Assert
		Assert.Null(stream);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.OpenReadAsync"/> returns <see langword="null"/> when
	/// the parent directory does not exist (a TOCTOU race against the GC sweep is mapped to "missing").
	/// </summary>
	[Fact]
	public async Task OpenReadAsync_WhenParentDirectoryMissing_ReturnsNull()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act
		Stream? stream = await sut.OpenReadAsync("absent/file.bin");

		// Assert
		Assert.Null(stream);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.OpenReadAsync"/> rejects whitespace-only storage paths.
	/// </summary>
	[Fact]
	public async Task OpenReadAsync_WhenStoragePathIsWhitespace_ThrowsArgumentException()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.OpenReadAsync("   "));
		Assert.Equal("storagePath", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.OpenReadAsync"/> observes cancellation before opening
	/// the file, so a canceled caller does not leak a <see cref="FileStream"/> that would have to be disposed.
	/// </summary>
	[Fact]
	public async Task OpenReadAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		await File.WriteAllTextAsync(Path.Combine(root.Path, "readable.bin"), "open me");
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act + Assert
		var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.OpenReadAsync(
			         "readable.bin",
			         cts.Token));
		Assert.Equal(cts.Token, ex.CancellationToken);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.OpenReadAsync"/> rejects a path-traversal attempt.
	/// </summary>
	/// <param name="escapingPath">The malicious storage path that escapes the storage root.</param>
	[Theory]
	[MemberData(nameof(EscapingPaths))]
	public async Task OpenReadAsync_WhenPathEscapesRoot_ThrowsInvalidOperationException(string escapingPath)
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenReadAsync(
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
