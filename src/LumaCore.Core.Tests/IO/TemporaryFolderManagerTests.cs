// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;

using Xunit;

namespace LumaCore.Core.Tests.IO;

/// <summary>
/// Unit tests for <see cref="TemporaryFolderManager"/>.
/// </summary>
/// <remarks>
///     <para>
///     These tests verify the lifecycle management of temporary folders including construction, folder creation,
///     orphan cleanup, disposal, and internal tracking.
///     </para>
///     <para>
///     Test files are organized by public API member:
///     <list type="bullet">
///         <item>
///         <c>TemporaryFolderManagerTests.Construction.cs</c> — Constructor tests for DI and standalone modes
///         </item>
///         <item>
///         <c>TemporaryFolderManagerTests.CreateFolder.cs</c> — <see cref="TemporaryFolderManager.CreateFolder"/>
///         method tests
///         </item>
///         <item>
///         <c>TemporaryFolderManagerTests.CleanupOrphanedFolders.cs</c> —
///         <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> method tests
///         </item>
///         <item><c>TemporaryFolderManagerTests.Helpers.cs</c> — Shared test helpers, logger, and simulation utilities</item>
///     </list>
///     </para>
/// </remarks>
[Trait("Category", "IO")]
public sealed partial class TemporaryFolderManagerTests
{
	#region Dispose

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.Dispose"/> disposes all tracked folders, deleting their
	/// directories and lock files from disk.
	/// </summary>
	[Fact]
	public void Dispose_DisposesAllTrackedFolders()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			TemporaryFolderManager manager = CreateManagerWithBasePath(basePath);
			ITemporaryFolder folder1 = manager.CreateFolder("a");
			ITemporaryFolder folder2 = manager.CreateFolder("b");
			ITemporaryFolder folder3 = manager.CreateFolder("c");
			string path1 = folder1.Path;
			string path2 = folder2.Path;
			string path3 = folder3.Path;
			Assert.True(Directory.Exists(path1));
			Assert.True(Directory.Exists(path2));
			Assert.True(Directory.Exists(path3));

			// Act
			manager.Dispose();

			// Assert
			Assert.False(Directory.Exists(path1));
			Assert.False(Directory.Exists(path2));
			Assert.False(Directory.Exists(path3));
			Assert.False(File.Exists(path1 + ".lock"));
			Assert.False(File.Exists(path2 + ".lock"));
			Assert.False(File.Exists(path3 + ".lock"));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="TemporaryFolderManager.Dispose"/> twice does not throw (idempotent).
	/// </summary>
	[Fact]
	public void Dispose_CalledTwice_IsIdempotent()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			TemporaryFolderManager manager = CreateManagerWithBasePath(basePath);
			manager.CreateFolder();

			// Act + Assert (no exception on second call)
			manager.Dispose();
			manager.Dispose();
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that when a folder is individually disposed before the manager, the manager's
	/// <see cref="TemporaryFolderManager.Dispose"/> still succeeds without double-dispose issues.
	/// </summary>
	[Fact]
	public void Dispose_IndividualFolderAlreadyDisposed_ManagerDisposeSucceeds()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			TemporaryFolderManager manager = CreateManagerWithBasePath(basePath);
			ITemporaryFolder folder1 = manager.CreateFolder("early");
			ITemporaryFolder folder2 = manager.CreateFolder("late");
			string path1 = folder1.Path;
			string path2 = folder2.Path;

			// Dispose folder1 individually — its Dispose() calls manager.RemoveFolder(this),
			// so the manager should no longer track it when its own Dispose() runs later.
			folder1.Dispose();
			Assert.False(Directory.Exists(path1));
			Assert.True(Directory.Exists(path2));

			// Act — manager.Dispose() should only dispose folder2 (folder1 was already untracked).
			manager.Dispose();

			// Assert — folder2 also cleaned up, no exception
			Assert.False(Directory.Exists(path2));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	#endregion

	#region RemoveFolder

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.RemoveFolder"/> removes a tracked folder from the manager's
	/// internal list so that <see cref="TemporaryFolderManager.Dispose"/> does not attempt to dispose it again.
	/// </summary>
	[Fact]
	public void RemoveFolder_TrackedFolder_RemovesFromTracking()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			TemporaryFolderManager manager = CreateManagerWithBasePath(basePath);
			var folder = (TemporaryFolder)manager.CreateFolder("test");

			// Act — remove from tracking WITHOUT disposing the folder itself.
			// After this call, the manager no longer knows about this folder.
			manager.RemoveFolder(folder);

			// Assert — when the manager is disposed, it only disposes tracked folders.
			// Since we untracked this folder, it must still exist on disk after manager.Dispose().
			manager.Dispose();
			Assert.True(Directory.Exists(folder.Path));

			// Cleanup — the folder is now our responsibility since it's untracked.
			folder.Dispose();
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.RemoveFolder"/> does not throw when called with a folder that
	/// is not currently tracked (e.g., already removed or never added).
	/// </summary>
	[Fact]
	public void RemoveFolder_UnknownFolder_DoesNotThrow()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager manager = CreateManagerWithBasePath(basePath);

			// Create a folder that the manager tracks, then remove it once (Arrange).
			var unknownFolder = new TemporaryFolder(manager, basePath, prefix: null);
			manager.RemoveFolder(unknownFolder);

			// Act + Assert — calling RemoveFolder again for an already-untracked folder must not throw.
			manager.RemoveFolder(unknownFolder);

			// Cleanup — folder is untracked, so we must dispose it ourselves.
			unknownFolder.Dispose();
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	#endregion
}
