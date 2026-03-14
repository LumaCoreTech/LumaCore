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
	/// <summary>
	/// Creates a new message.
	/// </summary>
	/// <param name="conversationId">The identifier of the conversation the message belongs to.</param>
	/// <param name="senderParticipantId">The identifier of the sending participant.</param>
	/// <param name="content">The message content. Must not be <see langword="null"/> or empty after trimming.</param>
	/// <param name="utcNow">The timestamp to store as creation time.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The created <see cref="MessageEntity"/>.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="content"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="content"/> is empty/whitespace after trimming.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> or <paramref name="senderParticipantId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The referenced conversation does not exist, the sender participant does not exist, or the sender is
	/// not a member of the conversation.
	/// </exception>
	Task<MessageEntity> CreateMessageAsync(
		ConversationId    conversationId,
		ParticipantId     senderParticipantId,
		string?           content,
		DateTime          utcNow,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists all messages for a conversation ordered by creation time (oldest first).
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The messages.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="conversationId"/> is less than or equal to 0.</exception>
	Task<List<MessageEntity>> ListMessagesByConversationAsync(
		ConversationId    conversationId,
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
	Task<List<MessageEntity>> ListRecentMessagesByConversationAsync(
		ConversationId    conversationId,
		int               limit,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Redacts the content of a message while preserving conversation structure.
	/// </summary>
	/// <param name="messageId">The internal message identifier.</param>
	/// <param name="reason">The reason why the message content is being redacted.</param>
	/// <param name="utcNow">The timestamp to persist as the redaction moment.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if a message was found and updated; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="messageId"/> is less than or equal to 0.</exception>
	Task<bool> RedactMessageAsync(
		MessageId              messageId,
		MessageRedactionReason reason,
		DateTime               utcNow,
		CancellationToken      cancellationToken = default);

	/// <summary>
	/// Redacts a message only if it was authored by the specified participant.
	/// </summary>
	/// <param name="messageId">The internal message identifier.</param>
	/// <param name="authorParticipantId">The identifier of the expected author participant.</param>
	/// <param name="utcNow">The timestamp to persist as the redaction moment.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the message existed and was redacted by this call; otherwise <see langword="false"/>.
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
		DateTime          utcNow,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Redacts all messages authored by a specific participant.
	/// </summary>
	/// <param name="participantId">The identifier of the participant whose messages should be redacted.</param>
	/// <param name="reason">The reason why the message contents are being redacted.</param>
	/// <param name="utcNow">The timestamp to persist as the redaction moment.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of messages updated.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="participantId"/> is less than or equal to 0.</exception>
	Task<int> RedactMessagesByParticipantAsync(
		ParticipantId          participantId,
		MessageRedactionReason reason,
		DateTime               utcNow,
		CancellationToken      cancellationToken = default);

	/// <summary>
	/// Creates generation metadata for an existing message.
	/// </summary>
	/// <param name="messageId">The internal message identifier.</param>
	/// <param name="metadata">The metadata to persist. <see cref="MessageGenerationMetadataEntity.MessageId"/> is ignored.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The created <see cref="MessageGenerationMetadataEntity"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="metadata"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="messageId"/> is less than or equal to 0.</exception>
	/// <exception cref="InvalidOperationException">The referenced message does not exist.</exception>
	/// <remarks>
	/// If <see cref="DatabaseOptions.StoreFullPrompts"/> is <see langword="false"/>,
	/// <see cref="MessageGenerationMetadataEntity.FullPrompt"/> will be pruned before persisting.
	/// </remarks>
	Task<MessageGenerationMetadataEntity> CreateMessageGenerationMetadataAsync(
		MessageId                       messageId,
		MessageGenerationMetadataEntity metadata,
		CancellationToken               cancellationToken = default);
}
