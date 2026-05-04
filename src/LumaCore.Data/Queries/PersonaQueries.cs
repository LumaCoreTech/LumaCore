// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Queries;

/// <summary>
/// Provides pre-compiled queries for persona operations.
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
public static class PersonaQueries
{
	/// <summary>
	/// Gets all active personas regardless of visibility, including the linked
	/// <see cref="PersonaEntity.Participant"/>, <see cref="PersonaEntity.ActiveSystemPrompt"/>, and
	/// <see cref="PersonaEntity.CreatedByParticipant"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This query does <b>not</b> filter by <see cref="PersonaEntity.Visibility"/> or ownership.
	///     It is intended for admin/system contexts where all active personas must be enumerable.
	///     </para>
	///     <para>
	///     For user-facing persona selection (where visibility and ownership matter), use
	///     <see cref="IPersonaDataService.GetPersonasForUserAsync"/> instead.
	///     </para>
	///     <para>
	///     The included navigations match <see cref="IPersonaDataService.GetAllActivePersonasAsync"/>'s
	///     contract so this query can serve as the compiled hot-path drop-in replacement.
	///     </para>
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, IAsyncEnumerable<PersonaEntity>>
		GetAllActive = EF.CompileAsyncQuery((LumaCoreDbContext ctx) =>
			ctx.Personas
				.AsNoTracking()
				.Include(p => p.Participant)
				.Include(p => p.ActiveSystemPrompt)
				.Include(p => p.CreatedByParticipant)
				.Include(p => p.DescriptionTranslations)
				.Where(p => p.IsActive)
				.OrderBy(p => p.Participant!.DisplayName)
				.AsQueryable());

	/// <summary>
	/// Gets a persona by its participant ID, including the linked <see cref="PersonaEntity.Participant"/>,
	/// <see cref="PersonaEntity.ActiveSystemPrompt"/>, and <see cref="PersonaEntity.CreatedByParticipant"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Used when resolving persona details from a message sender.
	///     </para>
	///     <para>
	///     The included navigations match <see cref="IPersonaDataService.GetPersonaByParticipantIdAsync"/>'s
	///     contract so this query can serve as the compiled hot-path drop-in replacement.
	///     </para>
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ParticipantId, Task<PersonaEntity?>>
		GetByParticipantId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ParticipantId participantId) =>
			ctx.Personas
				.AsNoTracking()
				.Include(p => p.Participant)
				.Include(p => p.ActiveSystemPrompt)
				.Include(p => p.CreatedByParticipant)
				.Include(p => p.DescriptionTranslations)
				.FirstOrDefault(p => p.ParticipantId == participantId));

	/// <summary>
	/// Gets a persona by its public ID, including the linked <see cref="PersonaEntity.Participant"/>,
	/// <see cref="PersonaEntity.ActiveSystemPrompt"/>, and <see cref="PersonaEntity.CreatedByParticipant"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Used for API lookups where the client provides a GUID. The included navigations match
	///     <see cref="IPersonaDataService.GetPersonaByPublicIdAsync"/>'s contract so this query can serve
	///     as the compiled hot-path drop-in replacement.
	///     </para>
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, Guid, Task<PersonaEntity?>>
		GetByPublicId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, Guid publicId) =>
			ctx.Personas
				.AsNoTracking()
				.Include(p => p.Participant)
				.Include(p => p.ActiveSystemPrompt)
				.Include(p => p.CreatedByParticipant)
				.Include(p => p.DescriptionTranslations)
				.FirstOrDefault(p => p.Participant!.PublicId == publicId));

	/// <summary>
	/// Gets the currently active system prompt for a persona by following
	/// <see cref="PersonaEntity.ActiveSystemPromptId"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Used when building the LLM prompt for a conversation. The active prompt is not necessarily the
	///     most recently created one for the persona —
	///     <see cref="IPersonaDataService.UpdatePersonaAsync"/> reuses an existing prompt row when its
	///     content matches the new value (deduplication by hash), so a revert to an earlier version leaves
	///     <see cref="PersonaEntity.ActiveSystemPromptId"/> pointing at the older row.
	///     </para>
	///     <para>
	///     Matches <see cref="IPersonaDataService.GetCurrentSystemPromptAsync"/>'s contract so this query
	///     can serve as the compiled hot-path drop-in replacement.
	///     </para>
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, PersonaId, Task<SystemPromptEntity?>>
		GetCurrentSystemPrompt = EF.CompileAsyncQuery((LumaCoreDbContext ctx, PersonaId personaId) =>
			ctx.Personas
				.AsNoTracking()
				.Where(p => p.Id == personaId && p.ActiveSystemPromptId != null)
				.Select(p => p.ActiveSystemPrompt)
				.FirstOrDefault());
}
