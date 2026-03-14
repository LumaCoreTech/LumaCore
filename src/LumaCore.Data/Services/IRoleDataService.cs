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
	/// <summary>
	/// Assigns a role to a user.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="roleId">The role identifier.</param>
	/// <param name="utcNow">The timestamp to store as assignment time.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the role was assigned;
	/// <see langword="false"/> if it already existed.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="userId"/> or <paramref name="roleId"/> is less than or equal to 0.
	/// </exception>
	Task<bool> AssignRoleToUserAsync(
		UserId            userId,
		RoleId            roleId,
		DateTime          utcNow,
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
}
