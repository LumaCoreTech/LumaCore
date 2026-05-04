// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// Provides persona-related database operations.
/// </summary>
public interface IPersonaDataService
{
	#region Read APIs

	/// <summary>
	/// Gets all active personas regardless of visibility, ordered by display name.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of active personas with <see cref="PersonaEntity.Participant"/>,
	/// <see cref="PersonaEntity.ActiveSystemPrompt"/>, <see cref="PersonaEntity.CreatedByParticipant"/>,
	/// and <see cref="PersonaEntity.DescriptionTranslations"/> loaded.
	/// </returns>
	/// <remarks>
	///     <para>
	///     Intended for admin/system contexts where all active personas must be enumerable.
	///     For user-facing persona selection (where visibility and ownership matter), use
	///     <see cref="GetPersonasForUserAsync"/> instead.
	///     </para>
	///     <para>
	///     The expected number of rows is small, so no pagination is provided.
	///     </para>
	/// </remarks>
	Task<IReadOnlyList<PersonaEntity>> GetAllActivePersonasAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a persona by its participant identifier, including the participant and active system prompt.
	/// </summary>
	/// <param name="participantId">The participant identifier of the persona.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The matching persona with <see cref="PersonaEntity.Participant"/>,
	/// <see cref="PersonaEntity.ActiveSystemPrompt"/>, <see cref="PersonaEntity.CreatedByParticipant"/>,
	/// and <see cref="PersonaEntity.DescriptionTranslations"/> loaded, or <see langword="null"/> if not
	/// found.
	/// </returns>
	/// <remarks>
	/// Intended for resolving persona details from a message sender, where the caller already has the
	/// participant identifier (e.g. from <see cref="MessageEntity.SenderId"/>).
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="participantId"/> is less than or equal to 0.
	/// </exception>
	Task<PersonaEntity?> GetPersonaByParticipantIdAsync(
		ParticipantId     participantId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a persona by its participant's public identifier, including the participant and active system prompt.
	/// </summary>
	/// <param name="publicId">The public identifier of the persona's linked participant.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The matching persona with <see cref="PersonaEntity.Participant"/>,
	/// <see cref="PersonaEntity.ActiveSystemPrompt"/>, <see cref="PersonaEntity.CreatedByParticipant"/>,
	/// and <see cref="PersonaEntity.DescriptionTranslations"/> loaded, or <see langword="null"/> if not
	/// found.
	/// </returns>
	/// <remarks>
	/// To mutate and persist the persona, use <see cref="UpdatePersonaAsync"/> — Read APIs return detached
	/// entities by contract.
	/// </remarks>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	Task<PersonaEntity?> GetPersonaByPublicIdAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets all personas visible to the specified user: their own private personas plus all shared personas.
	/// </summary>
	/// <param name="userParticipantId">The participant ID of the requesting user.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A list of personas with <see cref="PersonaEntity.Participant"/>,
	/// <see cref="PersonaEntity.CreatedByParticipant"/>, and
	/// <see cref="PersonaEntity.DescriptionTranslations"/> loaded.
	/// </returns>
	/// <remarks>
	/// <see cref="PersonaEntity.ActiveSystemPrompt"/> is intentionally omitted: persona-picker callers only
	/// need name, description, and ownership to render the selection UI. Use
	/// <see cref="GetPersonaByPublicIdAsync"/> or <see cref="GetCurrentSystemPromptAsync"/> when the prompt
	/// content is required.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="userParticipantId"/> is less than or equal to 0.
	/// </exception>
	Task<IReadOnlyList<PersonaEntity>> GetPersonasForUserAsync(
		ParticipantId     userParticipantId,
		CancellationToken cancellationToken = default);

	#endregion

	#region Projection APIs

	/// <summary>
	/// Gets the download metadata for a persona's avatar stored in the resource system.
	/// </summary>
	/// <param name="publicId">The public identifier of the persona's linked participant.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A <see cref="ResourceDownloadInfo"/> if the persona has an avatar,
	/// or <see langword="null"/> if the persona was not found or has no avatar.
	/// </returns>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	Task<ResourceDownloadInfo?> GetAvatarAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the currently active system prompt for the specified persona.
	/// </summary>
	/// <param name="personaId">The persona identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The <see cref="SystemPromptEntity"/> referenced by
	/// <see cref="PersonaEntity.ActiveSystemPromptId"/>, or <see langword="null"/> if the persona was not
	/// found or has no active prompt assigned.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="personaId"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// Intended for the LLM-prompt-building pipeline where only the prompt content is needed and the
	/// full persona graph is not. The returned prompt may not be the most recently created one for the
	/// persona — <see cref="UpdatePersonaAsync"/> reuses (rather than creates) a prompt when its content
	/// matches an existing version, so the active assignment can point at an older row after a revert.
	/// </remarks>
	Task<SystemPromptEntity?> GetCurrentSystemPromptAsync(
		PersonaId         personaId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks whether any of the specified personas have an avatar stored in the resource system.
	/// </summary>
	/// <param name="personaIds">The strongly-typed persona identifiers to check.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A set of <see cref="PersonaId"/> values that have at least one avatar resource reference.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="personaIds"/> is <see langword="null"/>.</exception>
	Task<IReadOnlySet<PersonaId>> GetPersonaIdsWithAvatarAsync(
		IEnumerable<PersonaId> personaIds,
		CancellationToken      cancellationToken = default);

	#endregion

	#region Mutation APIs

	/// <summary>
	/// Creates a private copy (clone) of an existing persona for the specified user.
	/// </summary>
	/// <param name="sourcePublicId">The public identifier of the source persona's participant.</param>
	/// <param name="creatorParticipantId">The participant ID of the user creating the clone.</param>
	/// <param name="utcNow">
	/// The timestamp to store as creation time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The cloned <see cref="PersonaEntity"/> with its <see cref="PersonaEntity.Participant"/>
	/// navigation loaded, or <see langword="null"/> if the source persona was not found.
	/// </returns>
	/// <exception cref="ArgumentException"><paramref name="sourcePublicId"/> is <see cref="Guid.Empty"/>.</exception>
	Task<PersonaEntity?> ClonePersonaAsync(
		Guid              sourcePublicId,
		ParticipantId     creatorParticipantId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a new persona with its participant identity and an optional initial system prompt.
	/// </summary>
	/// <param name="displayName">The display name for the persona's participant identity.</param>
	/// <param name="descriptionTranslations">
	/// An optional dictionary mapping culture codes (e.g. <c>en</c>, <c>de</c>) to localized description values.
	/// </param>
	/// <param name="defaultModel">An optional default LLM model identifier.</param>
	/// <param name="systemPrompt">
	/// An optional initial system prompt text. When provided, a
	/// <see cref="SystemPromptEntity"/> is created and set as the active prompt.
	/// </param>
	/// <param name="visibility">The visibility scope of the persona.</param>
	/// <param name="creatorParticipantId">
	/// The participant ID of the user creating this persona, or
	/// <see langword="null"/> for system-created personas.
	/// </param>
	/// <param name="utcNow">
	/// The timestamp to store as creation time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The created <see cref="PersonaEntity"/> with its <see cref="PersonaEntity.Participant"/>
	/// navigation loaded.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="displayName"/> is empty/whitespace or exceeds the configured maximum length, or
	/// <paramref name="defaultModel"/> exceeds the configured maximum length.
	/// </exception>
	Task<PersonaEntity> CreatePersonaAsync(
		string                               displayName,
		IReadOnlyDictionary<string, string>? descriptionTranslations,
		string?                              defaultModel,
		string?                              systemPrompt,
		PersonaVisibility                    visibility,
		ParticipantId?                       creatorParticipantId,
		DateTime?                            utcNow            = null,
		CancellationToken                    cancellationToken = default);

	/// <summary>
	/// Soft-deletes a persona by setting <see cref="PersonaEntity.IsActive"/> to <see langword="false"/>.
	/// </summary>
	/// <param name="publicId">The public identifier of the persona's linked participant.</param>
	/// <param name="utcNow">
	/// The timestamp to store as the last-updated time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the persona was found and deactivated;
	/// <see langword="false"/> if it was not found or was already inactive.
	/// </returns>
	/// <remarks>
	///     <para>
	///     <b>Avatar lifecycle:</b> The persona's avatar (if any) is <strong>intentionally retained</strong>
	///     across deactivation. The persona row continues to exist so historical messages can still resolve
	///     their author display (name, avatar) — symmetric to how <see cref="ParticipantEntity"/> survives
	///     user-account deletion to preserve chat-history integrity. To remove the avatar explicitly, call
	///     <see cref="DeleteAvatarAsync"/>.
	///     </para>
	///     <para>
	///     <b>Future hard-delete contract:</b> If a hard-delete operation is ever introduced (i.e. physically
	///     removing the persona row via <c>DbContext.Personas.Remove(...)</c>), it <strong>must</strong> first
	///     drop the persona-owned resource references via
	///     <see cref="IResourceService.DeleteReferencesByOwnerAsync"/> with
	///     <see cref="ResourceOwnerKind.Persona"/>. The polymorphic ownership model has no database-level FK
	///     between <see cref="ResourceReferenceEntity"/> and <see cref="PersonaEntity"/>, so cascade-delete
	///     does <em>not</em> run automatically — without explicit cleanup the avatar's <c>PublicId</c> URL
	///     would remain publicly resolvable, the underlying <see cref="ResourceEntity"/> would stay pinned
	///     as <see cref="ResourceDeletionState.Active"/>, and the GC could never reclaim the file. See
	///     <see cref="IUserDataService.DeleteUserAndScrubParticipantAsync"/> for the canonical pattern.
	///     </para>
	/// </remarks>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	Task<bool> DeactivatePersonaAsync(
		Guid              publicId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Removes the avatar image from a persona by deleting its resource references.
	/// </summary>
	/// <param name="publicId">The public identifier of the persona's linked participant.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the persona was found and had an avatar that was removed;
	/// <see langword="false"/> if the persona was not found or had no avatar.
	/// </returns>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	Task<bool> DeleteAvatarAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Saves avatar image data for a persona via the resource storage system, replacing any existing avatar.
	/// </summary>
	/// <param name="publicId">The public identifier of the persona's linked participant.</param>
	/// <param name="content">A readable stream containing the avatar image bytes.</param>
	/// <param name="contentType">The MIME content type (e.g. <c>image/png</c>).</param>
	/// <param name="createdByParticipantId">
	/// The identifier of the participant performing the upload (a user or persona), or
	/// <see langword="null"/> for system-initiated uploads.
	/// </param>
	/// <param name="utcNow">
	/// The UTC timestamp to record as the creation time, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the persona was found and the avatar was saved;
	/// <see langword="false"/> if the persona was not found.
	/// </returns>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	Task<bool> SaveAvatarAsync(
		Guid              publicId,
		Stream            content,
		string            contentType,
		ParticipantId?    createdByParticipantId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates an existing persona's metadata, participant display name, visibility, and optionally creates
	/// a new system prompt version.
	/// </summary>
	/// <param name="publicId">The public identifier of the persona's linked participant.</param>
	/// <param name="displayName">The updated display name.</param>
	/// <param name="descriptionTranslations">
	/// A dictionary mapping culture codes (e.g. <c>en</c>, <c>de</c>) to localized description values.
	/// When provided, replaces all existing description translations.
	/// Set to <see langword="null"/> to leave existing translations unchanged.
	/// </param>
	/// <param name="defaultModel">The updated default model identifier.</param>
	/// <param name="systemPrompt">
	/// The system prompt text. When it differs from the current active prompt,
	/// a new version is created (with deduplication via SHA-256 hash).
	/// Set to <see langword="null"/> to clear the active prompt.
	/// </param>
	/// <param name="visibility">The updated visibility scope.</param>
	/// <param name="isActive">Whether the persona should be active.</param>
	/// <param name="utcNow">
	/// The timestamp for any newly created system prompt, or <see langword="null"/> to use the service's configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The updated <see cref="PersonaEntity"/> with its <see cref="PersonaEntity.Participant"/> and
	/// <see cref="PersonaEntity.ActiveSystemPrompt"/> loaded, or <see langword="null"/> if the persona was
	/// not found.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="publicId"/> is <see cref="Guid.Empty"/>, or <paramref name="displayName"/> is
	/// empty/whitespace or exceeds the configured maximum length, or <paramref name="defaultModel"/>
	/// exceeds the configured maximum length.
	/// </exception>
	Task<PersonaEntity?> UpdatePersonaAsync(
		Guid                                 publicId,
		string                               displayName,
		IReadOnlyDictionary<string, string>? descriptionTranslations,
		string?                              defaultModel,
		string?                              systemPrompt,
		PersonaVisibility                    visibility,
		bool                                 isActive,
		DateTime?                            utcNow            = null,
		CancellationToken                    cancellationToken = default);

	#endregion
}
