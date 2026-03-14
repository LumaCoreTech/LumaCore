// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Queries;

/// <summary>
/// Provides pre-compiled queries for persona operations.
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
public static class PersonaQueries
{
	/// <summary>
	/// Gets all active personas.
	/// </summary>
	/// <remarks>
	/// Used for displaying the persona selector in the chat UI.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, IAsyncEnumerable<PersonaEntity>>
		GetAllActive = EF.CompileAsyncQuery((LumaCoreDbContext ctx) =>
			ctx.Personas
				.AsNoTracking()
				.Include(p => p.Participant)
				.Where(p => p.IsActive)
				.OrderBy(p => p.Participant!.DisplayName)
				.AsQueryable());

	/// <summary>
	/// Gets a persona by its participant ID.
	/// </summary>
	/// <remarks>
	/// Used when resolving persona details from a message sender.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ParticipantId, Task<PersonaEntity?>>
		GetByParticipantId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ParticipantId participantId) =>
			ctx.Personas
				.AsNoTracking()
				.Include(p => p.Participant)
				.FirstOrDefault(p => p.ParticipantId == participantId));

	/// <summary>
	/// Gets a persona by its public ID.
	/// </summary>
	/// <remarks>
	/// Used for API lookups where the client provides a GUID.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, Guid, Task<PersonaEntity?>>
		GetByPublicId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, Guid publicId) =>
			ctx.Personas
				.AsNoTracking()
				.Include(p => p.Participant)
				.FirstOrDefault(p => p.Participant!.PublicId == publicId));

	/// <summary>
	/// Gets the current system prompt for a persona.
	/// </summary>
	/// <remarks>
	/// Used when building the LLM prompt for a conversation.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, PersonaId, Task<SystemPromptEntity?>>
		GetCurrentSystemPrompt = EF.CompileAsyncQuery((LumaCoreDbContext ctx, PersonaId personaId) =>
			ctx.SystemPrompts
				.AsNoTracking()
				.Where(sp => sp.PersonaId == personaId)
				.OrderByDescending(sp => sp.CreatedAtUtc)
				.FirstOrDefault());
}
