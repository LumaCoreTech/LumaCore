// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.TestUtilities.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Core.Tests.IO;

public sealed partial class TemporaryFolderManagerTests
{
	/// <summary>
	/// Creates an isolated temporary base directory for a single test. The caller is responsible for cleaning it up
	/// via <see cref="CleanupBasePath"/> in a <c>finally</c> block.
	/// </summary>
	/// <returns>The absolute path to the newly created base directory.</returns>
	private static string CreateIsolatedBasePath()
	{
		string path = Path.Combine(Path.GetTempPath(), $"LumaCore-Tests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	/// <summary>
	/// Deletes the specified directory and all its contents. Best-effort: I/O exceptions are swallowed.
	/// </summary>
	/// <param name="path">The absolute path to the directory to delete.</param>
	private static void CleanupBasePath(string path)
	{
		try
		{
			if (Directory.Exists(path))
				Directory.Delete(path, recursive: true);
		}
		catch
		{
			// Best-effort cleanup.
		}
	}

	/// <summary>
	/// Creates a <see cref="TemporaryFolderManager"/> configured to use the specified base path and optional logger.
	/// </summary>
	/// <param name="basePath">The base directory for managed temporary folders.</param>
	/// <param name="logger">An optional logger for capturing log output.</param>
	/// <returns>A new <see cref="TemporaryFolderManager"/> instance.</returns>
	private static TemporaryFolderManager CreateManagerWithBasePath(
		string                           basePath,
		ILogger<TemporaryFolderManager>? logger = null)
	{
		IOptions<TemporaryFolderManagerOptions> options =
			Options.Create(new TemporaryFolderManagerOptions { BasePath = basePath });
		return new TemporaryFolderManager(options, logger);
	}

	/// <summary>
	/// Simulates an orphaned temporary folder by creating a folder and an unlocked <c>.lock</c> file on disk. The
	/// lock file is created and immediately closed, mimicking a folder left behind by a crashed process.
	/// </summary>
	/// <param name="basePath">The base directory in which to create the orphaned folder.</param>
	/// <param name="prefix">An optional prefix for the folder name.</param>
	/// <returns>
	/// A tuple of the absolute folder path and the absolute lock file path of the simulated orphan.
	/// </returns>
	private static (string FolderPath, string LockFilePath) SimulateOrphanedFolder(
		string  basePath,
		string? prefix = null)
	{
		string folderName = string.IsNullOrWhiteSpace(prefix)
			                    ? Guid.NewGuid().ToString("N")
			                    : $"{prefix}-{Guid.NewGuid():N}";
		string folderPath = Path.Combine(basePath, folderName);
		string lockFilePath = folderPath + ".lock";

		Directory.CreateDirectory(folderPath);

		// Create and immediately close the lock file — no process holds the lock.
		using (File.Create(lockFilePath)) { }

		return (folderPath, lockFilePath);
	}
}
