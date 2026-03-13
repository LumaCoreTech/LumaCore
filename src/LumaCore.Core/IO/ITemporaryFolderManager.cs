// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.IO;

/// <summary>
/// Creates and manages temporary folders with automatic cleanup.
/// </summary>
/// <remarks>
///     <para>
///     Folders created through the manager are tracked and protected by a lock file, enabling
///     <see cref="CleanupOrphanedFolders"/> to detect and remove folders that were orphaned by a process crash.
///     </para>
///     <para>
///     Disposing the manager disposes all folders it has created, removing their contents from disk.
///     </para>
/// </remarks>
public interface ITemporaryFolderManager : IDisposable
{
	/// <summary>
	/// Gets the base directory under which all managed temporary folders are created.
	/// </summary>
	string BasePath { get; }

	/// <summary>
	/// Creates a new temporary folder on disk.
	/// </summary>
	/// <param name="prefix">
	/// An optional human-readable prefix for the folder name (e.g., <c>"shuttle-export"</c>).
	/// The prefix is prepended to a GUID to form the directory name.
	/// </param>
	/// <returns>A new <see cref="ITemporaryFolder"/> representing the created directory.</returns>
	ITemporaryFolder CreateFolder(string? prefix = null);

	/// <summary>
	/// Scans the base directory for orphaned temporary folders and removes them.
	/// </summary>
	/// <remarks>
	///     <para>
	///     A folder is considered orphaned when its associated lock file can be opened for reading, indicating that
	///     no process currently holds an exclusive lock on it. This happens when the process that created the folder
	///     terminated without disposing it (e.g., due to a crash).
	///     </para>
	///     <para>
	///     Errors during cleanup (e.g., permission issues, concurrent access) are logged and swallowed — cleanup
	///     is best-effort.
	///     </para>
	/// </remarks>
	void CleanupOrphanedFolders();
}
