// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.Concurrent;

using LumaCore.Data.Services;

namespace LumaCore.Data.Tests.Services;

public sealed partial class ResourceServiceTests
{
	/// <summary>
	/// In-memory <see cref="IResourceStore"/> that records every operation for assertions and
	/// optionally fails saves to exercise error paths.
	/// </summary>
	internal sealed class FakeResourceStore : IResourceStore
	{
		private readonly ConcurrentDictionary<string, byte[]> mFiles = new();

		/// <summary>
		/// Number of <see cref="SaveAsync"/> calls observed.
		/// </summary>
		public int SaveCount { get; private set; }

		/// <summary>
		/// Number of <see cref="DeleteAsync"/> calls observed.
		/// </summary>
		public int DeleteCount { get; private set; }

		/// <summary>
		/// Storage paths that were saved at least once during the test.
		/// </summary>
		public List<string> SavedPaths { get; } = new();

		/// <summary>
		/// Storage paths passed to <see cref="DeleteAsync"/>.
		/// </summary>
		public List<string> DeletedPaths { get; } = new();

		/// <summary>
		/// Optional callback invoked during <see cref="SaveAsync"/> after the content has been
		/// buffered, but before it is recorded — used to inject failures or race conditions.
		/// </summary>
		public Func<string, Task>? OnSave { get; set; }

		/// <summary>
		/// Optional callback invoked at the start of <see cref="DeleteAsync"/> — used to inject
		/// failures (e.g., simulate a store-side I/O error during orphan-file cleanup).
		/// </summary>
		public Func<string, Task>? OnDelete { get; set; }

		/// <inheritdoc/>
		public async Task SaveAsync(string storagePath, Stream content, CancellationToken cancellationToken = default)
		{
			SaveCount++;
			SavedPaths.Add(storagePath);
			using var ms = new MemoryStream();
			await content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);

			if (OnSave is not null)
				await OnSave(storagePath).ConfigureAwait(false);

			mFiles[storagePath] = ms.ToArray();
		}

		/// <inheritdoc/>
		public async Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
		{
			DeleteCount++;
			DeletedPaths.Add(storagePath);

			if (OnDelete is not null)
				await OnDelete(storagePath).ConfigureAwait(false);

			return mFiles.TryRemove(storagePath, out byte[]? _);
		}

		/// <inheritdoc/>
		public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
		{
			if (mFiles.TryGetValue(storagePath, out byte[]? bytes))
				return Task.FromResult<Stream?>(new MemoryStream(bytes, writable: false));
			return Task.FromResult<Stream?>(null);
		}

		/// <inheritdoc/>
		public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default) =>
			Task.FromResult(mFiles.ContainsKey(storagePath));

		/// <summary>
		/// Returns the in-memory snapshot of all currently stored files.
		/// </summary>
		/// <returns>A dictionary of storage path to content bytes.</returns>
		public IReadOnlyDictionary<string, byte[]> Files => mFiles;
	}
}
