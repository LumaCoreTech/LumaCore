// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Core.IO;

using Microsoft.Extensions.Logging;

using Xunit;

namespace LumaCore.Core.Tests.IO;

public sealed partial class TemporaryFolderManagerTests
{
	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> removes a folder and its lock file
	/// when the lock file is not held by any process (orphaned).
	/// </summary>
	[Fact]
	public void CleanupOrphanedFolders_OrphanedFolder_DeletesFolderAndLockFile()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			// This first orphan gets cleaned up automatically by the constructor's built-in
			// CleanupOrphanedFolders() call — it only exists to keep the base path non-empty.
			SimulateOrphanedFolder(basePath);
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Simulate a second orphan AFTER construction — this one is the actual test target,
			// ensuring the explicit CleanupOrphanedFolders() call (not just the constructor) works.
			(string orphanFolder2, string orphanLock2) = SimulateOrphanedFolder(basePath, "late-orphan");
			Assert.True(Directory.Exists(orphanFolder2));
			Assert.True(File.Exists(orphanLock2));

			// Act
			sut.CleanupOrphanedFolders();

			// Assert
			Assert.False(Directory.Exists(orphanFolder2));
			Assert.False(File.Exists(orphanLock2));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> does not remove a folder whose lock
	/// file is actively held by the current process.
	/// </summary>
	[Fact]
	public void CleanupOrphanedFolders_ActiveFolder_LockHeld_SkipsFolder()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Create a folder with an actively held lock file (simulates a running process)
			string activeFolderName = $"active-{Guid.NewGuid():N}";
			string activeFolderPath = Path.Combine(basePath, activeFolderName);
			string activeLockPath = activeFolderPath + ".lock";
			Directory.CreateDirectory(activeFolderPath);

			// Hold the lock file open for the duration of the test
			using var lockStream = new FileStream(
				activeLockPath,
				FileMode.Create,
				FileAccess.ReadWrite,
				FileShare.None);

			// Act
			sut.CleanupOrphanedFolders();

			// Assert — active folder and lock file are untouched
			Assert.True(Directory.Exists(activeFolderPath));
			Assert.True(File.Exists(activeLockPath));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> removes a stale <c>.lock</c> file
	/// that has no matching directory on disk.
	/// </summary>
	[Fact]
	public void CleanupOrphanedFolders_StaleLockFile_NoMatchingDirectory_DeletesLockFile()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Create a lock file with no corresponding directory — this can happen if the directory
			// was manually deleted but the lock file survived (e.g., partial cleanup, external tool).
			// File.Create() + immediate dispose = lock file exists on disk but is not held by any process.
			string staleLockPath = Path.Combine(basePath, $"stale-{Guid.NewGuid():N}.lock");
			using (File.Create(staleLockPath)) { }
			Assert.True(File.Exists(staleLockPath));

			// Act
			sut.CleanupOrphanedFolders();

			// Assert
			Assert.False(File.Exists(staleLockPath));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> completes without error when there
	/// are no lock files in the base directory.
	/// </summary>
	[Fact]
	public void CleanupOrphanedFolders_NoLockFiles_DoesNothing()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Act + Assert (no exception)
			sut.CleanupOrphanedFolders();
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> correctly handles a mix of orphaned
	/// and active folders, cleaning only the orphans while leaving active ones untouched.
	/// </summary>
	[Fact]
	public void CleanupOrphanedFolders_MixedScenarios_CleansOnlyOrphans()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		FileStream? activeLock = null;
		try
		{
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);

			// Orphaned folder: lock file exists on disk but is NOT held → cleanup can open it
			// exclusively → concludes the owning process is gone → deletes folder + lock.
			(string orphanPath, string orphanLockPath) = SimulateOrphanedFolder(basePath, "orphan");

