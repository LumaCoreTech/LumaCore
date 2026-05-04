// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;
using LumaCore.Data.Entities;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LumaCore.Data.Services;

/// <summary>
/// Application-layer orchestration for resource upload, download, and reference management.
/// </summary>
/// <remarks>
///     <para>
///     This service coordinates between the database (<see cref="LumaCoreDbContext"/>) and the filesystem
///     (<see cref="IResourceStore"/>) to provide content-hash deduplication, reference tracking, and
///     safe pre-CASCADE cleanup for deletion flows.
///     </para>
///     <para>
///     <b>Upload flow:</b> The caller provides a stream and metadata. The service computes the SHA-256 hash,
///     checks for an existing <see cref="ResourceDeletionState.Active"/> resource with the same hash (dedup),
///     and either reuses the existing resource or persists a new file. A <see cref="ResourceReferenceEntity"/>
///     is always created to link the resource to its owning entity.
///     </para>
///     <para>
///     <b>Download flow:</b> The caller provides the public GUID of a reference. The service resolves
///     the reference, joins to the resource for the storage path and size, and returns the metadata needed
///     to serve the file.
///     </para>
///     <para>
///     <b>Deletion flow:</b> Before a domain entity is deleted (message, participant, persona), all its
///     resource references must be removed so the GC can reclaim orphaned resources. This pre-CASCADE
///     cleanup is handled by <see cref="DeleteReferencesByOwnerAsync"/>.
///     </para>
/// </remarks>
public interface IResourceService
{
	/// <summary>
	/// Uploads a resource, deduplicates by content hash, and creates a reference linking
	/// the resource to the specified owner.
	/// </summary>
	/// <param name="content">
	/// A readable stream containing the file content. The stream is read to completion;
	/// the caller retains ownership and is responsible for disposing it.
	/// </param>
	/// <param name="ownerKind">The kind of entity that owns this resource reference.</param>
	/// <param name="ownerId">
	/// The polymorphic identifier of the owning entity (interpreted based on <paramref name="ownerKind"/>).
	/// </param>
	/// <param name="contentType">The MIME content type of the uploaded file.</param>
	/// <param name="createdByParticipantId">
	/// The identifier of the participant performing the upload (a user or persona), or
	/// <see langword="null"/> for system-initiated uploads.
	/// </param>
	/// <param name="utcNow">
	/// The UTC timestamp to record as the creation time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="originalFileName">
	/// The original file name provided by the uploader, or <see langword="null"/> if not available.
	/// When supplied, must be non-empty/non-whitespace, no longer than
	/// <see cref="EntityLimits.ResourceOriginalFileNameMaxLength"/> characters, and must not contain
	/// path separators (<c>/</c>, <c>\</c>) or NUL characters — the value is purely descriptive
	/// metadata and is never used for filesystem path construction.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A <see cref="ResourceUploadResult"/> describing the outcome of the upload.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="content"/> or <paramref name="contentType"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="contentType"/> is empty or whitespace-only, or <paramref name="originalFileName"/>
	/// is supplied but is empty/whitespace, exceeds the allowed length, or contains path separators or
	/// NUL characters.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// A <see cref="Guid.NewGuid"/> collision was detected on either
	/// <see cref="ResourceEntity.StoragePath"/> or <see cref="ResourceReferenceEntity.PublicId"/>.
	/// Statistically impossible with a healthy RNG; indicates a compromised entropy source on the
	/// host.
	/// </exception>
	/// <exception cref="DbUpdateException">
	/// The database rejected the insert for a reason that could not be recovered from (e.g. an
	/// unrecognized conflict, an FK violation against state that was concurrently mutated, or a
	/// hash-race-winner that was itself promoted before the dedup lock could be acquired). The
	/// caller is expected to surface or retry this at its own discretion.
	/// </exception>
	/// <remarks>
	///     <para>
	///     <b>Ambient transaction contract:</b> when the caller has already opened a transaction on
	///     the underlying <see cref="LumaCoreDbContext"/>, the upload writes the file
	///     <em>before</em> the resource row is committed. To avoid leaking that file if the outer
	///     transaction is rolled back, the caller's transaction must be an
	///     <see cref="ICompensatingTransaction"/> obtained from
	///     <see cref="LumaCoreDbContext.BeginCompensatingTransactionAsync"/>. A plain
	///     <see cref="DatabaseFacade.BeginTransactionAsync(CancellationToken)"/> would commit/roll
	///     back the database state correctly but would NOT fire the orphan-file cleanup
	///     compensation, leaving the file on disk until the next garbage-collector cycle catches it
	///     via the resource row (which, in this case, never landed — so the file would leak
	///     permanently). Implementations are expected to enforce this contract by aborting the
	///     process immediately when a non-compensating ambient transaction is detected, rather than
	///     risk silent data corruption.
	///     </para>
	///     <para>
	///     <b>Concurrency:</b> implementations must tolerate concurrent uploads of the same
	///     content hash and concurrent resource cleanup activity. Callers never observe a dangling
	///     <see cref="ResourceUploadResult.ReferencePublicId"/> (one whose underlying file has been
	///     swept or whose dedup target has been promoted to
	///     <see cref="ResourceDeletionState.PendingDeletion"/>): either the upload completes
	///     successfully, or an exception propagates.
	///     </para>
	/// </remarks>
	Task<ResourceUploadResult> UploadAsync(
		Stream            content,
		ResourceOwnerKind ownerKind,
		ResourceOwnerId   ownerId,
		string            contentType,
		ParticipantId?    createdByParticipantId,
		DateTime?         utcNow            = null,
		string?           originalFileName  = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Resolves a resource reference by its public identifier and returns the metadata needed to serve the file.
	/// </summary>
	/// <param name="publicId">The public GUID of the <see cref="ResourceReferenceEntity"/>.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A <see cref="ResourceDownloadInfo"/> if the reference exists; otherwise, <see langword="null"/>.
	/// </returns>
	Task<ResourceDownloadInfo?> GetDownloadInfoAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes all resource references owned by the specified entity.
	/// </summary>
	/// <param name="ownerKind">The kind of the owning entity.</param>
	/// <param name="ownerId">The polymorphic identifier of the owning entity.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of resource references that were deleted.</returns>
	/// <remarks>
	///     <para>
	///     Call this <em>before</em> deleting the owning entity to ensure pre-CASCADE cleanup. The GC will
	///     reclaim any orphaned <see cref="ResourceEntity"/> rows during its next cycle.
	///     </para>
	///     <para>
	///     <b>Idempotent:</b> calling this method on an owner that has no references (already cleaned up,
	///     never had any, or owner does not exist) is a no-op and returns <c>0</c>. This makes the method
	///     safe to invoke as a defensive pre-CASCADE step regardless of prior cleanup state.
	///     </para>
	///     <para>
	///     <b>Ambient transaction:</b> when invoked under an existing transaction on the underlying
	///     <see cref="LumaCoreDbContext"/>, the deletion participates in that transaction and is rolled
	///     back atomically with it. No file IO is performed by this method — orphaned
	///     <see cref="ResourceEntity"/> rows and their physical files are reclaimed asynchronously by the
	///     resource garbage collector — so no compensating transaction is required.
	///     </para>
	/// </remarks>
	Task<int> DeleteReferencesByOwnerAsync(
		ResourceOwnerKind ownerKind,
		ResourceOwnerId   ownerId,
		CancellationToken cancellationToken = default);
}
