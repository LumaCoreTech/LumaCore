// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Queries;

/// <summary>
/// Provides pre-compiled queries for message operations.
/// </summary>
/// <remarks>
///     <para>
///     Compiled queries eliminate the overhead of expression tree parsing and SQL generation
///     on each execution. Use these for frequently-executed queries in hot paths.
///     </para>
///     <para>
///     <b>Important:</b> EF Core compiled queries do not accept a <see cref="CancellationToken"/>.
///     Cancellation is "best effort" only – the caller stops awaiting, but the underlying database
///     operation may still run to completion. Consider this trade-off when using these queries in
///     contexts where responsiveness to cancellation is critical.
///     </para>
///     <para>
///     All query delegates in this class are thread-safe and can be used concurrently.
///     The <see cref="LumaCoreDbContext"/> instances passed to them are not thread-safe and must remain scoped.
///     </para>
/// </remarks>
public static class MessageQueries
{
	/// <summary>
	/// Gets the message count for a conversation.
	/// </summary>
	/// <remarks>
	/// Used for pagination and statistics.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ConversationId, Task<int>>
		CountByConversationId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ConversationId conversationId) =>
			ctx.Messages.Count(m => m.ConversationId == conversationId));

	/// <summary>
	/// Gets all messages for a conversation, ordered by creation time (oldest first).
	/// </summary>
	/// <remarks>
	/// Used when loading chat history for display or context building.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ConversationId, IAsyncEnumerable<MessageEntity>>
		GetByConversationId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ConversationId conversationId) =>
			ctx.Messages
				.AsNoTracking()
				.Where(m => m.ConversationId == conversationId)
				.OrderBy(m => m.CreatedAtUtc)
				.AsQueryable());

	/// <summary>
	/// Gets a message by its public ID.
	/// </summary>
	/// <remarks>
	/// Used for API lookups where the client provides a GUID.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, Guid, Task<MessageEntity?>>
		GetByPublicId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, Guid publicId) =>
			ctx.Messages
				.AsNoTracking()
				.FirstOrDefault(m => m.PublicId == publicId));

	/// <summary>
	/// Gets the most recent messages for a conversation with a limit.
	/// </summary>
	/// <remarks>
	/// Used for context window management - fetching last N messages for LLM prompt.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ConversationId, int, IAsyncEnumerable<MessageEntity>>
		GetRecentByConversationId = EF.CompileAsyncQuery((
				LumaCoreDbContext ctx,
				ConversationId    conversationId,
				int               limit) =>
			ctx.Messages
				.AsNoTracking()
				.Where(m => m.ConversationId == conversationId)
				.OrderByDescending(m => m.CreatedAtUtc)
				.Take(limit)
				.AsQueryable());
}
