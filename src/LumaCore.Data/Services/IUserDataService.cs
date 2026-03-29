// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// Provides user/account related database operations.
/// </summary>
public interface IUserDataService
{
	/// <summary>
	/// Creates a new participant entry.
	/// </summary>
	/// <param name="displayName">The display name of the participant.</param>
	/// <param name="avatarUrl">An optional avatar URL.</param>
	/// <param name="utcNow">The timestamp to store as the creation time.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The created <see cref="ParticipantEntity"/>.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="displayName"/> is empty/whitespace or exceeds the configured maximum length, or when
	/// <paramref name="avatarUrl"/> exceeds the configured maximum length.
	/// </exception>
	Task<ParticipantEntity> CreateParticipantAsync(
		string            displayName,
		string?           avatarUrl,
		DateTime          utcNow,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a new user account linked to an existing participant.
	/// </summary>
	/// <param name="participantId">The participant to link to this user.</param>
	/// <param name="username">The unique username.</param>
	/// <param name="email">An optional email address (must be unique when provided).</param>
	/// <param name="passwordHash">The securely hashed password.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The created <see cref="UserEntity"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="participantId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="username"/> is empty/whitespace or exceeds the configured maximum length.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="email"/> exceeds the configured maximum length.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="passwordHash"/> is empty/whitespace or exceeds the configured maximum length.
	/// </exception>
	Task<UserEntity> CreateUserAsync(
		ParticipantId     participantId,
		string            username,
		string?           email,
		string            passwordHash,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a new participant and a linked user account atomically.
	/// </summary>
	/// <param name="displayName">The display name of the participant.</param>
	/// <param name="avatarUrl">An optional avatar URL.</param>
	/// <param name="username">The unique username.</param>
	/// <param name="email">An optional email address (must be unique when provided).</param>
	/// <param name="passwordHash">The securely hashed password.</param>
	/// <param name="assignDefaultUserRole">
	/// <see langword="true"/> to assign the default user role; otherwise <see langword="false"/>.
	/// </param>
	/// <param name="utcNow">The timestamp to store as the creation time.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The created <see cref="UserEntity"/> (including its linked <see cref="UserEntity.Participant"/>).
	/// </returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="displayName"/>, <paramref name="username"/>, or <paramref name="passwordHash"/> is
	/// empty/whitespace or exceeds the configured maximum length.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="email"/> or <paramref name="avatarUrl"/> exceeds the configured maximum length.
	/// </exception>
	/// <exception cref="InvalidOperationException">The default role cannot be found.</exception>
	Task<UserEntity> CreateUserWithParticipantAsync(
		string            displayName,
		string?           avatarUrl,
		string            username,
		string?           email,
		string            passwordHash,
		bool              assignDefaultUserRole,
		DateTime          utcNow,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes a user account and scrubs personal data from its linked participant.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if a user was found and deleted; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="userId"/> is less than or equal to 0.</exception>
	/// <remarks>
	/// The linked participant record is preserved to keep conversation participant lists and message history consistent.
	/// Additional cleanup behavior (redacting authored messages, deleting private conversations) is controlled by
	/// <see cref="DatabaseOptions"/>.
	/// </remarks>
	Task<bool> DeleteUserAndScrubParticipantAsync(UserId userId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Determines whether an email address already exists.
	/// </summary>
	/// <param name="email">The email address to check.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if the email exists; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="email"/> is empty/whitespace or exceeds the configured maximum length.
	/// </exception>
	Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a user by email address.
	/// </summary>
	/// <param name="email">The email to look up.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The matching user, or <see langword="null"/> if not found.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="email"/> is empty/whitespace or exceeds the configured maximum length.
	/// </exception>
	Task<UserEntity?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a user by username.
	/// </summary>
	/// <param name="username">The username to look up.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The matching user, or <see langword="null"/> if not found.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="username"/> is empty/whitespace or exceeds the configured maximum length.
	/// </exception>
	Task<UserEntity?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);

	/// <summary>
	/// Determines whether a username already exists.
	/// </summary>
	/// <param name="username">The username to check.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if the username exists; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="username"/> is empty/whitespace or exceeds the configured maximum length.
	/// </exception>
	Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the serialized preferences JSON for a user.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The raw JSON string, or <see langword="null"/> if no preferences have been stored yet.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="userId"/> is less than or equal to 0.</exception>
	Task<string?> GetPreferencesJsonAsync(UserId userId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates or updates the serialized preferences JSON for a user (upsert).
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="preferencesJson">
	/// The serialized JSON to persist. Must not exceed
	/// <see cref="Definitions.EntityLimits.UserPreferencesJsonMaxLength"/> characters.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="userId"/> is less than or equal to 0.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="preferencesJson"/> is empty/whitespace or exceeds the configured maximum length.
	/// </exception>
	Task UpdatePreferencesJsonAsync(
		UserId            userId,
		string            preferencesJson,
		CancellationToken cancellationToken = default);
}
