// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// Provides message related database operations.
/// </summary>
public interface IMessageDataService
{
	#region Read APIs

	/// <summary>
	/// Looks up a single message by its public identifier.
	/// </summary>
	/// <param name="publicId">The public (client-facing) identifier of the message.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The matching <see cref="MessageEntity"/>, or <see langword="null"/> if no message with the given
	/// <paramref name="publicId"/> exists.
	/// </returns>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	/// <remarks>
	///     <para>
	///     The returned entity has its <see cref="MessageEntity.Sender"/> navigation populated (or
	///     <see langword="null"/> for system messages) so REST callers can render the message without a
	///     follow-up roundtrip. The entity is returned untracked.
	///     </para>
	///     <para>
	///     This method is backed by an EF Core compiled query and therefore <paramref name="cancellationToken"/>
	///     cancellation is best-effort: the awaiting caller stops, but the underlying database operation may
	///     still run to completion.
	///     </para>
	/// </remarks>
	Task<MessageEntity?> GetMessageByPublicIdAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists messages for a conversation ordered by creation time (oldest first) with optional paging.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="offset">The number of messages to skip from the start of the ordered list. Defaults to 0.</param>
	/// <param name="limit">
	/// The maximum number of messages to return. Defaults to <see cref="int.MaxValue"/> (all messages).
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A <see cref="MessagePage"/> containing the messages for the requested page, the paging coordinates
	/// (<paramref name="offset"/> / <paramref name="limit"/> echoed back as <see cref="MessagePage.Offset"/> /
	/// <see cref="MessagePage.Limit"/>), and the total number of messages in the conversation.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> is less than or equal to 0, <paramref name="offset"/> is negative,
	/// or <paramref name="limit"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// The returned messages have their <see cref="MessageEntity.Sender"/> navigation populated
	/// (or <see langword="null"/> for system messages). Entities are returned untracked.
	/// </remarks>
	Task<MessagePage> ListMessagesByConversationAsync(
		ConversationId    conversationId,
		int               offset            = 0,
		int               limit             = int.MaxValue,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists the most recent messages for a conversation.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="limit">The maximum number of messages to return.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The messages (newest first).</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> or <paramref name="limit"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// The returned messages have their <see cref="MessageEntity.Sender"/> navigation populated
	/// (or <see langword="null"/> for system messages). Entities are returned untracked.
	/// </remarks>
	Task<IReadOnlyList<MessageEntity>> ListRecentMessagesByConversationAsync(
		ConversationId    conversationId,
		int               limit,
		CancellationToken cancellationToken = default);

	#endregion

	#region Mutation APIs

	/// <summary>
	/// Creates a new message.
	/// </summary>
	/// <param name="conversationId">The identifier of the conversation the message belongs to.</param>
	/// <param name="senderParticipantId">The identifier of the sending participant.</param>
	/// <param name="content">
	/// The message content, or <see langword="null"/> for attachment-only messages. Non-null values are trimmed;
	/// empty/whitespace strings are normalized to <see langword="null"/>.
	/// </param>
	/// <param name="utcNow">
	/// The timestamp to store as creation time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="publicId">
	/// An explicit public identifier to assign to the message, or <see langword="null"/> to auto-generate one.
	/// Callers that broadcast a preliminary message id (e.g. during SignalR streaming) before persisting can pass
	/// the same id here to keep the persisted entity consistent with what clients already received.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The created <see cref="MessageEntity"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> or <paramref name="senderParticipantId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="publicId"/> is <see cref="Guid.Empty"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The referenced conversation does not exist, the sender participant does not exist, or the sender is
	/// not a member of the conversation.
	/// </exception>
	/// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
	/// Persisting the message fails. This can happen when the conversation, sender, or membership row is removed
	/// between the pre-check and the insert (FK violation), when a caller-supplied <paramref name="publicId"/>
	/// collides with an existing message, or for the usual reasons (connection loss, provider errors).
	/// </exception>
	Task<MessageEntity> CreateMessageAsync(
		ConversationId    conversationId,
		ParticipantId     senderParticipantId,
		string?           content,
		DateTime?         utcNow            = null,
		Guid?             publicId          = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates generation metadata for an existing message.
	/// </summary>
	/// <param name="messageId">The internal message identifier.</param>
	/// <param name="metadata">The metadata to persist. <see cref="MessageGenerationMetadataEntity.MessageId"/> is ignored.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The created <see cref="MessageGenerationMetadataEntity"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="metadata"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="messageId"/> is less than or equal to 0, or
	/// <paramref name="metadata"/>.<see cref="MessageGenerationMetadataEntity.ModelEndpointId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="InvalidOperationException">The referenced message does not exist.</exception>
	/// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
	/// Persisting the metadata fails. This can happen when the referenced message is removed between the pre-check
	/// and the insert (FK violation), when <see cref="MessageGenerationMetadataEntity.ModelEndpointId"/> does not
	/// refer to an existing endpoint (FK violation), or for the usual reasons (connection loss, provider errors).
	/// </exception>
	/// <remarks>
	/// If <see cref="DatabaseOptions.StoreFullPrompts"/> is <see langword="false"/>,
	/// <see cref="MessageGenerationMetadataEntity.FullPrompt"/> will be pruned before persisting.
	/// </remarks>
	Task<MessageGenerationMetadataEntity> CreateMessageGenerationMetadataAsync(
		MessageId                       messageId,
		MessageGenerationMetadataEntity metadata,
		CancellationToken               cancellationToken = default);

	/// <summary>
	/// Creates a platform-generated system message (e.g., a join or leave notice) with no sender.
	/// </summary>
	/// <param name="conversationId">The identifier of the conversation the message belongs to.</param>
	/// <param name="content">The system message text. Must not be <see langword="null"/> or empty after trimming.</param>
	/// <param name="utcNow">
	/// The timestamp to store as creation time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The created <see cref="MessageEntity"/> with <see cref="MessageEntity.Type"/> set to
	/// <see cref="MessageType.System"/> and <see cref="MessageEntity.SenderId"/> set to
	/// <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="content"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="content"/> is empty/whitespace after trimming.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The referenced conversation does not exist.
	/// </exception>
	/// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
	/// Persisting the message fails. This can happen when the conversation is removed between the pre-check and the
	/// insert (FK violation), or for the usual reasons (connection loss, provider errors).
	/// </exception>
	Task<MessageEntity> CreateSystemMessageAsync(
		ConversationId    conversationId,
		string?           content,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Redacts the content of a message while preserving conversation structure.
	/// </summary>
	/// <param name="messageId">The internal message identifier.</param>
	/// <param name="reason">The reason why the message content is being redacted.</param>
	/// <param name="utcNow">
	/// The timestamp to persist as the redaction moment, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if a message was found and updated; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="messageId"/> is less than or equal to 0.</exception>
	Task<bool> RedactMessageAsync(
		MessageId              messageId,
		MessageRedactionReason reason,
		DateTime?              utcNow            = null,
		CancellationToken      cancellationToken = default);

	/// <summary>
	/// Redacts a message only if it was authored by the specified participant.
	/// </summary>
	/// <param name="messageId">The internal message identifier.</param>
	/// <param name="authorParticipantId">The identifier of the expected author participant.</param>
	/// <param name="utcNow">
	/// The timestamp to persist as the redaction moment, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the message existed and was redacted by this call with reason
	/// <see cref="MessageRedactionReason.UserRequestedDeletion"/>; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="messageId"/> or <paramref name="authorParticipantId"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// Intended for user-driven deletion flows where a mismatch (wrong author, already redacted, not found) is a
	/// normal outcome and should not be treated as an exceptional condition.
	/// </remarks>
	Task<bool> RedactMessageByAuthorAsync(
		MessageId         messageId,
		ParticipantId     authorParticipantId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Redacts all messages authored by a specific participant.
	/// </summary>
	/// <param name="participantId">The identifier of the participant whose messages should be redacted.</param>
	/// <param name="reason">The reason why the message contents are being redacted.</param>
	/// <param name="utcNow">
	/// The timestamp to persist as the redaction moment, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of messages updated.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="participantId"/> is less than or equal to 0.</exception>
	/// <remarks>
	/// Messages that have already been redacted (i.e. <see cref="MessageEntity.RedactedAtUtc"/> is non-null) are
	/// skipped to preserve their original <see cref="MessageEntity.RedactionReason"/>. Calling this method twice
	/// for the same participant is therefore idempotent: the second call will return 0.
	/// </remarks>
	Task<int> RedactMessagesByParticipantAsync(
		ParticipantId          participantId,
		MessageRedactionReason reason,
		DateTime?              utcNow            = null,
		CancellationToken      cancellationToken = default);

	#endregion
}
