// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

namespace LumaCore.Core.IO;

/// <summary>
/// A temporary folder on disk that is deleted (best-effort) when disposed.
/// </summary>
/// <remarks>
///     <para>
///     This class can be used in two modes:
///     </para>
///     <list type="bullet">
///         <item>
///             <term>Standalone</term>
///             <description>
///             Created directly via <see cref="TemporaryFolder(string?, string?)"/>. Suitable for tests or simple
///             scenarios where lock-file-based orphan detection is not needed.
///             </description>
///         </item>
///         <item>
///             <term>Managed</term>
///             <description>
///             Created through <see cref="TemporaryFolderManager.CreateFolder"/>. The manager tracks the folder and
///             holds an exclusive lock file for orphan detection.
///             </description>
///         </item>
///     </list>
///     <para>
///     Disposal is best-effort: I/O exceptions during deletion are silently swallowed because open handles or
///     antivirus scanners can temporarily prevent deletes on Windows.
///     </para>
/// </remarks>
public sealed class TemporaryFolder : ITemporaryFolder
{
	private readonly TemporaryFolderManager? mManager;
	private readonly FileStream?             mLockFile;
	private          int                     mDisposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="TemporaryFolder"/> class in standalone mode.
	/// </summary>
	/// <param name="prefix">
	/// An optional human-readable prefix for the folder name (e.g., <c>"shuttle-reader"</c>).
	/// When <see langword="null"/>, the folder is named with a GUID only.
	/// </param>
	/// <param name="basePath">
	/// The parent directory in which the temporary folder is created.
	/// When <see langword="null"/>, <see cref="System.IO.Path.GetTempPath()"/> is used.
	/// </param>
	public TemporaryFolder(string? prefix = null, string? basePath = null)
	{
		basePath ??= System.IO.Path.GetTempPath();
		string folderName = string.IsNullOrWhiteSpace(prefix)
			                    ? Guid.NewGuid().ToString("N")
			                    : $"{prefix}-{Guid.NewGuid():N}";
		Path = System.IO.Path.Combine(basePath, folderName);
		Directory.CreateDirectory(Path);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TemporaryFolder"/> class in managed mode, tracked by the
	/// specified <paramref name="manager"/> with an exclusive lock file for orphan detection.
	/// </summary>
	/// <param name="manager">The manager that tracks this folder.</param>
	/// <param name="basePath">The parent directory in which the temporary folder is created.</param>
	/// <param name="prefix">An optional human-readable prefix for the folder name.</param>
	internal TemporaryFolder(TemporaryFolderManager manager, string basePath, string? prefix)
	{
		mManager = manager;

		string folderName = string.IsNullOrWhiteSpace(prefix)
			                    ? Guid.NewGuid().ToString("N")
			                    : $"{prefix}-{Guid.NewGuid():N}";
		Path = System.IO.Path.Combine(basePath, folderName);
		Directory.CreateDirectory(Path);

		// Create a lock file with an exclusive lock — the OS releases it if the process crashes, allowing
		// TemporaryFolderManager.CleanupOrphanedFolders() to detect and remove the orphaned folder.
		string lockFilePath = Path + ".lock";
		mLockFile = new FileStream(lockFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
	}

	/// <inheritdoc/>
	public string Path { get; }

	/// <inheritdoc/>
	public string GetFilePath(string fileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
		return System.IO.Path.Combine(Path, fileName);
	}

	/// <inheritdoc/>
	public string CreateFile(string fileName)
	{
		ObjectDisposedException.ThrowIf(mDisposed != 0, this);
		string filePath = GetFilePath(fileName);
		using (File.Create(filePath)) { }
		return filePath;
	}

	/// <summary>
	/// Releases the lock file (if managed) and deletes the temporary folder and all its contents.
	/// </summary>
	/// <remarks>
	/// Deletion is best-effort. I/O exceptions are swallowed to prevent failures caused by transient file locks
	/// (e.g., antivirus scanners, open handles on Windows). The manager is notified so it stops tracking this folder.
	/// </remarks>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref mDisposed, 1) != 0)
			return;

		// Release the lock file first so the directory can be fully deleted.
		mLockFile?.Dispose();

		try
		{
			Directory.Delete(Path, recursive: true);
		}
		catch
		{
			// Best-effort: swallow to avoid failures from transient file locks.
		}

		// Delete the lock file after the directory — it lives next to the folder, not inside it.
		if (mLockFile is not null)
		{
			try
			{
				ExecutionStageMonitor.ReportStage("TemporaryFolder.Dispose.DeleteLockFile");
				File.Delete(Path + ".lock");
			}
			catch
			{
				// Best-effort.
			}
		}

		mManager?.RemoveFolder(this);
	}
}
