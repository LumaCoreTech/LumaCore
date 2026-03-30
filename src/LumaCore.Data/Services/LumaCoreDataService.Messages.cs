// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LumaCore.Data.Services;

public sealed partial class LumaCoreDataService
{
	/// <inheritdoc/>
	public async Task<MessageEntity> CreateMessageAsync(
		ConversationId    conversationId,
		ParticipantId     senderParticipantId,
		string?           content,
		DateTime          utcNow,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(senderParticipantId.Value);
		ArgumentNullException.ThrowIfNull(content);

		content = content.Trim();
		if (content.Length == 0)
			throw new ArgumentException("Message content must not be empty.", nameof(content));

		IDbContextTransaction transaction = await mDbContext
			                                    .Database
			                                    .BeginTransactionAsync(cancellationToken)
			                                    .ConfigureAwait(false);

		try
		{
			var message = new MessageEntity
			{
				PublicId = Guid.NewGuid(),
				ConversationId = conversationId,
				SenderId = senderParticipantId,
				Content = content,
				CreatedAtUtc = utcNow
			};

			// We load the conversation entity (tracked), because we update its UpdatedAtUtc for ordering.
			// This makes the message insert + conversation update atomic with the surrounding transaction.
			ConversationEntity? conversation = await mDbContext.Conversations
				                                   .FirstOrDefaultAsync(
					                                   c => c.Id == conversationId,
					                                   cancellationToken)
				                                   .ConfigureAwait(false);

			if (conversation is null)
				throw new InvalidOperationException($"Conversation '{conversationId}' does not exist.");

			// Validate sender existence early to fail with deterministic domain errors rather than an FK violation.
			bool senderExists = await mDbContext.Participants
				                    .AsNoTracking()
				                    .AnyAsync(p => p.Id == senderParticipantId, cancellationToken)
				                    .ConfigureAwait(false);

			if (!senderExists)
				throw new InvalidOperationException($"Sender participant '{senderParticipantId}' does not exist.");

			// Security/integrity check: prevent creating messages in conversations the sender is not a member of.
			// Relying on FK constraints alone is insufficient here: the sender can exist globally, and the conversation
			// can exist globally, but the relationship (membership) is what authorizes messaging.
			bool senderIsInConversation = await mDbContext.ConversationParticipants
				                              .AsNoTracking()
				                              .AnyAsync(
					                              cp => cp.ConversationId == conversationId &&
					                                    cp.ParticipantId == senderParticipantId,
					                              cancellationToken)
				                              .ConfigureAwait(false);

			if (!senderIsInConversation)
			{
				throw new InvalidOperationException(
					$"Sender participant '{senderParticipantId}' is not part of conversation '{conversationId}'.");
			}

			conversation.UpdatedAtUtc = utcNow;

			mDbContext.Messages.Add(message);
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			return message;
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public Task<List<MessageEntity>> ListMessagesByConversationAsync(
		ConversationId    conversationId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);

		return mDbContext.Messages
			.AsNoTracking()
			.Include(m => m.Sender)
			.Where(m => m.ConversationId == conversationId)
			.OrderBy(m => m.CreatedAtUtc)
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public Task<List<MessageEntity>> ListRecentMessagesByConversationAsync(
		ConversationId    conversationId,
		int               limit,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

		return mDbContext.Messages
			.AsNoTracking()
			.Where(m => m.ConversationId == conversationId)
			.OrderByDescending(m => m.CreatedAtUtc)
			.Take(limit)
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<bool> RedactMessageAsync(
		MessageId              messageId,
		MessageRedactionReason reason,
		DateTime               utcNow,
		CancellationToken      cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId.Value);

		MessageEntity? message = await mDbContext.Messages
			                         .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
			                         .ConfigureAwait(false);

		if (message is null)
			return false;

		RedactMessage(message, reason, utcNow);

		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc/>
	public async Task<bool> RedactMessageByAuthorAsync(
		MessageId         messageId,
		ParticipantId     authorParticipantId,
		DateTime          utcNow,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authorParticipantId.Value);

		MessageEntity? message = await mDbContext.Messages
			                         .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
			                         .ConfigureAwait(false);

		// All three conditions are non-exceptional outcomes in a user-driven deletion flow:
		// message not found, already redacted, or authored by someone else.
		if (message is null) return false;
		if (message.RedactedAtUtc is not null) return false;
		if (message.SenderId != authorParticipantId) return false;

		RedactMessage(message, MessageRedactionReason.UserRequestedDeletion, utcNow);
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc/>
	public async Task<int> RedactMessagesByParticipantAsync(
		ParticipantId          participantId,
		MessageRedactionReason reason,
		DateTime               utcNow,
		CancellationToken      cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantId.Value);

		// Use ExecuteUpdateAsync for efficient bulk redaction without loading entities into memory.
		// This is significantly faster for participants with many messages.
		// Already-redacted messages are excluded to preserve their original reason.
		int redactedCount = await mDbContext.Messages
			                    .Where(m => m.SenderId == participantId && m.RedactedAtUtc == null)
			                    .ExecuteUpdateAsync(
				                    setters => setters
					                    .SetProperty(m => m.Content, (string?)null)
					                    .SetProperty(m => m.RedactedAtUtc, utcNow)
					                    .SetProperty(m => m.RedactionReason, reason),
				                    cancellationToken)
			                    .ConfigureAwait(false);

		return redactedCount;
	}

	/// <inheritdoc/>
	public async Task<MessageGenerationMetadataEntity> CreateMessageGenerationMetadataAsync(
		MessageId                       messageId,
		MessageGenerationMetadataEntity metadata,
		CancellationToken               cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId.Value);
		ArgumentNullException.ThrowIfNull(metadata);

		// Validate eagerly — the caller supplies the endpoint reference, not the factory below.
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(metadata.ModelEndpointId.Value);

		// CreateForMessage copies relevant fields and prunes FullPrompt when StoreFullPrompts is disabled.
		var entity = MessageGenerationMetadataEntity.CreateForMessage(
			messageId,
			metadata,
			mDatabaseOptions.StoreFullPrompts);

		// Avoid FK violations and return a clear error.
		bool messageExists = await mDbContext.Messages
			                     .AsNoTracking()
			                     .AnyAsync(m => m.Id == messageId, cancellationToken)
			                     .ConfigureAwait(false);

		if (!messageExists)
			throw new InvalidOperationException($"Message '{messageId}' does not exist.");

		mDbContext.MessageGenerationMetadata.Add(entity);
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return entity;
	}

	/// <summary>
	/// Applies redaction to the specified tracked <paramref name="message"/> entity by clearing its content and
	/// recording the redaction timestamp and reason.
	/// </summary>
	/// <param name="message">The tracked message entity to redact.</param>
	/// <param name="reason">The reason for redaction.</param>
	/// <param name="utcNow">The UTC timestamp to record as the redaction moment.</param>
	/// <remarks>
	/// This method only mutates the in-memory entity. The caller is responsible for persisting the changes
	/// via <c>SaveChangesAsync</c>.
	/// </remarks>
	private static void RedactMessage(MessageEntity message, MessageRedactionReason reason, DateTime utcNow)
	{
		message.Content = null;
		message.RedactedAtUtc = utcNow;
		message.RedactionReason = reason;
	}
}
