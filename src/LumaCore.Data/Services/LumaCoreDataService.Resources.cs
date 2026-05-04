// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Services;

public sealed partial class LumaCoreDataService
{
	/// <summary>
	/// Maximum number of attempts to allocate unique <see cref="ResourceReferenceEntity.PublicId"/> values
	/// when cloning references. A collision is astronomically unlikely; this bound just prevents an
	/// infinite loop in the theoretical worst case.
	/// </summary>
	private const int MaxClonePublicIdAttempts = 3;

	#region Projection APIs

	/// <inheritdoc/>
	public async Task<IReadOnlyDictionary<ResourceOwnerId, IReadOnlyList<ResourceReferenceMetadata>>>
		GetResourceReferenceMetadataByOwnersAsync(
			ResourceOwnerKind            ownerKind,
			IEnumerable<ResourceOwnerId> ownerIds,
			CancellationToken            cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(ownerIds);

		// Materialize once at the boundary so the caller-supplied enumerable is enumerated exactly
		// once and EF gets a stable in-memory list for the IN-clause.
		List<ResourceOwnerId> ids = ownerIds as List<ResourceOwnerId> ?? ownerIds.ToList();

		if (ids.Count == 0)
			return new Dictionary<ResourceOwnerId, IReadOnlyList<ResourceReferenceMetadata>>();

		var rows = await mDbContext.ResourceReferences
			           .AsNoTracking()
			           .Where(rr => rr.OwnerKind == ownerKind && ids.Contains(rr.OwnerId))
			           .Join(
				           mDbContext.Resources,
				           rr => rr.ResourceId,
				           r => r.Id,
				           (rr, r) => new
				           {
					           rr.OwnerId,
					           rr.PublicId,
					           rr.OriginalFileName,
					           rr.ContentType,
					           r.SizeBytes
				           })
			           .ToListAsync(cancellationToken)
			           .ConfigureAwait(false);

		return rows
			.GroupBy(x => x.OwnerId)
			.ToDictionary(
				g => g.Key,
				g => (IReadOnlyList<ResourceReferenceMetadata>)g.Select(x => new ResourceReferenceMetadata(
						x.PublicId,
						x.OriginalFileName,
						x.ContentType,
						x.SizeBytes))
					.ToList());
	}

	#endregion

	#region Mutation APIs

	/// <inheritdoc/>
	public async Task<int> AssignPendingReferencesToOwnerAsync(
		IEnumerable<Guid> referencePublicIds,
		ResourceOwnerKind ownerKind,
		ResourceOwnerId   ownerId,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(referencePublicIds);

		// Materialize once at the boundary so the caller-supplied enumerable is enumerated exactly
		// once and EF gets a stable in-memory list for the IN-clause.
		List<Guid> ids = referencePublicIds as List<Guid> ?? referencePublicIds.ToList();

		if (ids.Count == 0)
			return 0;

		return await mDbContext.ResourceReferences
			       .Where(rr => ids.Contains(rr.PublicId) &&
			                    rr.OwnerKind == ownerKind &&
			                    rr.OwnerId == ResourceOwnerId.Unassigned)
			       .ExecuteUpdateAsync(
				       s => s.SetProperty(rr => rr.OwnerId, ownerId),
				       cancellationToken)
			       .ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<ResourceReferenceMetadata>> CloneResourceReferencesAsync(
		IEnumerable<Guid> sourceReferencePublicIds,
		ResourceOwnerKind ownerKind,
		ResourceOwnerId   ownerId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(sourceReferencePublicIds);

		// Materialize once at the boundary so the caller-supplied enumerable is enumerated exactly
		// once and EF gets a stable in-memory list for the IN-clause.
		List<Guid> ids = sourceReferencePublicIds as List<Guid> ?? sourceReferencePublicIds.ToList();

		if (ids.Count == 0)
			return [];

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		// Load the source references to copy their resource links and metadata.
		var sources = await mDbContext.ResourceReferences
			              .AsNoTracking()
			              .Where(rr => ids.Contains(rr.PublicId))
			              .Join(
				              mDbContext.Resources,
				              rr => rr.ResourceId,
				              r => r.Id,
				              (rr, r) => new
				              {
					              rr.ResourceId,
					              rr.OriginalFileName,
					              rr.ContentType,
					              r.SizeBytes
				              })
			              .ToListAsync(cancellationToken)
			              .ConfigureAwait(false);

		if (sources.Count == 0)
			return [];

		// Bounded retry loop on PublicId collisions. A real Guid.NewGuid() collision is lottery-grade
		// improbable, but EF Core surfaces it as a DbUpdateException without a strongly-typed signal,
		// so we re-query the database to classify the failure provider-agnostically (no index-name
		// string matching). The loop bound prevents an infinite spin in the theoretical worst case.
		for (int attempt = 1; attempt <= MaxClonePublicIdAttempts; attempt++)
		{
			List<ResourceReferenceEntity> clones = new(sources.Count);

			foreach (var source in sources)
			{
				var clone = new ResourceReferenceEntity
				{
					PublicId = Guid.NewGuid(),
					ResourceId = source.ResourceId,
					OwnerKind = ownerKind,
					OwnerId = ownerId,
					OriginalFileName = source.OriginalFileName,
					ContentType = source.ContentType,
					CreatedAtUtc = effectiveUtcNow
				};

				mDbContext.ResourceReferences.Add(clone);
				clones.Add(clone);
			}

			try
			{
				await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (DbUpdateException) when (attempt < MaxClonePublicIdAttempts)
			{
				// Detach all pending clones so the change tracker is clean for either the next attempt
				// or the rethrow path. We must do this before the re-query below; otherwise the tracker
				// would still contain the failed inserts.
				foreach (ResourceReferenceEntity clone in clones)
				{
					mDbContext.Entry(clone).State = EntityState.Detached;
				}

				// Provider-agnostic classification: did any of the just-attempted PublicIds already
				// exist? If yes, treat as a GUID collision and retry with fresh ones. If no, the
				// failure has another cause (FK violation, connection error, ...) and must surface.
				Guid[] attempted = clones.Select(c => c.PublicId).ToArray();
				bool publicIdCollision = await mDbContext.ResourceReferences
					                         .AnyAsync(rr => attempted.Contains(rr.PublicId), cancellationToken)
					                         .ConfigureAwait(false);

				if (!publicIdCollision)
					throw;

				continue;
			}
			catch (DbUpdateException)
			{
				// Last attempt: detach so the caller's DbContext is left in a clean state, then rethrow.
				foreach (ResourceReferenceEntity clone in clones)
				{
					mDbContext.Entry(clone).State = EntityState.Detached;
				}
				throw;
			}

			var result = new List<ResourceReferenceMetadata>(clones.Count);
			for (int i = 0; i < clones.Count; i++)
			{
				ResourceReferenceEntity clone = clones[i];
				result.Add(
					new ResourceReferenceMetadata(
						clone.PublicId,
						clone.OriginalFileName,
						clone.ContentType,
						sources[i].SizeBytes));
			}

			return result;
		}

		// All loop iterations either return, continue, or rethrow — the last iteration's catch block
		// has no `when` filter, so it is guaranteed to rethrow on the final attempt. This statement
		// is required by the compiler because the for-loop bound is not a constant the flow analysis
		// can prove.
		throw new UnreachableException(
			$"CloneResourceReferencesAsync loop fell through after {MaxClonePublicIdAttempts} attempts; " +
			"this is a control-flow invariant violation.");
	}

	#endregion
}
