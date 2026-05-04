// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Discriminator for the polymorphic owner of a <see cref="ResourceReferenceEntity"/>.
/// </summary>
/// <remarks>
///     <para>
///     Because resource references use a single <see cref="ResourceReferenceEntity.OwnerId"/> column
///     to point at different entity tables, this enum identifies which table the owner belongs to.
///     The application layer interprets <see cref="ResourceReferenceEntity.OwnerId"/> based on this value.
///     </para>
///     <para>
///     <b>Ownership contract:</b> Every owner kind must be a <em>configuration</em> entity whose deletion is
///     the canonical signal that the resource is no longer needed. Identity-style entities that survive
///     deletion of the configuration they represent (such as <see cref="ParticipantEntity"/>, which is
///     retained for chat history after a user closes their account) must <strong>never</strong> own a
///     resource reference — doing so would leak personal data past the deletion boundary and violate data
///     minimization (GDPR). Profile pictures therefore anchor to <see cref="User"/>, not to the participant.
///     </para>
///     <para>
///     No database-level foreign key is enforced on the polymorphic <see cref="ResourceReferenceEntity.OwnerId"/>
///     column. Referential integrity is maintained by the application via pre-CASCADE cleanup in deletion
///     flows of the owning configuration entity.
///     </para>
/// </remarks>
public enum ResourceOwnerKind
{
	/// <summary>
	/// The resource reference is owned by a <see cref="MessageEntity"/> (e.g. an uploaded attachment).
	/// <see cref="ResourceReferenceEntity.OwnerId"/> contains a <see cref="MessageId"/> value.
	/// </summary>
	/// <remarks>
	/// Lifecycle: the reference is removed when the message is deleted (either explicitly by the user or
	/// transitively when the conversation is deleted). The GC then reclaims the underlying
	/// <see cref="ResourceEntity"/> if no other reference still points to it.
	/// </remarks>
	Message = 1,

	/// <summary>
	/// The resource reference is owned by a <see cref="UserEntity"/> (e.g. the user's profile picture).
	/// <see cref="ResourceReferenceEntity.OwnerId"/> contains a <see cref="UserId"/> value.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Lifecycle: the reference must be removed as part of <see cref="UserEntity"/> deletion so that
	///     personal data (the user's facial image) does not persist beyond account closure. The
	///     <see cref="ParticipantEntity"/> identity is intentionally retained for historical chat data and
	///     therefore is <strong>not</strong> a valid resource owner — anchoring profile pictures to the user
	///     ensures data-minimization (GDPR) compliance via the user-deletion cleanup path.
	///     </para>
	/// </remarks>
	User = 2,

	/// <summary>
	/// The resource reference is owned by a <see cref="PersonaEntity"/> (e.g. the persona's avatar).
	/// <see cref="ResourceReferenceEntity.OwnerId"/> contains a <see cref="PersonaId"/> value.
	/// </summary>
	/// <remarks>
	/// Lifecycle: the reference is removed when the persona is deleted. Because the persona is a pure
	/// configuration entity (no historical retention requirement), its deletion is the natural end of life
	/// for its avatar.
	/// </remarks>
	Persona = 3
}
