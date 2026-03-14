// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Services;

public sealed partial class LumaCoreDataService
{
	/// <inheritdoc/>
	public async Task<bool> AssignRoleToUserAsync(
		UserId            userId,
		RoleId            roleId,
		DateTime          utcNow,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roleId.Value);

		var userRole = new UserRoleEntity
		{
			UserId = userId,
			RoleId = roleId,
			AssignedAtUtc = utcNow
		};

		mDbContext.UserRoles.Add(userRole);
		try
		{
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			return true;
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
	}

	/// <inheritdoc/>
	public async Task<bool> RemoveRoleFromUserAsync(
		UserId            userId,
		RoleId            roleId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roleId.Value);

		UserRoleEntity? existing = await mDbContext.UserRoles
			                           .FirstOrDefaultAsync(
				                           ur => ur.UserId == userId && ur.RoleId == roleId,
				                           cancellationToken)
			                           .ConfigureAwait(false);

		if (existing is null)
			return false;

		mDbContext.UserRoles.Remove(existing);
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return true;
	}
}