			// Active folder: lock file is held with FileShare.None → cleanup's exclusive open
			// attempt throws IOException → folder is treated as in-use and left alone.
			string activeFolderPath = Path.Combine(basePath, $"active-{Guid.NewGuid():N}");
			string activeLockPath = activeFolderPath + ".lock";
			Directory.CreateDirectory(activeFolderPath);
			activeLock = new FileStream(activeLockPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

			// Act
			sut.CleanupOrphanedFolders();

			// Assert — orphan removed, active untouched
			Assert.False(Directory.Exists(orphanPath));
			Assert.False(File.Exists(orphanLockPath));
			Assert.True(Directory.Exists(activeFolderPath));
			Assert.True(File.Exists(activeLockPath));
		}
		finally
		{
			activeLock?.Dispose();
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> logs the count of cleaned folders
	/// at <see cref="LogLevel.Information"/> when orphaned folders are found and removed.
	/// </summary>
	[Fact]
	public void CleanupOrphanedFolders_OrphansCleaned_LogsInformationWithCount()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var logger = new ListLogger<TemporaryFolderManager>();

			// Simulate two orphaned folders before creating the manager
			SimulateOrphanedFolder(basePath, "orphan1");
			SimulateOrphanedFolder(basePath, "orphan2");

			// Act — the constructor calls CleanupOrphanedFolders() which should log
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath, logger);

			// Assert
			LogEntry infoEntry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
			Assert.Contains("2", infoEntry.Message);
			Assert.Contains(basePath, infoEntry.Message);
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> does not throw when an orphaned
	/// folder cannot be deleted (e.g., a file inside is locked on Windows), and logs a warning for the failed
	/// deletion.
	/// </summary>
	[Fact]
	public void CleanupOrphanedFolders_OrphanFolderDeleteFails_LogsWarningAndContinues()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		FileStream? innerLock = null;
		try
		{
			var logger = new ListLogger<TemporaryFolderManager>();
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath, logger);
			logger.Entries.Clear();

			// Simulate an orphan whose directory cannot be deleted (locked inner file on Windows)
			(string orphanPath, string _) = SimulateOrphanedFolder(basePath, "stuck");
			string innerFile = Path.Combine(orphanPath, "locked.tmp");
			File.WriteAllText(innerFile, "x");
			innerLock = new FileStream(innerFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

			// Act + Assert (no exception)
			sut.CleanupOrphanedFolders();

			// On Windows, Directory.Delete() fails → the folder persists and a warning is logged.
			// On Linux, open handles do not block deletion → the folder is cleaned up normally.
			if (OperatingSystem.IsWindows())
			{
				Assert.True(Directory.Exists(orphanPath));
				Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
			}
			else
			{
				Assert.False(Directory.Exists(orphanPath));
			}
		}
		finally
		{
			innerLock?.Dispose();
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> still removes the orphaned folder
	/// when the subsequent lock file deletion fails. Uses <see cref="ExecutionStageMonitor"/> to inject an
	/// <see cref="IOException"/> at the <c>File.Delete</c> call in
	/// <c>TryDeleteFile()</c>.
	/// </summary>
	[Fact]
	public void CleanupOrphanedFolders_LockFileDeletionFails_OrphanFolderStillCleaned()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath);
			(string orphanPath, string orphanLockPath) = SimulateOrphanedFolder(basePath, "fault-inject");
			Assert.True(Directory.Exists(orphanPath));
			Assert.True(File.Exists(orphanLockPath));

			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt(
					"TemporaryFolderManager.TryDeleteFile",
					new IOException("Simulated lock file deletion failure"));

			// Act + Assert (no exception — TryDeleteFile swallows the injected IOException)
			sut.CleanupOrphanedFolders();

			// Assert — orphaned folder deleted (Directory.Delete succeeded before the fault),
			// but lock file persists because TryDeleteFile was preempted by the injected exception.
			Assert.False(Directory.Exists(orphanPath));
			Assert.True(File.Exists(orphanLockPath));
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}

	/// <summary>
	/// Verifies that <see cref="TemporaryFolderManager.CleanupOrphanedFolders"/> does not throw when the base path
	/// no longer exists (e.g., deleted externally), and logs a warning for the failed scan.
	/// </summary>
	[Fact]
	public void CleanupOrphanedFolders_BasePathDeleted_LogsWarningAndDoesNotThrow()
	{
		// Arrange
		string basePath = CreateIsolatedBasePath();
		try
		{
			var logger = new ListLogger<TemporaryFolderManager>();
			using TemporaryFolderManager sut = CreateManagerWithBasePath(basePath, logger);
			logger.Entries.Clear();

			// Delete the basePath to simulate an externally removed directory
			Directory.Delete(basePath, recursive: true);

			// Act + Assert (no exception — the outer catch swallows DirectoryNotFoundException)
			sut.CleanupOrphanedFolders();

			// Assert — warning logged about the failed scan
			LogEntry warningEntry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
			Assert.Contains(basePath, warningEntry.Message);
		}
		finally
		{
			CleanupBasePath(basePath);
		}
	}
}
