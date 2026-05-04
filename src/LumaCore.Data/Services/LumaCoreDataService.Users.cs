// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;
using LumaCore.Data.Entities;
using LumaCore.Data.Queries;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LumaCore.Data.Services;

public sealed partial class LumaCoreDataService
{
	#region Read APIs

	/// <inheritdoc/>
	public Task<UserEntity?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(email, EntityLimits.EmailMaxLength, out email);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return UserQueries.GetByEmail(mDbContext, email);
		}

		return mDbContext.Users
			.AsNoTracking()
			.Include(u => u.Participant)
			.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
	}

	/// <inheritdoc/>
	public Task<UserEntity?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(username, EntityLimits.UsernameMaxLength, out username);
		string normalized = NormalizeUsername(username);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return UserQueries.GetByUsernameNormalized(mDbContext, normalized);
		}

		return mDbContext.Users
			.AsNoTracking()
			.Include(u => u.Participant)
			.FirstOrDefaultAsync(u => u.UsernameNormalized == normalized, cancellationToken);
	}

	#endregion

	#region Projection APIs

	/// <inheritdoc/>
	public Task<string?> GetPreferencesJsonAsync(UserId userId, CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);

		return mDbContext.UserPreferences
			.AsNoTracking()
			.Where(p => p.UserId == userId)
			.Select(p => p.PreferencesJson)
			.FirstOrDefaultAsync(cancellationToken);
	}

	#endregion

	#region Existence Checks

	/// <inheritdoc/>
	public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(email, EntityLimits.EmailMaxLength, out email);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only (caller may stop awaiting,
			// but the underlying database operation may still run to completion).
			return UserQueries.ExistsByEmail(mDbContext, email);
		}

		return mDbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);
	}

	/// <inheritdoc/>
	public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(username, EntityLimits.UsernameMaxLength, out username);
		string normalized = NormalizeUsername(username);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return UserQueries.ExistsByUsernameNormalized(mDbContext, normalized);
		}

		return mDbContext.Users.AnyAsync(u => u.UsernameNormalized == normalized, cancellationToken);
	}

	#endregion

	#region Mutation APIs

	/// <inheritdoc/>
	public async Task<ParticipantEntity> CreateParticipantAsync(
		string            displayName,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(displayName, EntityLimits.ParticipantDisplayNameMaxLength, out displayName);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		var participant = new ParticipantEntity
		{
			PublicId = Guid.NewGuid(),
			DisplayName = displayName,
			CreatedAtUtc = effectiveUtcNow
		};

		mDbContext.Participants.Add(participant);
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// Convention: tracked entities never leave the data service. Detach so the caller cannot mutate
		// this instance and have the change picked up by a later SaveChangesAsync().
		mDbContext.Entry(participant).State = EntityState.Detached;
		return participant;
	}

	/// <inheritdoc/>
	public async Task<UserEntity> CreateUserAsync(
		ParticipantId     participantId,
		string            username,
		string?           email,
		string            passwordHash,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantId.Value);
		Guard.ThrowIfNullOrEmptyOrTooLong(username, EntityLimits.UsernameMaxLength, out username);
		string usernameNormalized = NormalizeUsername(username);
		Guard.ThrowIfNullOrEmptyOrTooLong(passwordHash, EntityLimits.PasswordHashMaxLength, out passwordHash);
		Guard.ThrowIfTooLong(email, EntityLimits.EmailMaxLength, out email);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		var user = new UserEntity
		{
			ParticipantId = participantId,
			CreatedAtUtc = effectiveUtcNow,
			Username = username,
			UsernameNormalized = usernameNormalized,
			Email = email,
			PasswordHash = passwordHash
		};

		mDbContext.Users.Add(user);
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// Convention: tracked entities never leave the data service.
		mDbContext.Entry(user).State = EntityState.Detached;
		return user;
	}

	/// <inheritdoc/>
	public async Task<UserEntity> CreateUserWithParticipantAsync(
		string            displayName,
		string            username,
		string?           email,
		string            passwordHash,
		bool              assignDefaultUserRole,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(displayName, EntityLimits.ParticipantDisplayNameMaxLength, out displayName);
		Guard.ThrowIfNullOrEmptyOrTooLong(username, EntityLimits.UsernameMaxLength, out username);
		Guard.ThrowIfNullOrEmptyOrTooLong(passwordHash, EntityLimits.PasswordHashMaxLength, out passwordHash);
		Guard.ThrowIfTooLong(email, EntityLimits.EmailMaxLength, out email);

		string usernameNormalized = NormalizeUsername(username);
		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		IDbContextTransaction transaction = await mDbContext
			                                    .Database
			                                    .BeginTransactionAsync(cancellationToken)
			                                    .ConfigureAwait(false);

		try
		{
			var participant = new ParticipantEntity
			{
				PublicId = Guid.NewGuid(),
				DisplayName = displayName,
				CreatedAtUtc = effectiveUtcNow
			};

			var user = new UserEntity
			{
				Participant = participant,
				CreatedAtUtc = effectiveUtcNow,
				Username = username,
				UsernameNormalized = usernameNormalized,
				Email = email,
				PasswordHash = passwordHash
			};

			mDbContext.Users.Add(user);

			if (assignDefaultUserRole)
			{
				RoleEntity? userRole;
				if (PreferCompiledHotPathQueries)
				{
					// Note: EF Core compiled queries do not accept a CancellationToken.
					// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
					userRole = await RoleQueries
						           .GetByName(mDbContext, RoleDefinitions.User.Name)
						           .ConfigureAwait(false);
				}
				else
				{
					userRole = await mDbContext.Roles
						           .FirstOrDefaultAsync(
							           r => r.Name == RoleDefinitions.User.Name,
							           cancellationToken)
						           .ConfigureAwait(false);
				}

				if (userRole is null)
					throw new InvalidOperationException($"Default role '{RoleDefinitions.User.Name}' does not exist.");

				// Use the FK directly instead of the Role navigation. The compiled-query branch loads
				// userRole with AsNoTracking(), so attaching the join entity through the navigation
				// would try to attach a detached RoleEntity whose key may already be tracked from a
				// previous load on the same DbContext, raising an identity conflict.
				mDbContext.UserRoles.Add(
					new UserRoleEntity
					{
						User = user,
						RoleId = userRole.Id,
						AssignedAtUtc = effectiveUtcNow
					});
			}

			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			// Convention: tracked entities never leave the data service. Detach both the user and its
			// linked participant (which is reachable through the navigation) so the caller cannot mutate
			// either instance and accidentally have the change picked up by a later SaveChangesAsync().
			mDbContext.Entry(user).State = EntityState.Detached;
			mDbContext.Entry(participant).State = EntityState.Detached;
			return user;
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task<bool> DeleteUserAndScrubParticipantAsync(
		UserId            userId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		UserEntity? user = await mDbContext.Users
			                   .Include(u => u.Participant)
			                   .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
			                   .ConfigureAwait(false);

		if (user is null)
			return false;

		IDbContextTransaction transaction = await mDbContext
			                                    .Database
			                                    .BeginTransactionAsync(cancellationToken)
			                                    .ConfigureAwait(false);

		try
		{
			ParticipantEntity? participant = user.Participant;
			mDbContext.Users.Remove(user);

			if (participant is not null)
			{
				if (mDatabaseOptions.UserDeletion.DeletePrivateConversations)
				{
					await DeleteAllPrivateConversationsByUserParticipantAsync(
							participant.Id,
							cancellationToken)
						.ConfigureAwait(false);
				}

				// Drop every resource reference owned by this user (profile picture, future attachments).
				// Profile pictures are anchored to the user (not the participant) precisely so this cleanup
				// is the structural endpoint of the avatar's lifetime: once the user row is gone, no further
				// owner exists. Without this explicit cleanup the avatar's PublicId URL would remain publicly
				// resolvable, and the underlying ResourceEntity would stay pinned as Active so the GC could
				// never reclaim the file. The polymorphic ownership model has no DB-level FK, so the cascade
				// must run as application logic before the User row is committed for deletion.
				await mResourceService
					.DeleteReferencesByOwnerAsync(
						ResourceOwnerKind.User,
						new ResourceOwnerId(userId.Value),
						cancellationToken)
					.ConfigureAwait(false);

				// Scrub personal data from the participant record. The record itself is kept
				// so conversation participant lists and message history remain structurally intact.
				participant.DisplayName = UserDefinitions.DeletedUserDisplayName;

				if (mDatabaseOptions.UserDeletion.RedactMessages)
				{
					// Bulk redaction via ExecuteUpdateAsync: runs server-side without loading entities into
					// memory, participates in the ambient transaction, and preserves already-redacted messages
					// (e.g., messages previously redacted for moderation keep their original reason).
					await mDbContext.Messages
						.Where(m => m.SenderId == participant.Id && m.RedactedAtUtc == null)
						.ExecuteUpdateAsync(
							setters => setters
								.SetProperty(m => m.Content, (string?)null)
								.SetProperty(m => m.RedactedAtUtc, effectiveUtcNow)
								.SetProperty(m => m.RedactionReason, MessageRedactionReason.UserDeleted),
							cancellationToken)
						.ConfigureAwait(false);
				}
			}

			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			return true;
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task UpdatePreferencesJsonAsync(
		UserId            userId,
		string            preferencesJson,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);
		Guard.ThrowIfNullOrEmptyOrTooLong(
			preferencesJson,
			EntityLimits.UserPreferencesJsonMaxLength,
			out preferencesJson);

		UserPreferencesEntity? existing = await mDbContext.UserPreferences
			                                  .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
			                                  .ConfigureAwait(false);

		if (existing is not null)
		{
			existing.PreferencesJson = preferencesJson;
		}
		else
		{
			mDbContext.UserPreferences.Add(
				new UserPreferencesEntity
				{
					UserId = userId,
					PreferencesJson = preferencesJson
				});
		}

		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
	}

	#endregion

	/// <summary>
	/// Normalizes the specified username by trimming whitespace and converting it to uppercase
	/// using the invariant culture.
	/// </summary>
	/// <param name="username">The username to normalize.</param>
	/// <returns>
	/// A normalized version of the username with leading and trailing whitespace removed and all characters
	/// converted to uppercase using the invariant culture.
	/// </returns>
	private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();
}
