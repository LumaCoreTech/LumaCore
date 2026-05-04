// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a reference from an owning configuration entity (message, user, persona) to a
/// <see cref="ResourceEntity"/>.
/// </summary>
/// <remarks>
///     <para>
///     Resource references provide the indirection layer between domain entities and physical files. Multiple
///     references can point to the same <see cref="ResourceEntity"/> (deduplication), and each reference can
///     carry its own <see cref="OriginalFileName"/> and <see cref="ContentType"/> — the same physical image
///     might be referenced as <c>avatar.png</c> by one participant and <c>profile.png</c> by another.
///     </para>
///     <para>
///         <b>Polymorphic ownership:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="OwnerKind"/> identifies the table that owns this reference
///             (see <see cref="ResourceOwnerKind"/>).
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="OwnerId"/> is the raw <see cref="long"/> primary key of the owning entity.
///             No database-level foreign key is enforced; referential integrity is maintained by the application
///             via pre-CASCADE cleanup in the owning entity's deletion flow.
///             </description>
///         </item>
///         <item>
///             <description>
///             Owners must be <em>configuration</em> entities whose deletion is the canonical signal that the
///             resource is no longer needed (e.g. <see cref="UserEntity"/>, <see cref="PersonaEntity"/>,
///             <see cref="MessageEntity"/>). Identity-style entities that are retained past their
///             configuration lifetime — notably <see cref="ParticipantEntity"/>, which survives user-account
///             deletion to preserve chat history — must <strong>never</strong> own a reference, because doing
///             so would leak personal data past its deletion boundary.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Download URL:</b> The <see cref="PublicId"/> is exposed as
///     <c>GET /api/resource-references/{publicId}</c>. Using a non-sequential GUID prevents enumeration attacks.
///     </para>
///     <para>
///     <b>Deletion semantics:</b> When an owning entity is about to be deleted (e.g. message, participant, user
///     account), the application must delete the corresponding resource references <em>before</em> the CASCADE
///     runs. The GC then reclaims any orphaned <see cref="ResourceEntity"/> rows during its next cycle.
///     </para>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class ResourceReferenceEntity
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
	public ResourceReferenceId Id { get; set; }

	// --- 2. Public identifier ---

	/// <summary>
	/// Gets or sets the public unique identifier for external references and download URLs.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Exposed via APIs as part of the download URL (<c>GET /api/resource-references/{publicId}</c>).
	///     Non-sequential to prevent enumeration attacks.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public Guid PublicId { get; set; }

	// --- 3. Foreign keys + Navigation properties ---

	/// <summary>
	/// Gets or sets the identifier of the referenced <see cref="ResourceEntity"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Foreign key to <see cref="ResourceEntity"/>. Uses <c>Cascade</c> delete behavior — when the resource
	///     row is removed (GC sweep), all its references are automatically deleted.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index for resource lookups and orphan detection.
	///     </para>
	/// </remarks>
	public ResourceId ResourceId { get; set; }

	/// <summary>
	/// Gets or sets the referenced resource.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ResourceEntity Resource { get; set; } = null!;

	/// <summary>
	/// Gets or sets the kind of entity that owns this reference.
	/// </summary>
	/// <remarks>
	/// Discriminator for the polymorphic <see cref="OwnerId"/> column.
	/// See <see cref="ResourceOwnerKind"/> for the supported owner types.
	/// </remarks>
	public ResourceOwnerKind OwnerKind { get; set; }

	/// <summary>
	/// Gets or sets the primary key of the owning entity.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The <see cref="ResourceOwnerId"/> wraps the underlying <see cref="long"/> primary key. Its
	///     interpretation depends on <see cref="OwnerKind"/>: for <see cref="ResourceOwnerKind.Message"/>
	///     this is a <see cref="MessageId"/> value, for <see cref="ResourceOwnerKind.User"/> a
	///     <see cref="UserId"/> value, for <see cref="ResourceOwnerKind.Persona"/> a <see cref="PersonaId"/>
	///     value.
	///     </para>
	///     <para>
	///     Use <see cref="ResourceOwnerId.Unassigned"/> to mark a reference as <em>pending</em> — created
	///     before the owning entity's INSERT and promoted later by
	///     <see cref="Services.IResourceDataService.AssignPendingReferencesToOwnerAsync"/> in a single
	///     set-based update once the owning entity's primary key is known.
	///     </para>
	///     <para>
	///     No database-level foreign key is enforced. Referential integrity is maintained by the application
	///     via pre-CASCADE cleanup in deletion flows.
	///     </para>
	///     <para>
	///     <b>Index:</b> Composite index on <c>(OwnerKind, OwnerId)</c> for efficient per-owner lookups.
	///     </para>
	/// </remarks>
	public ResourceOwnerId OwnerId { get; set; }

	// --- 4. Timestamps ---

	/// <summary>
	/// Gets or sets the UTC timestamp when this reference was created.
	/// </summary>
	public DateTime CreatedAtUtc { get; set; }

	// --- 5. Scalar domain fields ---

	/// <summary>
	/// Gets or sets the MIME content type of the resource as perceived by this reference.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Stored per-reference rather than per-resource because the same physical file could be served
	///     with different content types in edge cases (e.g. <c>application/octet-stream</c> vs. detected type).
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.ResourceContentTypeMaxLength"/>.
	///     </para>
	/// </remarks>
	public string ContentType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the original file name provided by the uploader.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Preserved for display purposes (e.g. download dialog). Different references to the same physical
	///     resource may have different original file names.
	///     </para>
	///     <para>
	///     <see langword="null"/> when the uploader did not provide a file name.
	///     Maximum length: <see cref="EntityLimits.ResourceOriginalFileNameMaxLength"/>.
	///     </para>
	/// </remarks>
	public string? OriginalFileName { get; set; }

	// --- 6. Collection navigation properties (none) ---
}
