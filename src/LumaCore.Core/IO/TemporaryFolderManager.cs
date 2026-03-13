// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LumaCore.Core.IO;

/// <summary>
/// Creates and manages temporary folders with lock-file-based orphan detection (thread-safe).
/// </summary>
/// <remarks>
///     <para>
///     Each folder created through <see cref="CreateFolder"/> is protected by an exclusive <c>.lock</c> file. If the
///     owning process crashes, the OS releases the file lock, and a subsequent call to
///     <see cref="CleanupOrphanedFolders"/> can detect and remove the orphaned directory.
///     </para>
///     <para>
///     Disposing the manager disposes all folders it has created, removing their contents and lock files from disk.
///     </para>
///     <para>
///     This class is designed for dependency injection via
///     <c>IOptions&lt;TemporaryFolderManagerOptions&gt;</c>. For test or standalone usage, use the parameterless
///     constructor <see cref="TemporaryFolderManager()"/>.
///     </para>
/// </remarks>
public sealed class TemporaryFolderManager : ITemporaryFolderManager
{
	private readonly ILogger<TemporaryFolderManager> mLogger;
	private readonly List<TemporaryFolder>           mFolders = [];
	private          int                             mDisposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="TemporaryFolderManager"/> class using the specified options and
	/// logger.
	/// </summary>
	/// <param name="options">The configuration options specifying the base path for temporary folders.</param>
	/// <param name="logger">The logger for reporting orphan cleanup activity.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	public TemporaryFolderManager(
		IOptions<TemporaryFolderManagerOptions> options,
		ILogger<TemporaryFolderManager>?        logger = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		mLogger = logger ?? NullLogger<TemporaryFolderManager>.Instance;
		BasePath = Path.GetFullPath(options.Value.BasePath);
		Directory.CreateDirectory(BasePath);
		CleanupOrphanedFolders();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TemporaryFolderManager"/> class with default options and no
	/// logging. Intended for test and standalone usage.
	/// </summary>
	public TemporaryFolderManager()
		: this(Options.Create(new TemporaryFolderManagerOptions())) { }

	/// <inheritdoc/>
	public string BasePath { get; }

	/// <inheritdoc/>
	public ITemporaryFolder CreateFolder(string? prefix = null)
	{
		lock (mFolders)
		{
			ObjectDisposedException.ThrowIf(mDisposed != 0, this);
			var folder = new TemporaryFolder(this, BasePath, prefix);
			mFolders.Add(folder);
			return folder;
		}
	}

	/// <inheritdoc/>
	public void CleanupOrphanedFolders()
	{
		try
		{
			string[] lockFiles = Directory.GetFiles(BasePath, "*.lock");
			if (lockFiles.Length == 0)
				return;

			int cleaned = 0;

			foreach (string lockFilePath in lockFiles)
			{
				string folderPath = lockFilePath[..^".lock".Length];
				if (!Directory.Exists(folderPath))
				{
					// Lock file without a matching directory — remove the stale lock file.
					TryDeleteFile(lockFilePath);
					continue;
				}

				try
				{
					// Try to open the lock file. If it succeeds, no process holds the lock — the folder is
					// orphaned and can be safely removed.
					using (File.Open(lockFilePath, FileMode.Open, FileAccess.Read, FileShare.None)) { }
				}
				catch (IOException)
				{
					// The lock file is held by another process — folder is still in use.
					continue;
				}

				// Folder is orphaned — clean up.
				try
				{
					Directory.Delete(folderPath, recursive: true);
				}
				catch (Exception ex)
				{
					mLogger.LogWarning(ex, "Failed to delete orphaned temporary folder '{FolderPath}'.", folderPath);
					continue;
				}

				TryDeleteFile(lockFilePath);
				cleaned++;
			}

			if (cleaned > 0)
			{
				mLogger.LogInformation(
					"Cleaned up {Count} orphaned temporary folder(s) in '{BasePath}'.",
					cleaned,
					BasePath);
			}
		}
		catch (Exception ex)
		{
			mLogger.LogWarning(ex, "Failed to scan for orphaned temporary folders in '{BasePath}'.", BasePath);
		}
	}

	/// <summary>
	/// Removes the specified folder from the tracked list. Called by <see cref="TemporaryFolder.Dispose"/> when a
	/// managed folder is disposed individually.
	/// </summary>
	/// <param name="folder">The folder to stop tracking.</param>
	internal void RemoveFolder(TemporaryFolder folder)
	{
		lock (mFolders)
		{
			mFolders.Remove(folder);
		}
	}

	/// <summary>
	/// Disposes all tracked temporary folders, removing their contents and lock files from disk.
	/// </summary>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref mDisposed, 1) != 0)
			return;

		TemporaryFolder[] snapshot;
		lock (mFolders)
		{
			snapshot = [.. mFolders];
			mFolders.Clear();
		}

		foreach (TemporaryFolder folder in snapshot)
		{
			folder.Dispose();
		}
	}

	/// <summary>
	/// Attempts to delete the specified file, swallowing any I/O exceptions.
	/// </summary>
	/// <param name="filePath">The absolute path of the file to delete.</param>
	private static void TryDeleteFile(string filePath)
	{
		try
		{
			ExecutionStageMonitor.ReportStage("TemporaryFolderManager.TryDeleteFile");
			File.Delete(filePath);
		}
		catch
		{
			// Best-effort.
		}
	}
}
