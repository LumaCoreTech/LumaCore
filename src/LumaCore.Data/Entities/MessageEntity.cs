// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a single message within a conversation.
/// </summary>
/// <remarks>
///     <para>
///     Messages are the core content unit of conversations. Each message has a sender (user or persona)
///     and textual content. For AI-generated messages, additional metadata is stored in
///     <see cref="MessageGenerationMetadataEntity"/>.
///     </para>
///     <para>
///     The structure is compatible with major LLM APIs (OpenAI, Anthropic, Ollama) for seamless
///     integration and history replay.
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
///     <b>Retention:</b>
///     Messages are treated as historical records.
///     The sender relationship is optional and uses <c>DeleteBehavior.SetNull</c> so that message history can outlive
///     a removed participant.
///     </para>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class MessageEntity
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
	public MessageId Id { get; set; }

	// --- 2. Public identifier ---

	/// <summary>
	/// Gets or sets the public unique identifier for external references.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Exposed via APIs and remains stable across database migrations.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public Guid PublicId { get; set; }

	// --- 3. Foreign keys + Navigation properties ---

	/// <summary>
	/// Gets or sets the foreign key to the conversation this message belongs to.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Points to <see cref="ConversationEntity.Id"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Leading column of the composite non-unique index <c>(ConversationId, CreatedAtUtc)</c>.
	///     This supports per-conversation lookups and ordered retrieval within a conversation.
	///     </para>
	/// </remarks>
	public ConversationId ConversationId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the conversation.
	/// </summary>
	/// <remarks>
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
	/// Gets or sets the foreign key to the participant who sent this message.
	/// </summary>
	/// <remarks>
	///     <para>
	///     References <see cref="ParticipantEntity.Id"/>, which can be either a user or a persona.
	///     The foreign key uses <see cref="DeleteBehavior.SetNull"/> to avoid deleting historical messages when a
	///     participant is removed.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index to support filtering messages by sender.
	///     </para>
	/// </remarks>
	public ParticipantId? SenderId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the sender.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This relationship is optional. <see cref="SenderId"/> may be <see langword="null"/> if the original
	///     sender was deleted (the foreign key uses <c>DeleteBehavior.SetNull</c> to preserve message history).
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ParticipantEntity? Sender { get; set; }

	/// <summary>
	/// Gets or sets the generation metadata for AI-generated messages.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Only populated for messages sent by personas; <see langword="null"/> for user messages.
	///     </para>
	///     <para>
	///     The corresponding row uses <see cref="MessageGenerationMetadataEntity.MessageId"/> as both primary key and
	///     foreign key.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public MessageGenerationMetadataEntity? GenerationMetadata { get; set; }

	// --- 4. Timestamps ---

	/// <summary>
	/// Gets or sets the UTC timestamp when this message was created.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Index:</b> Second column of the composite non-unique index <c>(ConversationId, CreatedAtUtc)</c>.
	///     </para>
	///     <para>
	///     This helps chronological retrieval only when the query is already constrained by <see cref="ConversationId"/>.
	///     There is no standalone index on <see cref="CreatedAtUtc"/> for global time-based queries.
	///     </para>
	/// </remarks>
	public DateTime CreatedAtUtc { get; set; }

	// --- 5. Scalar domain fields ---

	/// <summary>
	/// Gets or sets the type of this message.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The CLR default <see cref="MessageType.User"/> covers ordinary participant messages, so callers do
	///     not need to set this property explicitly. Use <see cref="MessageType.System"/> for platform-generated
	///     messages (e.g., "X joined the conversation") that have no <see cref="SenderId"/>.
	///     </para>
	///     <para>
	///     The database stores this as an integer column with the same default.
	///     </para>
	/// </remarks>
	public MessageType Type { get; set; }

	/// <summary>
	/// Gets or sets the textual content of this message.
	/// </summary>
	/// <remarks>
	///     <para>
	///     For user messages, this is the input text. For AI messages, this is the complete generated response.
	///     Content is stored as plain text; the application layer may interpret it as Markdown when rendering.
	///     </para>
	///     <para>
	///     Content may be <see langword="null"/> if it was redacted.
	///     The database does not currently enforce a maximum length; consider application-level limits if needed.
	///     </para>
	/// </remarks>
	// ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
	public string? Content { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this message content was redacted.
	/// </summary>
	/// <remarks>
	/// When set, the message content has been removed (see <see cref="Content"/>).
	/// </remarks>
	public DateTime? RedactedAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the reason why this message content was redacted.
	/// </summary>
	/// <remarks>
	/// The database stores only the reason code to support privacy-first data minimization.
	/// </remarks>
	public MessageRedactionReason? RedactionReason { get; set; }

	// --- 6. Collection navigation properties (none) ---
}
