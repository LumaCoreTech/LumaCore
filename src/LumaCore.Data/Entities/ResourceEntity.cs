// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a stored file (image, document, etc.) managed by the resource storage system.
/// </summary>
/// <remarks>
///     <para>
///     Each row maps to exactly one physical file on the filesystem, identified by <see cref="StoragePath"/>.
///     Files use GUID-based filenames — every entity owns its file exclusively, eliminating shared filesystem
///     state and race conditions between concurrent uploads and garbage collection.
///     </para>
///     <para>
///     <b>Content-hash deduplication:</b> A composite unique index on
///     <c>(<see cref="ContentHash"/>, <see cref="DeletionState"/>)</c> ensures that only one
///     <see cref="ResourceDeletionState.Active"/> row exists per content hash. Duplicate uploads
///     reuse the existing <see cref="ResourceDeletionState.Active"/> resource by adding a new
///     <see cref="ResourceReferenceEntity"/> instead of storing the file again.
///     </para>
///     <para>
///         <b>Garbage collection lifecycle:</b>
///     </para>
///     <list type="number">
///         <item>
///             <description>
///             <b>MARK:</b> Resources with zero references and <see cref="DeletionState"/> =
///             <see cref="ResourceDeletionState.Active"/> are promoted to
///             <see cref="ResourceDeletionState.PendingDeletion"/> after a configurable grace period.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>SWEEP:</b> For each <see cref="ResourceDeletionState.PendingDeletion"/> row, the physical file
///             is deleted first, then the database row is removed. File-first ordering preserves the database
///             row as recovery metadata if the file deletion fails.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Relationships:</b> Resources are referenced via <see cref="ResourceReferenceEntity"/>. A resource
///     without references is considered orphaned and eligible for garbage collection.
///     </para>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class ResourceEntity
{
	// --- 1. Primary key ---

	/// <summary>
	/// Gets or sets the internal unique identifier for database relationships.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Auto-incremented by the database. Never exposed via APIs.
	///     </para>
	///     <para>
	///     <b>Index:</b> Primary key.
	///     </para>
	/// </remarks>
	public ResourceId Id { get; set; }

	// --- 2. Public identifier (none) ---

	// --- 3. Foreign keys + Navigation properties ---

	/// <summary>
	/// Gets or sets the identifier of the participant who created this resource.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The creator may be a human user or an AI persona — both are represented uniformly by
	///     <see cref="ParticipantEntity"/>. <see langword="null"/> indicates a system-initiated upload
	///     (no attributable participant) or that the originating participant has been deleted
	///     (FK uses <c>SetNull</c> behavior).
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index for per-participant resource queries.
	///     </para>
	/// </remarks>
	public ParticipantId? CreatedByParticipantId { get; set; }

	/// <summary>
	/// Gets or sets the participant who created this resource.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <see langword="null"/> for system-initiated uploads, when the participant has been deleted,
	///     or when the navigation is not loaded. Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ParticipantEntity? CreatedByParticipant { get; set; }

	// --- 4. Timestamps ---

	/// <summary>
	/// Gets or sets the UTC timestamp when this resource was uploaded.
	/// </summary>
	/// <remarks>
	/// Used by the GC grace period check: resources younger than the grace period are not marked for deletion,
	/// even if they have zero references (the uploader may still be attaching them).
	/// </remarks>
	public DateTime CreatedAtUtc { get; set; }

	// --- 5. Scalar domain fields ---

	/// <summary>
	/// Gets or sets the SHA-256 hash of the file content, stored as a lowercase hex string (64 characters).
	/// </summary>
	/// <remarks>
	///     <para>
	///     Used for content-addressable deduplication. On upload, the hash is computed and compared against
	///     existing <see cref="ResourceDeletionState.Active"/> rows. If a match is found, the existing resource
	///     is reused and the duplicate file is discarded.
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.Sha256HexLength"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Composite unique index on <c>(ContentHash, DeletionState)</c>. This allows at most one
	///     <see cref="ResourceDeletionState.Active"/> and one <see cref="ResourceDeletionState.PendingDeletion"/>
	///     row per hash, separating upload and GC operations without row contention.
	///     </para>
	/// </remarks>
	public string ContentHash { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the relative path to the physical file within the storage root directory.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The path is a GUID-based filename (e.g. <c>a1b2c3d4-e5f6-7890-abcd-ef1234567890</c>), optionally
	///     prefixed with a directory shard (e.g. <c>a1/a1b2c3d4-...</c>). No file extension is stored —
	///     the content type is tracked on <see cref="ResourceReferenceEntity.ContentType"/>.
	///     </para>
	///     <para>
	///     Each resource entity owns its file exclusively. GUID filenames ensure that no two entities
	///     reference the same physical file, even when content hashes collide across deletion states.
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.ResourceStoragePathMaxLength"/>.
	///     </para>
	/// </remarks>
	public string StoragePath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the size of the stored file in bytes.
	/// </summary>
	public long SizeBytes { get; set; }

	/// <summary>
	/// Gets or sets the deletion state for garbage collection.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Defaults to <see cref="ResourceDeletionState.Active"/> for newly uploaded resources. The GC MARK phase
	///     promotes orphaned resources to <see cref="ResourceDeletionState.PendingDeletion"/>; the SWEEP phase
	///     deletes the file and then removes the row.
	///     </para>
	///     <para>
	///     <b>Index:</b> Part of composite unique index on <c>(ContentHash, DeletionState)</c>.
	///     Also indexed individually for GC MARK/SWEEP queries.
	///     </para>
	/// </remarks>
	public ResourceDeletionState DeletionState { get; set; }

	// --- 6. Collection navigation properties ---

	/// <summary>
	/// Gets the collection of references pointing to this resource.
	/// </summary>
	/// <remarks>
	///     <para>
	///     A resource with an empty collection is considered orphaned and eligible for garbage collection
	///     (after the grace period expires).
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<ResourceReferenceEntity> References { get; set; } = [];
}
