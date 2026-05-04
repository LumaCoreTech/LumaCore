// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;
using LumaCore.Data.Entities;
using LumaCore.Data.Queries;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LumaCore.Data.Services;

#pragma warning disable IDE0037 // Use inferred member name

public sealed partial class LumaCoreDataService
{
	#region Read APIs

	/// <inheritdoc/>
	public Task<MessageEntity?> GetMessageByPublicIdAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default)
	{
		// Guard against the sentinel value early — Guid.Empty is never a valid public id and indicates a
		// caller bug (e.g. uninitialized request DTO) rather than a legitimate "not found" lookup.
		Guard.ThrowIfEmpty(publicId);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return MessageQueries.GetByPublicId(mDbContext, publicId);
		}

		// Default branch: regular EF query, fully cancellable. AsNoTrackingWithIdentityResolution + explicit
		// Include(Sender) preserves the Sender-population contract from IMessageDataService.
		return mDbContext.Messages
			.AsNoTrackingWithIdentityResolution()
			.Include(m => m.Sender)
			.FirstOrDefaultAsync(m => m.PublicId == publicId, cancellationToken);
	}


	/// <inheritdoc/>
	public async Task<MessagePage> ListMessagesByConversationAsync(
		ConversationId    conversationId,
		int               offset            = 0,
		int               limit             = int.MaxValue,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);
		ArgumentOutOfRangeException.ThrowIfNegative(offset);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

		// Single-roundtrip pagination: project the page together with the total count via a window-style
		// subquery. The Sender navigation must be projected explicitly because Include is dropped when a
		// Select to an anonymous type follows. AsNoTrackingWithIdentityResolution enables EF's identity
		// fixup (without full change-tracking overhead) so the projected Sender instance is wired up to
		// Message.Sender — plain AsNoTracking would leave Message.Sender as null.
		var page = await mDbContext.Messages
			           .AsNoTrackingWithIdentityResolution()
			           .Where(m => m.ConversationId == conversationId)
			           .OrderBy(m => m.CreatedAtUtc)
			           .Select(m => new
			           {
				           Message = m,
				           Sender = m.Sender,
				           TotalCount = mDbContext.Messages.Count(x => x.ConversationId == conversationId)
			           })
			           .Skip(offset)
			           .Take(limit)
			           .ToListAsync(cancellationToken)
			           .ConfigureAwait(false);

		if (page.Count > 0)
			return new MessagePage(page.ConvertAll(x => x.Message), page[0].TotalCount, offset, limit);

		// Fallback: page is empty (offset beyond data range or no rows at all). A separate count query is
		// needed to preserve the TotalCount contract. PreferCompiledHotPathQueries chooses between the
		// compiled-query fast path (no CancellationToken support) and the regular EF query (cancellable).
		// This fallback is a rare edge case (offset past the end), so either path is acceptable.
		int totalCount;
		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			totalCount = await MessageQueries
				             .CountByConversationId(mDbContext, conversationId)
				             .ConfigureAwait(false);
		}
		else
		{
			totalCount = await mDbContext.Messages
				             .AsNoTracking()
				             .CountAsync(m => m.ConversationId == conversationId, cancellationToken)
				             .ConfigureAwait(false);
		}

		return new MessagePage([], totalCount, offset, limit);
	}


	/// <inheritdoc/>
	public async Task<IReadOnlyList<MessageEntity>> ListRecentMessagesByConversationAsync(
		ConversationId    conversationId,
		int               limit,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken. WithCancellation on the
			// IAsyncEnumerable below only allows the consuming await foreach to observe cancellation between
			// rows; it does not abort the underlying SQL execution. With PreferCompiledHotPathQueries enabled,
			// cancellation is best-effort only — the trade-off documented on DatabaseOptions.
			return await MaterializeRecentAsync(conversationId, limit, cancellationToken).ConfigureAwait(false);
		}

		// Default branch: regular EF query, fully cancellable. AsNoTrackingWithIdentityResolution + explicit
		// Include(Sender) preserves the Sender-population contract from IMessageDataService (callers rendering
		// recent messages need the sender's display data and would otherwise face N+1 lookups).
		return await mDbContext.Messages
			       .AsNoTrackingWithIdentityResolution()
			       .Include(m => m.Sender)
			       .Where(m => m.ConversationId == conversationId)
			       .OrderByDescending(m => m.CreatedAtUtc)
			       .Take(limit)
			       .ToListAsync(cancellationToken)
			       .ConfigureAwait(false);

		async Task<List<MessageEntity>> MaterializeRecentAsync(
			ConversationId    cid,
			int               max,
			CancellationToken ct)
		{
			var result = new List<MessageEntity>();

			IAsyncEnumerable<MessageEntity> source = MessageQueries.GetRecentByConversationId(mDbContext, cid, max);

			await foreach (MessageEntity message in source.WithCancellation(ct).ConfigureAwait(false))
			{
				result.Add(message);
			}

			return result;
		}
	}

	#endregion

	#region Mutation APIs

	/// <inheritdoc/>
	public async Task<MessageEntity> CreateMessageAsync(
		ConversationId    conversationId,
		ParticipantId     senderParticipantId,
		string?           content,
		DateTime?         utcNow            = null,
		Guid?             publicId          = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(senderParticipantId.Value);

		if (publicId is { } pid)
			Guard.ThrowIfEmpty(pid, nameof(publicId));

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		// Normalize content: trim whitespace and treat empty strings as null (attachment-only messages).
		content = content?.Trim();
		if (content is { Length: 0 })
			content = null;

		IDbContextTransaction transaction = await mDbContext
			                                    .Database
			                                    .BeginTransactionAsync(cancellationToken)
			                                    .ConfigureAwait(false);

		try
		{
			var message = new MessageEntity
			{
				PublicId = publicId ?? Guid.NewGuid(),
				ConversationId = conversationId,
				SenderId = senderParticipantId,
				Content = content,
				CreatedAtUtc = effectiveUtcNow
			};

			// We load the conversation entity (tracked), because we update its UpdatedAtUtc for ordering.
			// This makes the message insert + conversation update atomic with the surrounding transaction.
			ConversationEntity? conversation = await mDbContext.Conversations
				                                   .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
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

			conversation.UpdatedAtUtc = effectiveUtcNow;

			mDbContext.Messages.Add(message);
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			// Detach so the tracked entities do not cross the data-service boundary (service API convention).
			mDbContext.Entry(message).State = EntityState.Detached;
			mDbContext.Entry(conversation).State = EntityState.Detached;
			return message;
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}
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

		// Verify the referenced message exists before we do any allocation work.
		bool messageExists = await mDbContext.Messages
			                     .AsNoTracking()
			                     .AnyAsync(m => m.Id == messageId, cancellationToken)
			                     .ConfigureAwait(false);

		if (!messageExists)
			throw new InvalidOperationException($"Message '{messageId}' does not exist.");

		// CreateForMessage copies relevant fields and prunes FullPrompt when StoreFullPrompts is disabled.
		var entity = MessageGenerationMetadataEntity.CreateForMessage(
			messageId,
			metadata,
			mDatabaseOptions.StoreFullPrompts);

		mDbContext.MessageGenerationMetadata.Add(entity);
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// Detach so the tracked entity does not cross the data-service boundary (service API convention).
		mDbContext.Entry(entity).State = EntityState.Detached;
		return entity;
	}


	/// <inheritdoc/>
	public async Task<MessageEntity> CreateSystemMessageAsync(
		ConversationId    conversationId,
		string?           content,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);
		ArgumentNullException.ThrowIfNull(content);

		content = content.Trim();
		if (content.Length == 0)
			throw new ArgumentException("Message content must not be empty.", nameof(content));

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

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
				SenderId = null,
				Content = content,
				Type = MessageType.System,
				CreatedAtUtc = effectiveUtcNow
			};

			ConversationEntity? conversation = await mDbContext.Conversations
				                                   .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
				                                   .ConfigureAwait(false);

			if (conversation is null)
				throw new InvalidOperationException($"Conversation '{conversationId}' does not exist.");

			conversation.UpdatedAtUtc = effectiveUtcNow;

			mDbContext.Messages.Add(message);
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			// Detach so the tracked entities do not cross the data-service boundary (service API convention).
			mDbContext.Entry(message).State = EntityState.Detached;
			mDbContext.Entry(conversation).State = EntityState.Detached;
			return message;
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}
	}


	/// <inheritdoc/>
	public async Task<bool> RedactMessageAsync(
		MessageId              messageId,
		MessageRedactionReason reason,
		DateTime?              utcNow            = null,
		CancellationToken      cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(messageId.Value);

		MessageEntity? message = await mDbContext.Messages
			                         .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
			                         .ConfigureAwait(false);

		if (message is null)
			return false;

		RedactMessage(message, reason, ResolveUtcNow(utcNow));

		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return true;
	}


	/// <inheritdoc/>
	public async Task<bool> RedactMessageByAuthorAsync(
		MessageId         messageId,
		ParticipantId     authorParticipantId,
		DateTime?         utcNow            = null,
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

		RedactMessage(message, MessageRedactionReason.UserRequestedDeletion, ResolveUtcNow(utcNow));
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return true;
	}


	/// <inheritdoc/>
	public async Task<int> RedactMessagesByParticipantAsync(
		ParticipantId          participantId,
		MessageRedactionReason reason,
		DateTime?              utcNow            = null,
		CancellationToken      cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantId.Value);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		// Use ExecuteUpdateAsync for efficient bulk redaction without loading entities into memory.
		// This is significantly faster for participants with many messages.
		// Already-redacted messages are excluded to preserve their original reason.
		int redactedCount = await mDbContext.Messages
			                    .Where(m => m.SenderId == participantId && m.RedactedAtUtc == null)
			                    .ExecuteUpdateAsync(
				                    setters => setters
					                    .SetProperty(m => m.Content, (string?)null)
					                    .SetProperty(m => m.RedactedAtUtc, effectiveUtcNow)
					                    .SetProperty(m => m.RedactionReason, reason),
				                    cancellationToken)
			                    .ConfigureAwait(false);

		return redactedCount;
	}

	#endregion

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
