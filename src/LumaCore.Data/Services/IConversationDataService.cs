// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// Provides conversation related database operations.
/// </summary>
public interface IConversationDataService
{
	#region Read APIs

	/// <summary>
	/// Gets a conversation by its public identifier.
	/// </summary>
	/// <param name="publicId">The public identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The matching conversation, or <see langword="null"/> if not found.</returns>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	Task<ConversationEntity?> GetConversationByPublicIdAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets all participants in a conversation with their <see cref="ConversationParticipantEntity.Participant"/>
	/// and <see cref="ParticipantEntity.Persona"/> navigation properties loaded.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of <see cref="ConversationParticipantEntity"/> instances with
	/// <see cref="ConversationParticipantEntity.Participant"/> and <see cref="ParticipantEntity.Persona"/>
	/// eagerly loaded.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> is less than or equal to 0.
	/// </exception>
	Task<IReadOnlyList<ConversationParticipantEntity>> GetConversationParticipantsAsync(
		ConversationId    conversationId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the persona participants in a conversation that were created by the specified participant.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="ownerParticipantId">The participant identifier of the persona owner.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of <see cref="ParticipantEntity"/> instances for each owned persona in the conversation.
	/// Empty if the owner has no personas in this conversation.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> or <paramref name="ownerParticipantId"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	///     <para>
	///     Used during the leave flow to remove personas that belong to the departing user,
	///     preventing other users from accessing private memories via prompt injection.
	///     </para>
	///     <para>
	///     Only the scalar columns of <see cref="ParticipantEntity"/> are populated; navigation properties such as
	///     <see cref="ParticipantEntity.Persona"/> and <see cref="ParticipantEntity.User"/> are not loaded.
	///     The returned entities are detached (<c>AsNoTracking</c>).
	///     </para>
	/// </remarks>
	Task<IReadOnlyList<ParticipantEntity>> GetOwnedPersonaParticipantsInConversationAsync(
		ConversationId    conversationId,
		ParticipantId     ownerParticipantId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets all persona participants in a conversation, ordered by <see cref="ParticipantId"/>.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of <see cref="ParticipantEntity"/> instances whose linked <see cref="PersonaEntity"/> is not
	/// <see langword="null"/>. Empty if the conversation has no persona participants.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> is less than or equal to 0.
	/// </exception>
	Task<IReadOnlyList<ParticipantEntity>> GetPersonaParticipantsAsync(
		ConversationId    conversationId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists the conversations for a participant ordered by last update.
	/// </summary>
	/// <param name="participantId">The participant identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The conversations.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="participantId"/> is less than or equal to 0.</exception>
	Task<IReadOnlyList<ConversationEntity>> ListConversationsByParticipantAsync(
		ParticipantId     participantId,
		CancellationToken cancellationToken = default);

	#endregion

	#region Projection APIs

	/// <summary>
	/// Gets the participant counts for multiple conversations in a single batch query.
	/// </summary>
	/// <param name="conversationIds">The conversation identifiers to count participants for.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A dictionary mapping each <see cref="ConversationId"/> to its participant count.
	/// Conversations with no participants are absent from the dictionary.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="conversationIds"/> is <see langword="null"/>.
	/// </exception>
	Task<IReadOnlyDictionary<ConversationId, int>> GetParticipantCountsAsync(
		IEnumerable<ConversationId> conversationIds,
		CancellationToken           cancellationToken = default);

	#endregion

	#region Existence Checks

	/// <summary>
	/// Determines whether a conversation still has at least one user (non-persona) participant.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if at least one user participant remains; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// A "user participant" is a participant that has a corresponding row in the <c>Users</c> table.
	/// Persona-only participants are not counted. This is used after a user leaves to determine
	/// whether the conversation should be auto-deleted.
	/// </remarks>
	Task<bool> HasUserParticipantsAsync(
		ConversationId    conversationId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Determines whether a participant is a member of the specified conversation.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="participantId">The participant identifier to check.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the participant belongs to the conversation; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> or <paramref name="participantId"/> is less than or equal to 0.
	/// </exception>
	Task<bool> IsParticipantInConversationAsync(
		ConversationId    conversationId,
		ParticipantId     participantId,
		CancellationToken cancellationToken = default);

	#endregion

	#region Mutation APIs

	/// <summary>
	/// Adds a participant to an existing conversation.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="participantId">The identifier of the participant to add.</param>
	/// <param name="role">The role within the conversation.</param>
	/// <param name="utcNow">
	/// The timestamp to store as join time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the participant was added;
	/// <see langword="false"/> if it already existed.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> or <paramref name="participantId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
	/// Persisting the join row fails for a reason other than a duplicate (ConversationId, ParticipantId) pair
	/// (for example, an FK violation because the conversation or participant no longer exists, or a connection error).
	/// Duplicate-pair conflicts are classified internally and converted to a <see langword="false"/> return value.
	/// </exception>
	Task<bool> AddParticipantToConversationAsync(
		ConversationId              conversationId,
		ParticipantId               participantId,
		ConversationParticipantRole role,
		DateTime?                   utcNow            = null,
		CancellationToken           cancellationToken = default);

	/// <summary>
	/// Creates a new conversation.
	/// </summary>
	/// <param name="title">The conversation title.</param>
	/// <param name="creatorParticipantId">The identifier of the participant creating the conversation.</param>
	/// <param name="utcNow">
	/// The timestamp to store as creation/update time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="description">An optional description for the conversation.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The created <see cref="ConversationEntity"/>.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="title"/> is empty/whitespace or exceeds the configured maximum length.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="creatorParticipantId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// <paramref name="creatorParticipantId"/> does not refer to a user participant.
	/// </exception>
	/// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
	/// Persisting the new conversation or its initial participant row fails. This can happen when the creator
	/// participant is removed between the user-participant pre-check and the insert (FK violation), or for the usual
	/// reasons (connection loss, provider errors).
	/// </exception>
	Task<ConversationEntity> CreateConversationAsync(
		string            title,
		ParticipantId     creatorParticipantId,
		DateTime?         utcNow            = null,
		string?           description       = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes all private conversations for a user participant.
	/// </summary>
	/// <param name="userParticipantId">The user participant identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A <see cref="DeletePrivateConversationsResult"/> containing the number of conversations deleted and the number of
	/// conversations skipped because they were multi-user.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="userParticipantId"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// A "private" conversation is defined as a conversation that contains exactly one active user participant
	/// (the specified user) and may contain any number of personas.
	/// </remarks>
	Task<DeletePrivateConversationsResult> DeleteAllPrivateConversationsByUserParticipantAsync(
		ParticipantId     userParticipantId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes a conversation and all dependent records.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if a conversation was found and deleted;
	/// otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> is less than or equal to 0.
	/// </exception>
	Task<bool> DeleteConversationAsync(
		ConversationId    conversationId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Removes a participant from a conversation.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="participantId">The identifier of the participant to remove.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the participant was removed;
	/// <see langword="false"/> if the participant was not found in the conversation.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> or <paramref name="participantId"/> is less than or equal to 0.
	/// </exception>
	Task<bool> RemoveParticipantFromConversationAsync(
		ConversationId    conversationId,
		ParticipantId     participantId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates the editable metadata of an existing conversation.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="title">The new conversation title.</param>
	/// <param name="description">
	/// The new conversation description, or <see langword="null"/> to clear it.
	/// </param>
	/// <param name="utcNow">
	/// The timestamp to store as update time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the conversation existed and was updated;
	/// otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="title"/> is empty/whitespace or exceeds the configured maximum length, or
	/// <paramref name="description"/> exceeds the configured maximum length.
	/// </exception>
	/// <remarks>
	/// This is a full-replacement operation: both <see cref="ConversationEntity.Title"/> and
	/// <see cref="ConversationEntity.Description"/> are set to the provided values.
	/// <see cref="ConversationEntity.UpdatedAtUtc"/> is updated as well.
	/// The update is set-based (no entity materialization/change tracking), so it scales well.
	/// </remarks>
	Task<bool> UpdateConversationAsync(
		ConversationId    conversationId,
		string            title,
		string?           description,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default);

	#endregion
}
