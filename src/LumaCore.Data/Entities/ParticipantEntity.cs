// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a participant that can engage in conversations.
/// </summary>
/// <remarks>
///     <para>
///     This is the base entity for both users and personas. It provides a unified identity that can be referenced
///     as a message sender or conversation participant, regardless of whether the participant is human or AI.
///     </para>
///     <para>
///         <b>Identifiers:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="Id"/> is the internal identifier used for database relationships.
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="PublicId"/> is exposed via APIs and remains stable even if the database is restructured.
///             </description>
///         </item>
///     </list>
///     <para>
///         <b>Relationships:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             A participant may be linked to exactly one <see cref="UserEntity"/> or exactly one
///             <see cref="PersonaEntity"/> via a 1:1 relationship.
///             </description>
///         </item>
///         <item>
///             <description>
///             The <see cref="User"/> and <see cref="Persona"/> navigation properties indicate whether the participant
///             represents a human user or an AI persona.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Deletion semantics:</b> Messages may reference participants. If a participant is removed, the message sender
///     foreign key uses <c>DeleteBehavior.SetNull</c> so that conversation history can be preserved.
///     </para>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class ParticipantEntity
{
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
	public ParticipantId Id { get; set; }

	/// <summary>
	/// Gets or sets the public unique identifier for external references.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This identifier is exposed via APIs, used in URLs, and remains stable across database migrations.
	///     It is not predictable, providing a layer of security against enumeration attacks.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public Guid PublicId { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this participant was created.
	/// </summary>
	public DateTime CreatedAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the display name of this participant.
	/// </summary>
	/// <remarks>
	/// For users, this might be their chosen display name. For personas, this is the character name.
	/// The database enforces a maximum length.
	/// </remarks>
	public string DisplayName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the URL to the participant's avatar image.
	/// </summary>
	/// <remarks>
	/// Can be a relative path to a local asset or an absolute URL to an external image.
	/// The database enforces a maximum length.
	/// </remarks>
	public string? AvatarUrl { get; set; }

	/// <summary>
	/// Gets or sets the persona linked to this participant, if any.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	///     <para>
	///     A participant is either a <see cref="User"/> or a <see cref="Persona"/>, never both.
	///     If this property is not <see langword="null"/>, this participant represents an AI persona.
	///     </para>
	/// </remarks>
	public PersonaEntity? Persona { get; set; }

	/// <summary>
	/// Gets or sets the user linked to this participant, if any.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	///     <para>
	///     A participant is either a <see cref="User"/> or a <see cref="Persona"/>, never both.
	///     If this property is not <see langword="null"/>, this participant represents a human user.
	///     </para>
	/// </remarks>
	public UserEntity? User { get; set; }

	/// <summary>
	/// Gets the collection of conversation participations.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<ConversationParticipantEntity> ConversationParticipants { get; set; } = [];

	/// <summary>
	/// Gets the collection of messages sent by this participant.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	///     <para>
	///     This collection can become large. Avoid loading full histories into memory; prefer filtering/paging via
	///     dedicated message queries (e.g. querying <see cref="LumaCoreDbContext.Messages"/> with
	///     <c>Where</c>/<c>OrderBy</c>/<c>Take</c>) and project only the fields required.
	///     </para>
	///     <para>
	///     Messages are retained for history/audit.
	///     In typical deployments, participants are kept even if a linked user account is deleted, so that
	///     conversation participant lists and message history remain consistent.
	///     </para>
	/// </remarks>
	public ICollection<MessageEntity> Messages { get; set; } = [];
}
