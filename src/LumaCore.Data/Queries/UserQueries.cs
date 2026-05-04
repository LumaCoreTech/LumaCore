// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Queries;

/// <summary>
/// Provides pre-compiled queries for user operations.
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
/// </remarks>
public static class UserQueries
{
	/// <summary>
	/// Checks if an email is already registered.
	/// </summary>
	/// <remarks>
	/// Used during user registration to validate uniqueness.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, string, Task<bool>>
		ExistsByEmail = EF.CompileAsyncQuery((LumaCoreDbContext ctx, string email) =>
			ctx.Users.Any(u => u.Email == email));

	/// <summary>
	/// Checks if a normalized username is already taken.
	/// </summary>
	/// <remarks>
	/// Used during user registration to validate uniqueness. The caller must pass a pre-normalized username
	/// (via <c>Trim()</c> + <c>ToUpperInvariant()</c>) to match the <see cref="UserEntity.UsernameNormalized"/> column.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, string, Task<bool>>
		ExistsByUsernameNormalized = EF.CompileAsyncQuery((LumaCoreDbContext ctx, string normalizedUsername) =>
			ctx.Users.Any(u => u.UsernameNormalized == normalizedUsername));

	/// <summary>
	/// Gets a user by email for authentication or recovery.
	/// </summary>
	/// <remarks>
	/// Used for email-based login or password recovery.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, string, Task<UserEntity?>>
		GetByEmail = EF.CompileAsyncQuery((LumaCoreDbContext ctx, string email) =>
			ctx.Users
				.AsNoTracking()
				.Include(u => u.Participant)
				.FirstOrDefault(u => u.Email == email));

	/// <summary>
	/// Gets a user by their participant ID.
	/// </summary>
	/// <remarks>
	/// Used when resolving user details from a message sender.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, ParticipantId, Task<UserEntity?>>
		GetByParticipantId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, ParticipantId participantId) =>
			ctx.Users
				.AsNoTracking()
				.Include(u => u.Participant)
				.FirstOrDefault(u => u.ParticipantId == participantId));

	/// <summary>
	/// Gets a user by normalized username for authentication.
	/// </summary>
	/// <remarks>
	/// Used on every login attempt. Includes the <see cref="ParticipantEntity"/> for display name.
	/// The caller must pass a pre-normalized username (via <c>Trim()</c> + <c>ToUpperInvariant()</c>)
	/// to match the <see cref="UserEntity.UsernameNormalized"/> column.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, string, Task<UserEntity?>>
		GetByUsernameNormalized = EF.CompileAsyncQuery((LumaCoreDbContext ctx, string normalizedUsername) =>
			ctx.Users
				.AsNoTracking()
				.Include(u => u.Participant)
				.FirstOrDefault(u => u.UsernameNormalized == normalizedUsername));
}
