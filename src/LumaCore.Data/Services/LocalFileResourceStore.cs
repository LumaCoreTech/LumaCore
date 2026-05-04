// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Data.Services;

/// <summary>
/// Implements <see cref="IResourceStore"/> using the local filesystem.
/// </summary>
/// <remarks>
///     <para>
///     Files are stored under the directory configured in <see cref="ResourceStoreOptions.StorageRootPath"/>.
///     The root directory is created on first use if it does not exist.
///     </para>
///     <para>
///     All operations enforce path-traversal protection: the resolved absolute path of a storage path must
///     remain within the storage root. Any attempt to escape (e.g. via <c>../</c> segments) throws
///     <see cref="InvalidOperationException"/>.
///     </para>
///     <para>
///     <see cref="SaveAsync"/> uses <see cref="FileMode.CreateNew"/> to prevent silent overwrites — if a file
///     already exists at the target path, an <see cref="IOException"/> is thrown. This guards against GUID
///     collisions producing data loss.
///     </para>
/// </remarks>
public sealed class LocalFileResourceStore : IResourceStore
{
	/// <summary>
	/// The resolved absolute path to the storage root directory.
	/// </summary>
	private readonly string mStorageRoot;

	/// <summary>
	/// The logger instance for diagnostic output.
	/// </summary>
	private readonly ILogger<LocalFileResourceStore> mLogger;

	/// <summary>
	/// Initializes a new instance of the <see cref="LocalFileResourceStore"/> class.
	/// </summary>
	/// <param name="options">The resource storage configuration.</param>
	/// <param name="logger">The logger for diagnostic output.</param>
	public LocalFileResourceStore(IOptions<ResourceStoreOptions> options, ILogger<LocalFileResourceStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		mLogger = logger;

		// Resolve to absolute path once at construction time. The trailing separator is intentionally
		// preserved so Path.GetRelativePath() in ResolveSafePath() treats the root as a directory.
		mStorageRoot = Path.GetFullPath(options.Value.StorageRootPath)
			               .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
		               + Path.DirectorySeparatorChar;

		mLogger.LogDebug("Resource storage root resolved to: {StorageRoot}", mStorageRoot);
	}

	/// <inheritdoc/>
	public async Task SaveAsync(
		string            storagePath,
		Stream            content,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
		ArgumentNullException.ThrowIfNull(content);

		string absolutePath = ResolveSafePath(storagePath);

		// Ensure parent directory exists (supports future directory sharding like "a1/guid").
		string? directory = Path.GetDirectoryName(absolutePath);
		if (directory is not null)
		{
			Directory.CreateDirectory(directory);
		}

		// FileMode.CreateNew prevents silent overwrites on GUID collisions.
		var fileStream = new FileStream(
			absolutePath,
			FileMode.CreateNew,
			FileAccess.Write,
			FileShare.None,
			bufferSize: 81920,
			useAsync: true);

		bool completed = false;
		try
		{
			await content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
			await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
			completed = true;
		}
		finally
		{
			await fileStream.DisposeAsync().ConfigureAwait(false);

			// Clean up partial file if CopyToAsync threw (cancellation, IO error). Without this, a
			// half-written file would remain on disk forever: FileMode.CreateNew blocks any retry,
			// and ResourceCleanupService only sees orphaned DB rows, not orphaned files.
			if (!completed)
			{
				try
				{
					File.Delete(absolutePath);
				}
				catch (Exception ex)
				{
					// Best-effort cleanup; do not mask the original failure.
					mLogger.LogWarning(
						ex,
						"Failed to remove partial file after SaveAsync error: {StoragePath}",
						storagePath);
				}
			}
		}

		mLogger.LogDebug("Saved resource file: {StoragePath}", storagePath);
	}

	/// <inheritdoc/>
	public Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

		string absolutePath = ResolveSafePath(storagePath);

