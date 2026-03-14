// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Queries;

/// <summary>
/// Provides pre-compiled queries for conversation operations.
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
public static class ConversationQueries
{
	/// <summary>
	/// Gets the conversation count for a participant.
	/// </summary>
	/// <remarks>
	/// Used for pagination and statistics.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ParticipantId, Task<int>>
		CountByParticipantId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ParticipantId participantId) =>
			ctx.ConversationParticipants.Count(cp => cp.ParticipantId == participantId));

	/// <summary>
	/// Gets a conversation by its internal ID.
	/// </summary>
	/// <remarks>
	/// Used for internal lookups after resolving <c>PublicId</c>.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ConversationId, Task<ConversationEntity?>>
		GetById = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ConversationId id) =>
			ctx.Conversations
				.AsNoTracking()
				.FirstOrDefault(c => c.Id == id));

	/// <summary>
	/// Gets all conversations for a participant, ordered by last update (newest first).
	/// </summary>
	/// <remarks>
	/// Used for displaying the conversation list in the sidebar.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ParticipantId, IAsyncEnumerable<ConversationEntity>>
		GetByParticipantId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ParticipantId participantId) =>
			ctx.ConversationParticipants
				.AsNoTracking()
				.Where(cp => cp.ParticipantId == participantId)
				.Select(cp => cp.Conversation!)
				.OrderByDescending(c => c.UpdatedAtUtc)
				.AsQueryable());

	/// <summary>
	/// Gets a conversation by its public ID.
	/// </summary>
	/// <remarks>
	/// Used for API lookups where the client provides a GUID.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, Guid, Task<ConversationEntity?>>
		GetByPublicId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, Guid publicId) =>
			ctx.Conversations
				.AsNoTracking()
				.FirstOrDefault(c => c.PublicId == publicId));
}
