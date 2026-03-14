// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a conversation thread between participants.
/// </summary>
/// <remarks>
///     <para>
///     A conversation is a container for messages exchanged between one or more participants, which can be
///     users, personas, or a mix of both. This enables one-on-one chats, group conversations, and AI roundtables.
///     </para>
///     <para>
///     The <see cref="Title"/> can be auto-generated from the first message or manually set by participants.
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
///             <see cref="PublicId"/> is intended for stable external references.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Indexing:</b> <see cref="UpdatedAtUtc"/> is indexed to support listing conversations by last activity.
///     </para>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class ConversationEntity
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
	public ConversationId Id { get; set; }

	/// <summary>
	/// Gets or sets the public unique identifier for external references.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Exposed via APIs, used in URLs, and remains stable across database migrations.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public Guid PublicId { get; set; }

	/// <summary>
	/// Gets or sets the human-readable title of this conversation.
	/// </summary>
	/// <remarks>
	/// Can be auto-generated from the first message or manually set by participants.
	/// The database enforces a maximum length.
	/// </remarks>
	public string Title { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the UTC timestamp when this conversation was created.
	/// </summary>
	/// <remarks>
	/// Set when the conversation is first created.
	/// </remarks>
	public DateTime CreatedAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this conversation was last updated.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Updated whenever a new message is added or conversation metadata changes.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index to support sorting conversation lists by last activity.
	///     </para>
	/// </remarks>
	public DateTime UpdatedAtUtc { get; set; }

	/// <summary>
	/// Gets the collection of messages in this conversation.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     Typically queried ordered by <see cref="MessageEntity.CreatedAtUtc"/>.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<MessageEntity> Messages { get; set; } = [];

	/// <summary>
	/// Gets the collection of participants in this conversation.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     Membership details (role, join timestamp) are stored in <see cref="ConversationParticipantEntity"/>.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<ConversationParticipantEntity> Participants { get; set; } = [];
}