		// Honour the IResourceStore.DeleteAsync contract: report whether a file actually existed.
		// File.Delete is silent on missing files (it only throws DirectoryNotFoundException when the
		// parent directory is gone), so we cannot derive the "did exist" signal from its behaviour.
		// We therefore probe with File.Exists first.
		//
		// TOCTOU note: another process could theoretically delete the file between this check and
		// File.Delete. That is acceptable — the operation is idempotent (we catch the missing-file
		// races below and treat them as "did not exist"), and a false positive on the return value
		// is preferable to a meaningless always-true.
		bool existed = File.Exists(absolutePath);

		try
		{
			File.Delete(absolutePath);
		}
		catch (DirectoryNotFoundException)
		{
			mLogger.LogDebug("Delete skipped (directory not found): {StoragePath}", storagePath);
			return Task.FromResult(false);
		}
		catch (FileNotFoundException)
		{
			// Race: file was deleted between File.Exists and File.Delete. Report as "did not exist".
			mLogger.LogDebug("Delete skipped (file vanished mid-operation): {StoragePath}", storagePath);
			return Task.FromResult(false);
		}

		if (existed)
		{
			mLogger.LogDebug("Deleted resource file: {StoragePath}", storagePath);
		}
		else
		{
			mLogger.LogDebug("Delete skipped (file not found): {StoragePath}", storagePath);
		}

		return Task.FromResult(existed);
	}

	/// <inheritdoc/>
	public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
		cancellationToken.ThrowIfCancellationRequested();

		string absolutePath = ResolveSafePath(storagePath);

		// Open the file directly instead of pre-checking File.Exists, which has a TOCTOU window with
		// the GC sweep that may delete the file between the check and the open call.
		try
		{
			Stream stream = new FileStream(
				absolutePath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				bufferSize: 81920,
				useAsync: true);
			return Task.FromResult<Stream?>(stream);
		}
		catch (FileNotFoundException)
		{
			mLogger.LogDebug("Open skipped (file not found): {StoragePath}", storagePath);
			return Task.FromResult<Stream?>(null);
		}
		catch (DirectoryNotFoundException)
		{
			mLogger.LogDebug("Open skipped (directory not found): {StoragePath}", storagePath);
			return Task.FromResult<Stream?>(null);
		}
	}

	/// <inheritdoc/>
	public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
		cancellationToken.ThrowIfCancellationRequested();

		string absolutePath = ResolveSafePath(storagePath);
		return Task.FromResult(File.Exists(absolutePath));
	}

	/// <summary>
	/// Resolves a relative storage path to an absolute filesystem path and validates that it remains
	/// within the configured storage root.
	/// </summary>
	/// <param name="storagePath">The relative storage path to resolve.</param>
	/// <returns>The validated absolute path.</returns>
	/// <exception cref="InvalidOperationException">
	/// The resolved path escapes the storage root directory (path-traversal attempt).
	/// </exception>
	private string ResolveSafePath(string storagePath)
	{
		string absolutePath = Path.GetFullPath(Path.Combine(mStorageRoot, storagePath));

		// Use Path.GetRelativePath() for containment instead of StartsWith. This is filesystem-agnostic:
		// it sidesteps the case-sensitivity question entirely (NTFS-on-Linux is case-sensitive,
		// ext4-on-Windows via WSL is case-sensitive, APFS is configurable, etc.).
		//
		// The relative path indicates an escape if it is exactly "..", begins with a "../" or "..\"
		// segment, or is rooted (an absolute path the caller smuggled in via Path.Combine, which
		// silently discards its first argument when the second is rooted). We deliberately do NOT
		// reject paths that merely *start* with the literal characters ".." (e.g. a legitimate
		// filename "..foo") — only true parent-directory traversals.
		//
		// AltDirectorySeparatorChar is included for cross-platform safety: on Windows it catches
		// "../foo" coming from web/REST callers; on Linux it equals DirectorySeparatorChar (both '/')
		// and the second check is a harmless duplicate.
		string relative = Path.GetRelativePath(mStorageRoot, absolutePath);
		bool escapes =
			relative == ".." ||
			relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
			relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) ||
			Path.IsPathRooted(relative);

		if (escapes)
		{
			throw new InvalidOperationException(
				$"Path traversal detected: resolved path '{absolutePath}' escapes storage root.");
		}

		return absolutePath;
	}
}
