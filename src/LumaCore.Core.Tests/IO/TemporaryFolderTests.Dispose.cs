// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Core.IO;

using Xunit;

namespace LumaCore.Core.Tests.IO;

public sealed partial class TemporaryFolderTests
{
	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.Dispose"/> deletes the temporary folder from disk in standalone mode.
	/// </summary>
	[Fact]
	public void Dispose_StandaloneMode_DeletesFolder()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			string folderPath = sut.Path;
			Assert.True(Directory.Exists(folderPath));

			// Act
			sut.Dispose();

			// Assert
			Assert.False(Directory.Exists(folderPath));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="TemporaryFolder.Dispose"/> twice in standalone mode
	/// does not throw (idempotent).
	/// </summary>
	[Fact]
	public void Dispose_StandaloneMode_CalledTwice_IsIdempotent()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);

			// Act + Assert (no exception on second call)
			sut.Dispose();
			sut.Dispose();
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.Dispose"/> does not throw when a file inside the folder is locked,
	/// exercising the best-effort <see cref="Directory.Delete(string, bool)"/> catch path. On Windows, the locked
	/// file prevents deletion; on Linux, open handles do not block deletion.
	/// </summary>
	[Fact]
	public void Dispose_StandaloneMode_LockedFileInsideFolder_DoesNotThrow()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		FileStream? lockedFile = null;
		try
		{
			var sut = new TemporaryFolder(basePath: basePath);
			string filePath = sut.CreateFile("locked.tmp");
			lockedFile = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

			// Act + Assert (no exception — best-effort deletion swallows I/O errors)
			sut.Dispose();
		}
		finally
		{
			lockedFile?.Dispose();
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.Dispose"/> in managed mode deletes both the temporary folder and
	/// its adjacent <c>.lock</c> file.
	/// </summary>
	[Fact]
	public void Dispose_ManagedMode_DeletesFolderAndLockFile()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager manager = CreateManagerForTesting(basePath);
			var sut = new TemporaryFolder(manager, basePath, prefix: null);
			string folderPath = sut.Path;
			string lockFilePath = folderPath + ".lock";
			Assert.True(Directory.Exists(folderPath));
			Assert.True(File.Exists(lockFilePath));

			// Act
			sut.Dispose();

			// Assert
			Assert.False(Directory.Exists(folderPath));
			Assert.False(File.Exists(lockFilePath));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.Dispose"/> in managed mode calls
	/// <see cref="TemporaryFolderManager.RemoveFolder"/> to untrack itself, so that the manager's own
	/// <see cref="TemporaryFolderManager.Dispose"/> does not attempt a double-dispose.
	/// </summary>
	/// <remarks>
	/// This is the folder-side counterpart to
	/// <c>TemporaryFolderManagerTests.Dispose_IndividualFolderAlreadyDisposed_ManagerDisposeSucceeds</c>,
	/// which tests the same scenario from the manager's perspective.
	/// </remarks>
	[Fact]
	public void Dispose_ManagedMode_NotifiesManagerToStopTracking()
	{
		// Arrange — create a managed folder that the manager tracks
		string basePath = CreateIsolatedBasePath();
		try
		{
			TemporaryFolderManager manager = CreateManagerForTesting(basePath);
			var sut = (TemporaryFolder)manager.CreateFolder("tracked");
			string folderPath = sut.Path;

			// Act — dispose the folder first; this should call manager.RemoveFolder(this) internally
			// (see TemporaryFolder.Dispose(), line: mManager?.RemoveFolder(this)).
			// Then dispose the manager — if RemoveFolder() was NOT called, the manager would try to
			// dispose the already-disposed folder again, risking errors.
			sut.Dispose();
			manager.Dispose();

			// Assert — no double-dispose exception, folder is cleaned up
			Assert.False(Directory.Exists(folderPath));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="TemporaryFolder.Dispose"/> twice in managed mode does not throw (idempotent).
	/// </summary>
	[Fact]
	public void Dispose_ManagedMode_CalledTwice_IsIdempotent()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager manager = CreateManagerForTesting(basePath);
			var sut = new TemporaryFolder(manager, basePath, prefix: null);

			// Act + Assert (no exception on second call)
			sut.Dispose();
			sut.Dispose();
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolder.Dispose"/> does not throw when the lock file deletion fails
	/// (e.g., another process has re-locked the file between close and delete). Uses
	/// <see cref="ExecutionStageMonitor"/> to deterministically inject an <see cref="IOException"/> at the
	/// <c>File.Delete()</c> call for the lock file.
	/// </summary>
	[Fact]
	public void Dispose_ManagedMode_LockFileDeletionFails_DoesNotThrow()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager manager = CreateManagerForTesting(basePath);
			var sut = new TemporaryFolder(manager, basePath, prefix: null);
			string folderPath = sut.Path;
			string lockFilePath = folderPath + ".lock";
			Assert.True(Directory.Exists(folderPath));
			Assert.True(File.Exists(lockFilePath));

			// Inject an IOException at the "DeleteLockFile" stage. TemporaryFolder.Dispose() works
			// in sequence: (1) release lock file stream, (2) Directory.Delete(), (3) File.Delete()
			// for the .lock file. The monitor intercepts step 3, so the folder is already deleted.
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt(
					"TemporaryFolder.Dispose.DeleteLockFile",
					new IOException("Simulated lock file deletion failure"));

			// Act + Assert (no exception — the catch block in step 3 swallows the injected IOException)
			sut.Dispose();

			// Assert — folder is gone (step 2 succeeded), but lock file persists (step 3 was faulted).
			Assert.False(Directory.Exists(folderPath));
			Assert.True(File.Exists(lockFilePath));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}
}
