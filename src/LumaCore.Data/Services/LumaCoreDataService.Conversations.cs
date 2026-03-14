// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;
using LumaCore.Data.Entities;
using LumaCore.Data.Queries;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LumaCore.Data.Services;

public sealed partial class LumaCoreDataService
{
	/// <inheritdoc/>
	public async Task<bool> AddParticipantToConversationAsync(
		ConversationId              conversationId,
		ParticipantId               participantId,
		ConversationParticipantRole role,
		DateTime                    utcNow,
		CancellationToken           cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantId.Value);

		var join = new ConversationParticipantEntity
		{
			ConversationId = conversationId,
			ParticipantId = participantId,
			Role = role,
			JoinedAtUtc = utcNow
		};

		mDbContext.ConversationParticipants.Add(join);
		try
		{
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (DbUpdateException)
		{
			// This can happen if (ConversationId, ParticipantId) already exists (race or repeated request).
			// To avoid swallowing unrelated failures (FK violations, provider issues), we verify existence.
			bool exists = await mDbContext.ConversationParticipants
				              .AsNoTracking()
				              .AnyAsync(
					              cp => cp.ConversationId == conversationId &&
					                    cp.ParticipantId == participantId,
					              cancellationToken)
				              .ConfigureAwait(false);

			if (exists)
			{
				// Detach the stale entity so subsequent SaveChangesAsync() calls on the same
				// DbContext don't attempt to re-insert it.
				mDbContext.Entry(join).State = EntityState.Detached;
				return false;
			}

			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<ConversationEntity> CreateConversationAsync(
		string            title,
		ParticipantId     creatorParticipantId,
		DateTime          utcNow,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(title, EntityLimits.ConversationTitleMaxLength, out title);

		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(creatorParticipantId.Value);

		// Explicitly ensure the creator is a user participant. Personas can participate in conversations,
		// but cannot be the entity that "creates" one (auditability/authorization boundary).
		bool creatorIsUser = await mDbContext.Users
			                     .AsNoTracking()
			                     .AnyAsync(u => u.ParticipantId == creatorParticipantId, cancellationToken)
			                     .ConfigureAwait(false);

		if (!creatorIsUser)
		{
			throw new InvalidOperationException(
				$"Creator participant '{creatorParticipantId}' is not a user participant.");
		}

		IDbContextTransaction transaction = await mDbContext
			                                    .Database
			                                    .BeginTransactionAsync(cancellationToken)
			                                    .ConfigureAwait(false);

		try
		{
			var conversation = new ConversationEntity
			{
				PublicId = Guid.NewGuid(),
				Title = title,
				CreatedAtUtc = utcNow,
				UpdatedAtUtc = utcNow
			};

			mDbContext.Conversations.Add(conversation);

			var join = new ConversationParticipantEntity
			{
				Conversation = conversation,
				ParticipantId = creatorParticipantId,
				Role = ConversationParticipantRole.Owner,
				JoinedAtUtc = utcNow
			};

			mDbContext.ConversationParticipants.Add(join);

			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			return conversation;
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task<DeletePrivateConversationsResult> DeleteAllPrivateConversationsByUserParticipantAsync(
		ParticipantId     userParticipantId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userParticipantId.Value);

		// Definition: A "private" conversation has exactly one active user participant (this user)
		// and may include any number of personas.
		//
		// Strategy (fully set-based, no materialization into memory):
		// 1. Find candidate conversations (all conversations this participant belongs to).
		// 2. Of those, identify the private ones (exactly one user-participant per conversation).
		// 3. Delete the private ones in a single ExecuteDeleteAsync call.
		//
		// "User" is determined by a matching row in Users (via ParticipantId) — personas don't count.

		// --- Step 1: Candidate conversations ---
		IQueryable<ConversationId> candidateConversationIds = mDbContext.ConversationParticipants
			.AsNoTracking()
			.Where(cp => cp.ParticipantId == userParticipantId)
			.Select(cp => cp.ConversationId)
			.Distinct();

		int candidateCount = await candidateConversationIds
			                     .CountAsync(cancellationToken)
			                     .ConfigureAwait(false);

		if (candidateCount == 0)
			return new DeletePrivateConversationsResult(Deleted: 0, SkippedMultiUser: 0);

		// --- Step 2: Filter to private conversations (exactly 1 user participant) ---
		// GroupJoin against Users produces a LEFT JOIN so personas get HasUser = false.
		// Grouping by ConversationId then counts actual users per conversation.
		IQueryable<ConversationId> privateConversationIdsQuery = mDbContext.ConversationParticipants
			.AsNoTracking()
			.Where(cp => candidateConversationIds.Contains(cp.ConversationId))
			.GroupJoin(
				mDbContext.Users.AsNoTracking(),
				cp => cp.ParticipantId,
				u => u.ParticipantId,
				(cp, users) => new { cp.ConversationId, HasUser = users.Any() })
			.GroupBy(x => x.ConversationId)
			.Select(g => new { ConversationId = g.Key, UserCount = g.Count(x => x.HasUser) })
			.Where(x => x.UserCount == 1)
			.Select(x => x.ConversationId);

		int privateCount = await privateConversationIdsQuery
			                   .CountAsync(cancellationToken)
			                   .ConfigureAwait(false);

		int skippedMultiUser = candidateCount - privateCount;
		if (privateCount == 0)
			return new DeletePrivateConversationsResult(Deleted: 0, SkippedMultiUser: skippedMultiUser);

		// --- Step 3: Delete private conversations in a single database command ---
		int deleted = await mDbContext.Conversations
			              .Where(c => privateConversationIdsQuery.Contains(c.Id))
			              .ExecuteDeleteAsync(cancellationToken)
			              .ConfigureAwait(false);

		return new DeletePrivateConversationsResult(Deleted: deleted, SkippedMultiUser: skippedMultiUser);
	}

	/// <inheritdoc/>
	public async Task<bool> DeleteConversationAsync(
		ConversationId    conversationId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);

		int deleted = await mDbContext.Conversations
			              .Where(c => c.Id == conversationId)
			              .ExecuteDeleteAsync(cancellationToken)
			              .ConfigureAwait(false);

		return deleted > 0;
	}

	/// <inheritdoc/>
	public Task<ConversationEntity?> GetConversationByPublicIdAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default)
	{
		if (publicId == Guid.Empty)
			throw new ArgumentException("PublicId must not be empty.", nameof(publicId));

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return ConversationQueries.GetByPublicId(mDbContext, publicId);
		}

		return mDbContext.Conversations
			.AsNoTracking()
			.FirstOrDefaultAsync(c => c.PublicId == publicId, cancellationToken);
	}

	/// <inheritdoc/>
	public Task<List<ConversationEntity>> ListConversationsByParticipantAsync(
		ParticipantId     participantId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantId.Value);

		return mDbContext.ConversationParticipants
			.AsNoTracking()
			.Where(cp => cp.ParticipantId == participantId)
			.Select(cp => cp.Conversation!)
			.OrderByDescending(c => c.UpdatedAtUtc)
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<bool> UpdateConversationTitleAsync(
		ConversationId    conversationId,
		string            title,
		DateTime          utcNow,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversationId.Value);
		Guard.ThrowIfNullOrEmptyOrTooLong(title, EntityLimits.ConversationTitleMaxLength, out title);

		int updated = await mDbContext.Conversations
			              .Where(c => c.Id == conversationId)
			              .ExecuteUpdateAsync(
				              setters => setters
					              .SetProperty(c => c.Title, title)
					              .SetProperty(c => c.UpdatedAtUtc, utcNow),
				              cancellationToken)
			              .ConfigureAwait(false);

		return updated > 0;
	}
}
