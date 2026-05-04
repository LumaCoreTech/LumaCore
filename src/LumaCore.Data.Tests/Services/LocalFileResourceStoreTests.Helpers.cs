// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LocalFileResourceStoreTests
{
	/// <summary>
	/// Creates a fresh, isolated storage root directory under the system temp directory and returns its
	/// absolute path. The returned path is guaranteed to exist and to be writable.
	/// </summary>
	/// <returns>The absolute path of the new storage root directory.</returns>
	/// <remarks>
	/// Caller is responsible for cleanup — tests use a <see cref="TempStorageRoot"/> scope guard for that.
	/// </remarks>
	private static string CreateTempStorageRoot()
	{
		string path = Path.Combine(Path.GetTempPath(), "luma-store-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}

	/// <summary>
	/// Creates a <see cref="LocalFileResourceStore"/> rooted at <paramref name="storageRoot"/>, optionally
	/// using a caller-supplied logger.
	/// </summary>
	/// <param name="storageRoot">The absolute storage root path to configure.</param>
	/// <param name="logger">An optional logger; defaults to <see cref="NullLogger{T}.Instance"/>.</param>
	/// <returns>The constructed store instance.</returns>
	private static LocalFileResourceStore CreateStore(
		string                           storageRoot,
		ILogger<LocalFileResourceStore>? logger = null)
	{
		IOptions<ResourceStoreOptions> options =
			Options.Create(new ResourceStoreOptions { StorageRootPath = storageRoot });
		return new LocalFileResourceStore(options, logger ?? NullLogger<LocalFileResourceStore>.Instance);
	}

	/// <summary>
	/// Creates a <see cref="MemoryStream"/> filled with the specified UTF-8 string, positioned at zero.
	/// </summary>
	/// <param name="content">The string to encode into the stream.</param>
	/// <returns>A readable, seekable memory stream containing the encoded bytes.</returns>
	private static MemoryStream MakeStream(string content) => new(System.Text.Encoding.UTF8.GetBytes(content));

	/// <summary>
	/// Disposable scope guard that creates a temporary storage root on construction and recursively
	/// deletes it on disposal. Tests use this to keep filesystem state isolated.
	/// </summary>
	private sealed class TempStorageRoot : IDisposable
	{
		/// <summary>
		/// Gets the absolute path of the managed temporary directory.
		/// </summary>
		public string Path { get; } = CreateTempStorageRoot();

		/// <inheritdoc/>
		public void Dispose()
		{
			// Best-effort cleanup: tests must not fail because Windows held a file handle a moment longer.
			try
			{
				if (Directory.Exists(Path))
				{
					Directory.Delete(Path, recursive: true);
				}
			}
			catch
			{
				// Swallow — leftover temp directories are harmless and will be reclaimed by the OS.
			}
		}
	}
}
