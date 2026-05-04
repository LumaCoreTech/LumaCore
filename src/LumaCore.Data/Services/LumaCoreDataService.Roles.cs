// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;
using LumaCore.Data.Entities;
using LumaCore.Data.Queries;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Services;

public sealed partial class LumaCoreDataService
{
	#region Read APIs

	/// <inheritdoc/>
	public async Task<IReadOnlyList<RoleEntity>> GetAllRolesAsync(CancellationToken cancellationToken = default)
	{
		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return await MaterializeAsync(RoleQueries.GetAll(mDbContext), cancellationToken).ConfigureAwait(false);
		}

		return await mDbContext.Roles
			       .AsNoTracking()
			       .OrderBy(r => r.Name)
			       .ToListAsync(cancellationToken)
			       .ConfigureAwait(false);
	}

	#endregion

	#region Projection APIs

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> GetRoleNamesByUserIdAsync(
		UserId            userId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return await MaterializeAsync(RoleQueries.GetRoleNamesByUserId(mDbContext, userId), cancellationToken)
				       .ConfigureAwait(false);
		}

		return await mDbContext.UserRoles
			       .AsNoTracking()
			       .Where(ur => ur.UserId == userId)
			       .Select(ur => ur.Role!.Name)
			       .ToListAsync(cancellationToken)
			       .ConfigureAwait(false);
	}

	#endregion

	#region Existence Checks

	/// <inheritdoc/>
	public Task<bool> UserHasRoleAsync(
		UserId            userId,
		string            roleName,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);
		Guard.ThrowIfNullOrEmptyOrTooLong(roleName, EntityLimits.RoleNameMaxLength, out roleName);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return RoleQueries.UserHasRole(mDbContext, userId, roleName);
		}

		return mDbContext.UserRoles
			.AsNoTracking()
			.AnyAsync(ur => ur.UserId == userId && ur.Role!.Name == roleName, cancellationToken);
	}

	#endregion

	#region Mutation APIs

	/// <inheritdoc/>
	public async Task<bool> AssignRoleToUserAsync(
		UserId            userId,
		RoleId            roleId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roleId.Value);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		var userRole = new UserRoleEntity
		{
			UserId = userId,
			RoleId = roleId,
			AssignedAtUtc = effectiveUtcNow
		};

		mDbContext.UserRoles.Add(userRole);
		try
		{
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (DbUpdateException)
		{
			// This can happen if (UserId, RoleId) already exists (race or repeated request).
			// To avoid swallowing unrelated failures (FK violations, provider issues), we verify existence.
			bool exists = await mDbContext.UserRoles
				              .AsNoTracking()
				              .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken)
				              .ConfigureAwait(false);

			if (exists)
			{
				// Detach the stale entity so subsequent SaveChangesAsync() calls on the same
				// DbContext don't attempt to re-insert it.
				mDbContext.Entry(userRole).State = EntityState.Detached;
				return false;
			}

			throw;
		}

		return true;
	}

	/// <inheritdoc/>
	public async Task<bool> RemoveRoleFromUserAsync(
		UserId            userId,
		RoleId            roleId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roleId.Value);

		// Set-based DELETE: avoids the load-track-delete-save round-trip. Returns the number of rows
		// affected, which is 0 (no such assignment) or 1 (the unique (UserId, RoleId) pair).
		int deleted = await mDbContext.UserRoles
			              .Where(ur => ur.UserId == userId && ur.RoleId == roleId)
			              .ExecuteDeleteAsync(cancellationToken)
			              .ConfigureAwait(false);

		return deleted > 0;
	}

	#endregion
}
