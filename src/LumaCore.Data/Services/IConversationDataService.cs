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
	/// <summary>
	/// Adds a participant to an existing conversation.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="participantId">The identifier of the participant to add.</param>
	/// <param name="role">The role within the conversation.</param>
	/// <param name="utcNow">The timestamp to store as join time.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if the participant was added; <see langword="false"/> if it already existed.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> or <paramref name="participantId"/> is less than or equal to 0.
	/// </exception>
	Task<bool> AddParticipantToConversationAsync(
		ConversationId              conversationId,
		ParticipantId               participantId,
		ConversationParticipantRole role,
		DateTime                    utcNow,
		CancellationToken           cancellationToken = default);

	/// <summary>
	/// Creates a new conversation.
	/// </summary>
	/// <param name="title">The conversation title.</param>
	/// <param name="creatorParticipantId">The identifier of the participant creating the conversation.</param>
	/// <param name="utcNow">The timestamp to store as creation/update time.</param>
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
	Task<ConversationEntity> CreateConversationAsync(
		string            title,
		ParticipantId     creatorParticipantId,
		DateTime          utcNow,
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
	/// <see langword="true"/> if a conversation was found and deleted; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="conversationId"/> is less than or equal to 0.
	/// </exception>
	Task<bool> DeleteConversationAsync(ConversationId conversationId, CancellationToken cancellationToken = default);

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
	/// Lists the conversations for a participant ordered by last update.
	/// </summary>
	/// <param name="participantId">The participant identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The conversations.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="participantId"/> is less than or equal to 0.</exception>
	Task<List<ConversationEntity>> ListConversationsByParticipantAsync(
		ParticipantId     participantId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates the title of an existing conversation.
	/// </summary>
	/// <param name="conversationId">The internal conversation identifier.</param>
	/// <param name="title">The new conversation title.</param>
	/// <param name="utcNow">The timestamp to store as update time.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the conversation existed and was updated;
	/// otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="conversationId"/> is less than or equal to 0.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="title"/> is empty/whitespace or exceeds the configured
	/// maximum length.
	/// </exception>
	/// <remarks>
	/// This operation also updates <see cref="ConversationEntity.UpdatedAtUtc"/>.
	/// The update is intended to be set-based (no entity materialization/change tracking), so it scales well.
	/// </remarks>
	Task<bool> UpdateConversationTitleAsync(
		ConversationId    conversationId,
		string            title,
		DateTime          utcNow,
		CancellationToken cancellationToken = default);
}
