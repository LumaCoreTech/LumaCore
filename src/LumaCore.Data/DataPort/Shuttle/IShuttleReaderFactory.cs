// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.DataPort.Shuttle;

/// <summary>
/// Creates <see cref="IShuttleReader"/> instances for reading LumaCore Shuttle backup files.
/// </summary>
public interface IShuttleReaderFactory
{
	/// <summary>
	/// Creates a new <see cref="IShuttleReader"/> for the specified shuttle file.
	/// </summary>
	/// <param name="filePath">The absolute path to the LumaCore Shuttle file.</param>
	/// <returns>
	/// A new <see cref="IShuttleReader"/> instance. The reader is not yet initialized; the caller must invoke
	/// <see cref="IShuttleReader.InitializeAsync"/> before reading data. The caller owns the returned instance
	/// and is responsible for disposing it via <see cref="IAsyncDisposable.DisposeAsync"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="filePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="filePath"/> is empty, consists only of white-space characters, contains characters that are
	/// invalid on the current operating system, or contains a path segment that exceeds 255 characters.
	/// </exception>
	IShuttleReader Create(string filePath);
}
