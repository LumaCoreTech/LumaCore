// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LocalFileResourceStoreTests
{
	/// <summary>
	/// Verifies that the constructor accepts valid arguments and resolves the storage root to an absolute path.
	/// </summary>
	[Fact]
	public void Constructor_WhenArgumentsAreValid_CreatesInstance()
	{
		// Arrange
		using var root = new TempStorageRoot();

		// Act
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Assert
		Assert.NotNull(sut);
	}

	/// <summary>
	/// Verifies that the constructor resolves a relative storage root path against the current working directory.
	/// </summary>
	[Fact]
	public async Task Constructor_WhenStorageRootIsRelative_ResolvesAgainstWorkingDirectory()
	{
		// Arrange — relative path that is guaranteed to resolve into a writable location.
		string relativeRoot = "luma-store-rel-" + Guid.NewGuid().ToString("N");
		string absoluteRoot = Path.GetFullPath(relativeRoot);
		Directory.CreateDirectory(absoluteRoot);

		try
		{
			LocalFileResourceStore sut = CreateStore(relativeRoot);

			// Act — perform a SaveAsync; the file must land under the resolved absolute root.
			await using MemoryStream content = MakeStream("relative-root-test");
			await sut.SaveAsync("file.bin", content);

			// Assert
			Assert.True(File.Exists(Path.Combine(absoluteRoot, "file.bin")));
		}
		finally
		{
			if (Directory.Exists(absoluteRoot))
			{
				Directory.Delete(absoluteRoot, recursive: true);
			}
		}
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when the options argument is
	/// <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
			new LocalFileResourceStore(null!, NullLogger<LocalFileResourceStore>.Instance));

		Assert.Equal("options", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when the logger argument is
	/// <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using var root = new TempStorageRoot();
		IOptions<ResourceStoreOptions> options =
			Options.Create(new ResourceStoreOptions { StorageRootPath = root.Path });

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new LocalFileResourceStore(options, null!));
		Assert.Equal("logger", ex.ParamName);
	}
}
