// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Services;

/// <summary>
/// Abstraction for physical file storage operations used by the resource storage system.
/// </summary>
/// <remarks>
///     <para>
///     All paths passed to this interface are <b>relative</b> to the configured storage root
///     (see <see cref="ResourceStoreOptions.StorageRootPath"/>). Implementations are responsible for
///     combining these with the root path and enforcing path-traversal protection.
///     </para>
///     <para>
///     Operations are designed to be idempotent where applicable (e.g. <see cref="DeleteAsync"/> on a
///     non-existent file returns <see langword="false"/> instead of throwing) to support safe retry
///     semantics in the garbage collector.
///     </para>
/// </remarks>
public interface IResourceStore
{
	/// <summary>
	/// Persists the content of a stream to a new file at the specified storage path.
	/// </summary>
	/// <param name="storagePath">
	/// The relative path (GUID-based filename) within the storage root where the file will be created.
	/// </param>
	/// <param name="content">The stream containing the file content to persist.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that completes when the file has been fully written and flushed to disk.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="storagePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="storagePath"/> is empty or whitespace-only.
	/// </exception>
	/// <exception cref="IOException">
	/// A file already exists at <paramref name="storagePath"/>
	/// (uses <see cref="FileMode.CreateNew"/> to prevent silent overwrites).
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The resolved absolute path escapes the configured storage root (path-traversal attempt).
	/// </exception>
	Task SaveAsync(string storagePath, Stream content, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes the file at the specified storage path.
	/// </summary>
	/// <param name="storagePath">The relative path within the storage root of the file to delete.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the file existed and was deleted;
	/// <see langword="false"/> if the file did not exist (idempotent for GC retry safety).
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="storagePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="storagePath"/> is empty or whitespace-only.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The resolved absolute path escapes the configured storage root (path-traversal attempt).
	/// </exception>
	Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Opens a read-only stream for the file at the specified storage path.
	/// </summary>
	/// <param name="storagePath">The relative path within the storage root of the file to read.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A readable stream positioned at the beginning of the file, or <see langword="null"/> if the file
	/// does not exist. The caller is responsible for disposing the returned stream.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="storagePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="storagePath"/> is empty or whitespace-only.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The resolved absolute path escapes the configured storage root (path-traversal attempt).
	/// </exception>
	Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks whether a file exists at the specified storage path.
	/// </summary>
	/// <param name="storagePath">The relative path within the storage root to check.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the file exists; <see langword="false"/> otherwise.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="storagePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="storagePath"/> is empty or whitespace-only.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The resolved absolute path escapes the configured storage root (path-traversal attempt).
	/// </exception>
	Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default);
}
