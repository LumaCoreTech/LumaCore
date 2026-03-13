// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;

using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Core.Tests.IO;

public sealed partial class TemporaryFolderManagerTests
{
	/// <summary>
	/// Verifies that the DI constructor creates the base directory on disk when it does not yet exist.
	/// </summary>
	[Fact]
	public void Constructor_ValidOptions_CreatesBaseDirectory()
	{
		// Arrange
		string basePath = Path.Combine(Path.GetTempPath(), $"LumaCore-Tests-{Guid.NewGuid():N}");
		Assert.False(Directory.Exists(basePath));

		try
		{
			// Act
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Assert
			Assert.True(Directory.Exists(basePath));
			Assert.Equal(Path.GetFullPath(basePath), sut.BasePath);
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that the DI constructor accepts a <see langword="null"/> logger without throwing,
	/// falling back to <c>NullLogger</c> internally.
	/// </summary>
	[Fact]
	public void Constructor_NullLogger_DoesNotThrow()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			IOptions<TemporaryFolderManagerOptions> options =
				Options.Create(new TemporaryFolderManagerOptions { BasePath = basePath });

			// Act + Assert (no exception)
			using var sut = new TemporaryFolderManager(options, logger: null);
			Assert.Equal(Path.GetFullPath(basePath), sut.BasePath);
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that the DI constructor runs orphan cleanup during initialization, removing any orphaned folders
	/// that were left behind by a previous process.
	/// </summary>
	[Fact]
	public void Constructor_CleansUpOrphanedFoldersOnCreation()
	{
		// Arrange — simulate an orphaned folder before creating the manager
		string basePath = CreateIsolatedBasePath();
		try
		{
			(string orphanFolderPath, string orphanLockPath) = SimulateOrphanedFolder(basePath, "orphan");
			Assert.True(Directory.Exists(orphanFolderPath));
			Assert.True(File.Exists(orphanLockPath));

			// Act — the constructor calls CleanupOrphanedFolders() automatically
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Assert — the orphaned folder and lock file were removed
			Assert.False(Directory.Exists(orphanFolderPath));
			Assert.False(File.Exists(orphanLockPath));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that the parameterless constructor creates a manager with the default base path from
	/// <see cref="TemporaryFolderManagerOptions"/>.
	/// </summary>
	[Fact]
	public void Constructor_Parameterless_UsesDefaultBasePath()
	{
		// Arrange
		string expectedBasePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LumaCore"));

		// Act
		using var sut = new TemporaryFolderManager();

		// Assert
		Assert.Equal(expectedBasePath, sut.BasePath);
		Assert.True(Directory.Exists(sut.BasePath));
	}

	/// <summary>
	/// Verifies that the DI constructor throws <see cref="ArgumentNullException"/> when the options parameter is
	/// <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_NullOptions_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new TemporaryFolderManager(null!));
		Assert.Equal("options", ex.ParamName);
	}
}
