// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Queries;

/// <summary>
/// Provides pre-compiled queries for role operations.
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
public static class RoleQueries
{
	/// <summary>
	/// Gets all available roles.
	/// </summary>
	/// <remarks>
	/// Used for admin UI to display available roles for assignment.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, IAsyncEnumerable<RoleEntity>>
		GetAll = EF.CompileAsyncQuery((LumaCoreDbContext ctx) =>
			ctx.Roles
				.AsNoTracking()
				.OrderBy(r => r.Name)
				.AsQueryable());

	/// <summary>
	/// Gets a role by name.
	/// </summary>
	/// <remarks>
	/// Used when assigning roles to users.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, string, Task<RoleEntity?>>
		GetByName = EF.CompileAsyncQuery((LumaCoreDbContext ctx, string name) =>
			ctx.Roles
				.AsNoTracking()
				.FirstOrDefault(r => r.Name == name));

	/// <summary>
	/// Gets all role names for a user.
	/// </summary>
	/// <remarks>
	/// Used on every authenticated request for authorization checks.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, UserId, IAsyncEnumerable<string>>
		GetRoleNamesByUserId = EF.CompileAsyncQuery((LumaCoreDbContext ctx, UserId userId) =>
			ctx.UserRoles
				.AsNoTracking()
				.Where(ur => ur.UserId == userId)
				.Select(ur => ur.Role!.Name));

	/// <summary>
	/// Checks if a user has a specific role.
	/// </summary>
	/// <remarks>
	/// Used for quick authorization checks.
	/// </remarks>
	public static readonly Func<LumaCoreDbContext, UserId, string, Task<bool>>
		UserHasRole = EF.CompileAsyncQuery((LumaCoreDbContext ctx, UserId userId, string roleName) =>
			ctx.UserRoles
				.Any(ur => ur.UserId == userId && ur.Role!.Name == roleName));
}
