// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Queries;

/// <summary>
/// Provides pre-compiled queries for message operations.
/// </summary>
/// <remarks>
///     <para>
///     Compiled queries eliminate the overhead of expression-tree parsing and SQL generation on each
///     execution. Use these for frequently-executed queries in hot paths.
///     </para>
///     <para>
///     <b>Important:</b> EF Core compiled queries do not accept a <see cref="CancellationToken"/>.
///     Cancellation is "best effort" only — the caller stops awaiting, but the underlying database
///     operation may still run to completion. Consider this trade-off when using these queries in
///     contexts where responsiveness to cancellation is critical.
///     </para>
///     <para>
///     All query delegates in this class are thread-safe and can be used concurrently. The
///     <see cref="LumaCoreDbContext"/> instances passed to them are not thread-safe and must remain scoped.
///     </para>
///     <para>
///     <b>Implementation note:</b> the streaming queries returning <see cref="IAsyncEnumerable{T}"/> end with
///     a trailing <c>.AsQueryable()</c>. This is <em>not</em> redundant: it disambiguates the
///     <c>EF.CompileAsyncQuery</c> overload — without it, a trailing <c>OrderBy</c>/<c>Take</c> resolves to
///     <see cref="IOrderedQueryable{T}"/> and the compiler picks the buffering
///     <c>Task&lt;IOrderedQueryable&lt;T&gt;&gt;</c> overload instead of the streaming one.
///     </para>
/// </remarks>
public static class MessageQueries
{
	/// <summary>
	/// Counts the messages in a conversation.
	/// </summary>
	/// <remarks>
	/// Used by <see cref="LumaCoreDataService"/> as the total-count fallback in
	/// <c>ListMessagesByConversationAsync</c> when the projected page is empty
	/// (e.g. <c>offset</c> beyond the data range).
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ConversationId, Task<int>>
		CountByConversationId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ConversationId conversationId) =>
			ctx.Messages.Count(m => m.ConversationId == conversationId));

	/// <summary>
	/// Gets all messages for a conversation, ordered by creation time (oldest first), with the
	/// <see cref="MessageEntity.Sender"/> navigation populated.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Uses <see cref="EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution{TEntity}"/> +
	///     <c>Include(m =&gt; m.Sender)</c> to satisfy the Sender-population contract of
	///     <see cref="IMessageDataService"/> list APIs and to deduplicate repeated senders without paying for
	///     full change-tracking.
	///     </para>
	///     <para>
	///     Not used by <c>ListMessagesByConversationAsync</c> directly — that method uses a single-roundtrip
	///     projection that bundles the page with the total count, which cannot be expressed as a parameterless
	///     compiled query.
	///     </para>
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ConversationId, IAsyncEnumerable<MessageEntity>>
		GetByConversationId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ConversationId conversationId) =>
			ctx.Messages
				.AsNoTrackingWithIdentityResolution()
				.Include(m => m.Sender)
				.Where(m => m.ConversationId == conversationId)
				.OrderBy(m => m.CreatedAtUtc)
				.AsQueryable());

	/// <summary>
	/// Gets a single message by its public identifier, with the <see cref="MessageEntity.Sender"/> navigation
	/// populated.
	/// </summary>
	/// <remarks>
	/// Used by <see cref="IMessageDataService.GetMessageByPublicIdAsync"/> to back REST API lookups where the
	/// client provides a GUID and expects to render the message (including sender display data) without a
	/// follow-up roundtrip.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, Guid, Task<MessageEntity?>>
		GetByPublicId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, Guid publicId) =>
			ctx.Messages
				.AsNoTrackingWithIdentityResolution()
				.Include(m => m.Sender)
				.FirstOrDefault(m => m.PublicId == publicId));

	/// <summary>
	/// Gets the most recent messages for a conversation (newest first) up to the given limit, with the
	/// <see cref="MessageEntity.Sender"/> navigation populated.
	/// </summary>
	/// <remarks>
	/// Used by <see cref="IMessageDataService.ListRecentMessagesByConversationAsync"/> for context window
	/// management (e.g. fetching the last N messages for an LLM prompt or for a chat-pane initial render).
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ConversationId, int, IAsyncEnumerable<MessageEntity>>
		GetRecentByConversationId = EF.CompileAsyncQuery((
				LumaCoreDbContext ctx,
				ConversationId    conversationId,
				int               limit) =>
			ctx.Messages
				.AsNoTrackingWithIdentityResolution()
				.Include(m => m.Sender)
				.Where(m => m.ConversationId == conversationId)
				.OrderByDescending(m => m.CreatedAtUtc)
				.Take(limit)
				.AsQueryable());
}
