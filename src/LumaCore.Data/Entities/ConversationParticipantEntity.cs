// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a participant's membership in a conversation.
/// </summary>
/// <remarks>
///     <para>
///     This is a join entity for the many-to-many relationship between conversations and participants.
///     It includes the participant's role within the conversation and when they joined.
///     </para>
///     <para>
///     The <see cref="Role"/> determines what actions the participant can perform within this specific
///     conversation, such as adding other participants or deleting the conversation.
///     </para>
///     <para>
///         <b>Keys:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             The primary key is composite: <see cref="ConversationId"/> + <see cref="ParticipantId"/>.
///             This means a participant can only appear once per conversation.
///             </description>
///         </item>
///     </list>
///     <para>
///     Database relationships, keys, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class ConversationParticipantEntity
{
	/// <summary>
	/// Gets or sets the foreign key to the conversation.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Part of the composite primary key.
	///     </para>
	///     <para>
	///     <b>Index:</b> Composite primary key <c>(ConversationId, ParticipantId)</c>.
	///     </para>
	///     <para>
	///     Points to <see cref="ConversationEntity.Id"/>.
	///     </para>
	/// </remarks>
	public ConversationId ConversationId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the conversation.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     This relationship is required at the database level via <see cref="ConversationId"/>,
	///     but the navigation may be <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ConversationEntity? Conversation { get; set; }

	/// <summary>
	/// Gets or sets the foreign key to the participant.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Part of the composite primary key.
	///     Points to <see cref="ParticipantEntity.Id"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Composite primary key <c>(ConversationId, ParticipantId)</c>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index to support listing conversation membership by participant.
	///     </para>
	/// </remarks>
	public ParticipantId ParticipantId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the participant.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     This relationship is required at the database level via <see cref="ParticipantId"/>,
	///     but the navigation may be <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ParticipantEntity? Participant { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this participant joined the conversation.
	/// </summary>
	public DateTime JoinedAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the participant's role within this conversation.
	/// </summary>
	/// <remarks>
	/// Determines permissions within the conversation context. The <see cref="ConversationParticipantRole.Owner"/>
	/// is typically the participant who initiated the conversation.
	/// </remarks>
	public ConversationParticipantRole Role { get; set; }
}
