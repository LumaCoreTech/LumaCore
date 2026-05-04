// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// Provides role/authorization mapping related database operations.
/// </summary>
public interface IRoleDataService
{
	#region Read APIs

	/// <summary>
	/// Gets all roles ordered by name.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A list of all roles, ordered alphabetically by <see cref="RoleEntity.Name"/>.</returns>
	/// <remarks>
	/// Intended for admin scenarios (e.g. role-assignment dropdowns).
	/// The expected number of rows is small, so no pagination is provided.
	/// </remarks>
	Task<IReadOnlyList<RoleEntity>> GetAllRolesAsync(CancellationToken cancellationToken = default);

	#endregion

	#region Projection APIs

	/// <summary>
	/// Gets the names of all roles assigned to the specified user.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of role names assigned to the user, in unspecified order.
	/// The list is empty when the user has no roles or does not exist.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="userId"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// Intended for the authorization pipeline (e.g. building <c>ClaimsPrincipal</c> on login or token refresh).
	/// </remarks>
	Task<IReadOnlyList<string>> GetRoleNamesByUserIdAsync(
		UserId            userId,
		CancellationToken cancellationToken = default);

	#endregion

	#region Existence Checks

	/// <summary>
	/// Determines whether the specified user is assigned the named role.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="roleName">
	/// The role name to check, matched against <see cref="RoleEntity.Name"/>. The case-sensitivity of the
	/// match is currently provider-dependent and follows the active database collation; callers should
	/// supply the role name with the casing it was originally created with.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the user has the named role; otherwise <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// Intended for fine-grained authorization checks where loading the full role list would be wasteful.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="userId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="roleName"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="roleName"/> is empty, whitespace-only, or exceeds the maximum role name length.
	/// </exception>
	Task<bool> UserHasRoleAsync(
		UserId            userId,
		string            roleName,
		CancellationToken cancellationToken = default);

	#endregion

	#region Mutation APIs

	/// <summary>
	/// Assigns a role to a user.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="roleId">The role identifier.</param>
	/// <param name="utcNow">
	/// The timestamp to store as assignment time, or <see langword="null"/> to use the service's configured <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the role was assigned;
	/// <see langword="false"/> if it already existed.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="userId"/> or <paramref name="roleId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
	/// Persisting the join row fails for a reason other than a duplicate (UserId, RoleId) pair (for example,
	/// an FK violation because the user or role no longer exists, or a connection error). Duplicate-pair
	/// conflicts are classified internally and converted to a <see langword="false"/> return value.
	/// </exception>
	/// <remarks>
	/// A return value of <see langword="false"/> means "the assignment exists at the moment this call
	/// completes" — it covers both the case where the row was already present before the call and the case
	/// where a concurrent caller won an insert race. Callers must not infer ordering from the boolean alone.
	/// </remarks>
	Task<bool> AssignRoleToUserAsync(
		UserId            userId,
		RoleId            roleId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Removes a role assignment from a user.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="roleId">The role identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if an assignment was removed;
	/// otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="userId"/> or <paramref name="roleId"/> is less than or equal to 0.
	/// </exception>
	Task<bool> RemoveRoleFromUserAsync(
		UserId            userId,
		RoleId            roleId,
		CancellationToken cancellationToken = default);

	#endregion
}
