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
	public async Task<int> CleanupConversationsWithNoUsersAsync(CancellationToken cancellationToken = default)
	{
		// Cleanup is intentionally implemented as a single set-based DELETE.
		// Reasons:
		// - avoids loading entities/change tracking (memory + time)
		// - avoids N+1 deletes across conversations/messages/join rows
		// - lets the database engine perform the operation efficiently
		// This method is used as an integrity cleanup. Under normal operation, the result should be 0.
		IDbContextTransaction transaction = await mDbContext
			                                    .Database
			                                    .BeginTransactionAsync(cancellationToken)
			                                    .ConfigureAwait(false);

		try
		{
			int deleted = await mDbContext.Conversations
				              .Where(conversation => !mDbContext.ConversationParticipants
					                                     .Where(cp => cp.ConversationId == conversation.Id)
					                                     .Join(
						                                     mDbContext.Users,
						                                     cp => cp.ParticipantId,
						                                     u => u.ParticipantId,
						                                     (cp, u) => 1)
					                                     .Any())
				              .ExecuteDeleteAsync(cancellationToken)
				              .ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			return deleted;
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public Task<List<ConversationId>> ListConversationIdsWithNoUsersAsync(
		int               limit,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

		return mDbContext.Conversations
			.AsNoTracking()
			.Where(conversation => !mDbContext.ConversationParticipants
				                       .AsNoTracking()
				                       .Where(cp => cp.ConversationId == conversation.Id)
				                       .Join(
					                       mDbContext.Users.AsNoTracking(),
					                       cp => cp.ParticipantId,
					                       u => u.ParticipantId,
					                       (cp, u) => 1)
				                       .Any())
			.Select(c => c.Id)
			.Take(limit)
			.ToListAsync(cancellationToken);
	}
}
