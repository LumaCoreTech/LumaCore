// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LumaCore.Data.Tests;

public sealed partial class ResourceCleanupServiceTests
{
	/// <summary>
	/// Builds a non-persisted <see cref="ResourceEntity"/> with deterministic placeholder fields,
	/// used by shape-filter tests that only need an instance reference (no DB row).
	/// </summary>
	/// <returns>A fresh <see cref="ResourceEntity"/> instance.</returns>
	private static ResourceEntity NewResourceEntity() => new()
	{
		ContentHash = "deadbeef00000000000000000000000000000000000000000000000000000001",
		StoragePath = Guid.NewGuid().ToString(),
		SizeBytes = 1,
		CreatedAtUtc = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
		CreatedByParticipantId = null,
		DeletionState = ResourceDeletionState.PendingDeletion
	};

	/// <summary>
	/// Minimal in-memory <see cref="IResourceStore"/> that records delete calls and can be configured
	/// to throw on deletion to exercise the SWEEP error path.
	/// </summary>
	internal sealed class RecordingStore : IResourceStore
	{
		/// <summary>
		/// Number of <see cref="DeleteAsync"/> invocations observed.
		/// </summary>
		public int DeleteCount { get; private set; }

		/// <summary>
		/// When <see langword="true"/>, <see cref="DeleteAsync"/> throws an <see cref="IOException"/>
		/// after recording the call.
		/// </summary>
		public bool ThrowOnDelete { get; init; }

		/// <inheritdoc/>
		public Task SaveAsync(string storagePath, Stream content, CancellationToken cancellationToken = default) =>
			Task.CompletedTask;

		/// <inheritdoc/>
		public Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
		{
			DeleteCount++;
			if (ThrowOnDelete)
				throw new IOException("simulated file-store failure");
			return Task.FromResult(true);
		}

		/// <inheritdoc/>
		public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default) =>
			Task.FromResult<Stream?>(null);

		/// <inheritdoc/>
		public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default) =>
			Task.FromResult(false);
	}

	/// <summary>
	/// Test-only <see cref="DbUpdateConcurrencyException"/> subclass that exposes a controlled
	/// <see cref="DbUpdateException.Entries"/> list. EF Core's public constructors do not let
	/// production code inject a custom entries list, so we override the (virtual) property
	/// directly to inject the exact shapes the
	/// <see cref="ResourceCleanupService.IsBenignSweepRaceShape"/> filter must classify.
	/// </summary>
	private sealed class ShapedConcurrencyException : DbUpdateConcurrencyException
	{
		/// <summary>
		/// Initializes a new instance with the supplied <paramref name="entries"/>.
		/// </summary>
		/// <param name="entries">The conflicting EF entries to expose via <see cref="DbUpdateException.Entries"/>.</param>
		public ShapedConcurrencyException(IReadOnlyList<EntityEntry> entries)
			: base("shaped for unit test")
		{
			Entries = entries;
		}

		/// <inheritdoc/>
		public override IReadOnlyList<EntityEntry> Entries { get; }
	}
}
