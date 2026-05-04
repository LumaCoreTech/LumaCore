// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// Provides data-query and set-based update operations for <see cref="ResourceReferenceEntity"/> records.
/// </summary>
/// <remarks>
///     <para>
///     This interface covers pure data-access operations (reads and set-based writes) on resource references.
///     Orchestration-level concerns (upload with filesystem coordination, download stream resolution,
///     pre-CASCADE cleanup) remain in <see cref="IResourceService"/>.
///     </para>
///     <para>
///     The polymorphic ownership model (<see cref="ResourceOwnerKind"/> + <c>OwnerId</c>) means these
///     methods accept <see cref="ResourceOwnerId"/> owner identifiers — a strongly-typed wrapper around
///     the underlying <see cref="long"/> primary key whose interpretation depends on the accompanying
///     <see cref="ResourceOwnerKind"/> discriminator.
///     </para>
/// </remarks>
public interface IResourceDataService
{
	#region Projection APIs

	/// <summary>
	/// Batch-loads attachment metadata for one or more owners by joining
	/// <see cref="ResourceReferenceEntity"/> with <see cref="ResourceEntity"/>.
	/// </summary>
	/// <param name="ownerKind">The kind of entity that owns the references.</param>
	/// <param name="ownerIds">The primary keys of the owning entities.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A dictionary mapping each owner id to its list of <see cref="ResourceReferenceMetadata"/>.
	/// Owners with no references are absent from the dictionary.
	/// </returns>
	/// <remarks>
	/// This method uses <see cref="Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>
	/// for read-only projections.
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="ownerIds"/> is <see langword="null"/>.
	/// </exception>
	Task<IReadOnlyDictionary<ResourceOwnerId, IReadOnlyList<ResourceReferenceMetadata>>>
		GetResourceReferenceMetadataByOwnersAsync(
			ResourceOwnerKind            ownerKind,
			IEnumerable<ResourceOwnerId> ownerIds,
			CancellationToken            cancellationToken = default);

	#endregion

	#region Mutation APIs

	/// <summary>
	/// Assigns pending (unowned) resource references to the specified owner using a set-based update.
	/// </summary>
	/// <param name="referencePublicIds">
	/// The public identifiers of the <see cref="ResourceReferenceEntity"/> rows to assign.
	/// Only references with <see cref="ResourceReferenceEntity.OwnerId"/> equal to
	/// <see cref="ResourceOwnerId.Unassigned"/> and the matching <paramref name="ownerKind"/> are updated.
	/// </param>
	/// <param name="ownerKind">The kind of entity that will own the references.</param>
	/// <param name="ownerId">The polymorphic identifier of the owning entity.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of resource references that were assigned.</returns>
	/// <remarks>
	/// Pending references are created during upload with <see cref="ResourceReferenceEntity.OwnerId"/> set to
	/// <see cref="ResourceOwnerId.Unassigned"/>. This method wires them to their final owner (e.g., a newly
	/// created message) in a single <c>ExecuteUpdateAsync()</c> call.
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="referencePublicIds"/> is <see langword="null"/>.
	/// </exception>
	Task<int> AssignPendingReferencesToOwnerAsync(
		IEnumerable<Guid> referencePublicIds,
		ResourceOwnerKind ownerKind,
		ResourceOwnerId   ownerId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates new resource references that point to the same underlying resources as the given source references,
	/// but owned by a different entity.
	/// </summary>
	/// <param name="sourceReferencePublicIds">
	/// The public identifiers of the existing <see cref="ResourceReferenceEntity"/> rows to clone.
	/// </param>
	/// <param name="ownerKind">The kind of entity that will own the cloned references.</param>
	/// <param name="ownerId">The polymorphic identifier of the new owning entity.</param>
	/// <param name="utcNow">
	/// The UTC timestamp to record as the creation time of the cloned references, or <see langword="null"/> to
	/// use the service's configured <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of <see cref="ResourceReferenceMetadata"/> for the newly created references,
	/// containing the new public identifiers. Empty if none of the source references were found.
	/// </returns>
	/// <remarks>
	/// This is used when the AI completion pipeline echoes user attachments back as part of the AI response.
	/// The AI message needs its own resource references so that loading messages by owner returns the
	/// correct attachments for both the user message and the AI message independently.
	/// <para>
	/// New <see cref="ResourceReferenceEntity.PublicId"/> values are generated per call. On the
	/// (lottery-grade improbable) event of a uniqueness collision, the implementation transparently
	/// retries with fresh GUIDs up to a small bounded number of attempts before surfacing the failure.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="sourceReferencePublicIds"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
	/// All retry attempts to allocate unique <see cref="ResourceReferenceEntity.PublicId"/> values were
	/// exhausted, or the database rejected the insert for an unrecoverable reason. Statistically near-
	/// impossible with a healthy RNG; indicates a compromised entropy source on the host.
	/// </exception>
	Task<IReadOnlyList<ResourceReferenceMetadata>> CloneResourceReferencesAsync(
		IEnumerable<Guid> sourceReferencePublicIds,
		ResourceOwnerKind ownerKind,
		ResourceOwnerId   ownerId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default);

	#endregion
}
