// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.IO;

/// <summary>
/// Represents a temporary folder on disk that is automatically deleted when disposed.
/// </summary>
/// <remarks>
///     <para>
///     Instances are created either directly via <see cref="TemporaryFolder(string?, string?)"/> for standalone use
///     (e.g., in tests) or through <see cref="ITemporaryFolderManager.CreateFolder"/> for production scenarios with
///     lock-file-based orphan detection.
///     </para>
///     <para>
///     The folder can contain arbitrary files and subdirectories. Use <see cref="GetFilePath"/> to compute a file
///     path within the folder, or <see cref="CreateFile"/> to create an empty file and retrieve its path.
///     </para>
/// </remarks>
public interface ITemporaryFolder : IDisposable
{
	/// <summary>
	/// Gets the absolute path to the temporary folder on disk.
	/// </summary>
	string Path { get; }

	/// <summary>
	/// Computes the absolute path for a file within this temporary folder without creating the file.
	/// </summary>
	/// <param name="fileName">The file name (including extension) to place inside the folder.</param>
	/// <returns>The absolute path to the file within <see cref="Path"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="fileName"/> is empty or whitespace-only.</exception>
	string GetFilePath(string fileName);

	/// <summary>
	/// Creates an empty file inside this temporary folder and returns its absolute path.
	/// </summary>
	/// <param name="fileName">The file name (including extension) to create inside the folder.</param>
	/// <returns>The absolute path to the newly created file within <see cref="Path"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="fileName"/> is empty or whitespace-only.</exception>
	string CreateFile(string fileName);
}
