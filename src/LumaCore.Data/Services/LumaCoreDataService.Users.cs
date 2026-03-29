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
	/// <inheritdoc/>
	public async Task<ParticipantEntity> CreateParticipantAsync(
		string            displayName,
		string?           avatarUrl,
		DateTime          utcNow,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(displayName, EntityLimits.DisplayNameMaxLength, out displayName);
		Guard.ThrowIfTooLong(avatarUrl, EntityLimits.AvatarUrlMaxLength, out avatarUrl);

		var participant = new ParticipantEntity
		{
			PublicId = Guid.NewGuid(),
			DisplayName = displayName,
			AvatarUrl = avatarUrl,
			CreatedAtUtc = utcNow
		};

		mDbContext.Participants.Add(participant);
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return participant;
	}

	/// <inheritdoc/>
	public async Task<UserEntity> CreateUserAsync(
		ParticipantId     participantId,
		string            username,
		string?           email,
		string            passwordHash,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantId.Value);
		Guard.ThrowIfNullOrEmptyOrTooLong(username, EntityLimits.UsernameMaxLength, out username);
		string usernameNormalized = NormalizeUsername(username);
		Guard.ThrowIfNullOrEmptyOrTooLong(passwordHash, EntityLimits.PasswordHashMaxLength, out passwordHash);
		Guard.ThrowIfTooLong(email, EntityLimits.EmailMaxLength, out email);

		var user = new UserEntity
		{
			ParticipantId = participantId,
			Username = username,
			UsernameNormalized = usernameNormalized,
			Email = email,
			PasswordHash = passwordHash
		};

		mDbContext.Users.Add(user);
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return user;
	}

	/// <inheritdoc/>
	public async Task<UserEntity> CreateUserWithParticipantAsync(
		string            displayName,
		string?           avatarUrl,
		string            username,
		string?           email,
		string            passwordHash,
		bool              assignDefaultUserRole,
		DateTime          utcNow,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(displayName, EntityLimits.DisplayNameMaxLength, out displayName);
		Guard.ThrowIfTooLong(avatarUrl, EntityLimits.AvatarUrlMaxLength, out avatarUrl);
		Guard.ThrowIfNullOrEmptyOrTooLong(username, EntityLimits.UsernameMaxLength, out username);
		Guard.ThrowIfNullOrEmptyOrTooLong(passwordHash, EntityLimits.PasswordHashMaxLength, out passwordHash);
		Guard.ThrowIfTooLong(email, EntityLimits.EmailMaxLength, out email);

		string usernameNormalized = NormalizeUsername(username);

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
				AvatarUrl = avatarUrl,
				CreatedAtUtc = utcNow
			};

			var user = new UserEntity
			{
				Participant = participant,
				Username = username,
				UsernameNormalized = usernameNormalized,
				Email = email,
				PasswordHash = passwordHash
			};

			mDbContext.Users.Add(user);

			if (assignDefaultUserRole)
			{
				RoleEntity? userRole = await mDbContext.Roles
					                       .FirstOrDefaultAsync(
						                       r => r.Name == RoleDefinitions.User.Name,
						                       cancellationToken)
					                       .ConfigureAwait(false);

				if (userRole is null)
					throw new InvalidOperationException($"Default role '{RoleDefinitions.User.Name}' does not exist.");

				mDbContext.UserRoles.Add(
					new UserRoleEntity
					{
						User = user,
						Role = userRole,
						AssignedAtUtc = utcNow
					});
			}

			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);

		DateTime utcNow = mTimeProvider.GetUtcNow().UtcDateTime;

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

				// Scrub personal data from the participant record. The record itself is kept
				// so conversation participant lists and message history remain structurally intact.
				participant.DisplayName = "Deleted user";
				participant.AvatarUrl = null;

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
								.SetProperty(m => m.RedactedAtUtc, utcNow)
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

	/// <inheritdoc/>
	public async Task<string?> GetPreferencesJsonAsync(UserId userId, CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId.Value);

		return await mDbContext.UserPreferences
			       .AsNoTracking()
			       .Where(p => p.UserId == userId)
			       .Select(p => p.PreferencesJson)
			       .FirstOrDefaultAsync(cancellationToken)
			       .ConfigureAwait(false);
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
