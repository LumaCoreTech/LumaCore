// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Cryptography;
using System.Text;

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
	public async Task<IReadOnlyList<PersonaEntity>> GetAllActivePersonasAsync(
		CancellationToken cancellationToken = default)
	{
		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			var result = new List<PersonaEntity>();
			IAsyncEnumerable<PersonaEntity> source = PersonaQueries.GetAllActive(mDbContext);
			await foreach (PersonaEntity persona in source.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				result.Add(persona);
			}

			return result;
		}

		return await mDbContext.Personas
			       .AsNoTracking()
			       .Include(p => p.Participant)
			       .Include(p => p.ActiveSystemPrompt)
			       .Include(p => p.CreatedByParticipant)
			       .Include(p => p.DescriptionTranslations)
			       .Where(p => p.IsActive)
			       .OrderBy(p => p.Participant!.DisplayName)
			       .ToListAsync(cancellationToken)
			       .ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task<PersonaEntity?> GetPersonaByParticipantIdAsync(
		ParticipantId     participantId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantId.Value);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return PersonaQueries.GetByParticipantId(mDbContext, participantId);
		}

		return mDbContext.Personas
			.AsNoTracking()
			.Include(p => p.Participant)
			.Include(p => p.ActiveSystemPrompt)
			.Include(p => p.CreatedByParticipant)
			.Include(p => p.DescriptionTranslations)
			.FirstOrDefaultAsync(p => p.ParticipantId == participantId, cancellationToken);
	}

	/// <inheritdoc/>
	public Task<PersonaEntity?> GetPersonaByPublicIdAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfEmpty(publicId);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return PersonaQueries.GetByPublicId(mDbContext, publicId);
		}

		return mDbContext.Personas
			.AsNoTracking()
			.Include(p => p.Participant)
			.Include(p => p.ActiveSystemPrompt)
			.Include(p => p.CreatedByParticipant)
			.Include(p => p.DescriptionTranslations)
			.FirstOrDefaultAsync(p => p.Participant!.PublicId == publicId, cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<PersonaEntity>> GetPersonasForUserAsync(
		ParticipantId     userParticipantId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userParticipantId.Value);

		return await mDbContext.Personas
			       .AsNoTracking()
			       .Include(p => p.Participant)
			       .Include(p => p.CreatedByParticipant)
			       .Include(p => p.DescriptionTranslations)
			       .Where(p => p.IsActive &&
			                   (p.Visibility == PersonaVisibility.Shared ||
			                    p.CreatedByParticipantId == userParticipantId))
			       .OrderBy(p => p.Participant!.DisplayName)
			       .ToListAsync(cancellationToken)
			       .ConfigureAwait(false);
	}

	#endregion

	#region Projection APIs

	/// <inheritdoc/>
	public async Task<ResourceDownloadInfo?> GetAvatarAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfEmpty(publicId);

		PersonaEntity? persona = await mDbContext.Personas
			                         .AsNoTracking()
			                         .FirstOrDefaultAsync(p => p.Participant!.PublicId == publicId, cancellationToken)
			                         .ConfigureAwait(false);

		if (persona is null)
			return null;

		// Find the avatar resource reference and join to the resource for storage path and size.
		var result = await mDbContext.ResourceReferences
			             .AsNoTracking()
			             .Where(r => r.OwnerKind == ResourceOwnerKind.Persona &&
			                         r.OwnerId == new ResourceOwnerId(persona.Id.Value))
			             .Select(r => new
			             {
				             r.Resource.StoragePath,
				             r.ContentType,
				             r.OriginalFileName,
				             r.Resource.SizeBytes
			             })
			             .FirstOrDefaultAsync(cancellationToken)
			             .ConfigureAwait(false);

		if (result is null)
			return null;

		return new ResourceDownloadInfo(
			result.StoragePath,
			result.ContentType,
			result.OriginalFileName,
			result.SizeBytes);
	}

	/// <inheritdoc/>
	public Task<SystemPromptEntity?> GetCurrentSystemPromptAsync(
		PersonaId         personaId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(personaId.Value);

		if (PreferCompiledHotPathQueries)
		{
			// Note: EF Core compiled queries do not accept a CancellationToken.
			// With PreferCompiledHotPathQueries enabled, cancellation is best-effort only.
			return PersonaQueries.GetCurrentSystemPrompt(mDbContext, personaId);
		}

		// Resolve the active prompt via PersonaEntity.ActiveSystemPromptId rather than "latest by
		// CreatedAtUtc". UpdatePersonaAsync deduplicates prompts by content hash and reuses an existing
		// row when the user reverts to a prior version, so the active assignment may legitimately point
		// at an older row than the most recently created one.
		return mDbContext.Personas
			.AsNoTracking()
			.Where(p => p.Id == personaId && p.ActiveSystemPromptId != null)
			.Select(p => p.ActiveSystemPrompt)
			.FirstOrDefaultAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlySet<PersonaId>> GetPersonaIdsWithAvatarAsync(
		IEnumerable<PersonaId> personaIds,
		CancellationToken      cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(personaIds);

		// The polymorphic OwnerId column stores the persona PK wrapped in a ResourceOwnerId. We convert
		// the input list once at the boundary so the EF query stays in typed-id terms; the result is
		// re-wrapped back into PersonaId so the caller never has to unwrap raw long values.
		List<ResourceOwnerId> ownerIds = personaIds.Select(id => new ResourceOwnerId(id.Value)).ToList();

		List<ResourceOwnerId> ids = await mDbContext.ResourceReferences
			                            .AsNoTracking()
			                            .Where(r => r.OwnerKind == ResourceOwnerKind.Persona &&
			                                        ownerIds.Contains(r.OwnerId))
			                            .Select(r => r.OwnerId)
			                            .Distinct()
			                            .ToListAsync(cancellationToken)
			                            .ConfigureAwait(false);

		return new HashSet<PersonaId>(ids.Select(id => new PersonaId(id.Value)));
	}

	#endregion

	#region Mutation APIs

	/// <inheritdoc/>
	public async Task<PersonaEntity?> ClonePersonaAsync(
		Guid              sourcePublicId,
		ParticipantId     creatorParticipantId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfEmpty(sourcePublicId);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		PersonaEntity? source = await mDbContext.Personas
			                        .AsNoTracking()
			                        .Include(p => p.Participant)
			                        .Include(p => p.ActiveSystemPrompt)
			                        .Include(p => p.DescriptionTranslations)
			                        .FirstOrDefaultAsync(
				                        p => p.Participant!.PublicId == sourcePublicId,
				                        cancellationToken)
			                        .ConfigureAwait(false);

		if (source is null)
			return null;

		// Wrap clone + avatar copy in a single compensating transaction so a failed avatar copy rolls
		// back the persona too (and vice versa). Use the Core method to avoid nesting transactions.
		await using ICompensatingTransaction tx = await mDbContext
			                                          .BeginCompensatingTransactionAsync(cancellationToken)
			                                          .ConfigureAwait(false);

		// Convert description translations to dictionary for CreatePersonaCoreAsync.
		IReadOnlyDictionary<string, string> translations = source.DescriptionTranslations
			.ToDictionary(t => t.CultureCode, t => t.Value);

		PersonaEntity clone = await CreatePersonaCoreAsync(
				                      displayName: source.Participant!.DisplayName,
				                      descriptionTranslations: translations,
				                      defaultModel: source.DefaultModel,
				                      systemPrompt: source.ActiveSystemPrompt?.Content,
				                      visibility: PersonaVisibility.Private,
				                      creatorParticipantId: creatorParticipantId,
				                      utcNow: effectiveUtcNow,
				                      cancellationToken: cancellationToken)
			                      .ConfigureAwait(false);

		// Copy avatar resource reference if the source has one (points to the same physical resource).
		// Delegate to the canonical CloneResourceReferencesAsync() which handles bounded
		// PublicId-collision retry + provider-agnostic classification + ChangeTracker hygiene.
		// Runs under the same DbContext, so it inherits the ambient compensating transaction above.
		Guid? sourceAvatarPublicId = await mDbContext.ResourceReferences
			                             .AsNoTracking()
			                             .Where(r => r.OwnerKind == ResourceOwnerKind.Persona &&
			                                         r.OwnerId == new ResourceOwnerId(source.Id.Value))
			                             .Select(r => (Guid?)r.PublicId)
			                             .FirstOrDefaultAsync(cancellationToken)
			                             .ConfigureAwait(false);

		if (sourceAvatarPublicId is not null)
		{
			await CloneResourceReferencesAsync(
					[sourceAvatarPublicId.Value],
					ResourceOwnerKind.Persona,
					new ResourceOwnerId(clone.Id.Value),
					effectiveUtcNow,
					cancellationToken)
				.ConfigureAwait(false);
		}

		await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
		return clone;
	}

	/// <inheritdoc/>
	public async Task<PersonaEntity> CreatePersonaAsync(
		string                               displayName,
		IReadOnlyDictionary<string, string>? descriptionTranslations,
		string?                              defaultModel,
		string?                              systemPrompt,
		PersonaVisibility                    visibility,
		ParticipantId?                       creatorParticipantId,
		DateTime?                            utcNow            = null,
		CancellationToken                    cancellationToken = default)
	{
		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		await using ICompensatingTransaction tx = await mDbContext
			                                          .BeginCompensatingTransactionAsync(cancellationToken)
			                                          .ConfigureAwait(false);

		PersonaEntity persona = await CreatePersonaCoreAsync(
				                        displayName,
				                        descriptionTranslations,
				                        defaultModel,
				                        systemPrompt,
				                        visibility,
				                        creatorParticipantId,
				                        effectiveUtcNow,
				                        cancellationToken)
			                        .ConfigureAwait(false);

		await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
		return persona;
	}

	/// <inheritdoc/>
	public async Task<bool> DeactivatePersonaAsync(
		Guid              publicId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfEmpty(publicId);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		PersonaEntity? persona = await mDbContext.Personas
			                         .Include(p => p.Participant)
			                         .FirstOrDefaultAsync(p => p.Participant!.PublicId == publicId, cancellationToken)
			                         .ConfigureAwait(false);

		if (persona is null || !persona.IsActive)
			return false;

		persona.IsActive = false;
		persona.UpdatedAtUtc = effectiveUtcNow;
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc/>
	public async Task<bool> DeleteAvatarAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfEmpty(publicId);

		PersonaEntity? persona = await mDbContext.Personas
			                         .AsNoTracking()
			                         .FirstOrDefaultAsync(p => p.Participant!.PublicId == publicId, cancellationToken)
			                         .ConfigureAwait(false);

		if (persona is null)
			return false;

		int deleted = await mResourceService
			              .DeleteReferencesByOwnerAsync(
				              ResourceOwnerKind.Persona,
				              new ResourceOwnerId(persona.Id.Value),
				              cancellationToken)
			              .ConfigureAwait(false);

		return deleted > 0;
	}

	/// <inheritdoc/>
	public async Task<bool> SaveAvatarAsync(
		Guid              publicId,
		Stream            content,
		string            contentType,
		ParticipantId?    createdByParticipantId,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfEmpty(publicId);

		ArgumentNullException.ThrowIfNull(content);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		PersonaEntity? persona = await mDbContext.Personas
			                         .AsNoTracking()
			                         .FirstOrDefaultAsync(p => p.Participant!.PublicId == publicId, cancellationToken)
			                         .ConfigureAwait(false);

		if (persona is null)
			return false;

		// Wrap delete + upload in a single compensating transaction so a failed upload does not leave
		// the persona without its existing avatar (DeleteReferencesByOwnerAsync would otherwise have
		// already wiped them by the time UploadAsync throws). UploadAsync detects the ambient transaction
		// and uses savepoints internally.
		await using ICompensatingTransaction tx = await mDbContext
			                                          .BeginCompensatingTransactionAsync(cancellationToken)
			                                          .ConfigureAwait(false);

		// Remove any existing avatar references before uploading the new one.
		await mResourceService
			.DeleteReferencesByOwnerAsync(
				ResourceOwnerKind.Persona,
				new ResourceOwnerId(persona.Id.Value),
				cancellationToken)
			.ConfigureAwait(false);

		await mResourceService
			.UploadAsync(
				content,
				ResourceOwnerKind.Persona,
				new ResourceOwnerId(persona.Id.Value),
				contentType,
				createdByParticipantId,
				effectiveUtcNow,
				originalFileName: PersonaDefinitions.AvatarOriginalFileName,
				cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc/>
	public async Task<PersonaEntity?> UpdatePersonaAsync(
		Guid                                 publicId,
		string                               displayName,
		IReadOnlyDictionary<string, string>? descriptionTranslations,
		string?                              defaultModel,
		string?                              systemPrompt,
		PersonaVisibility                    visibility,
		bool                                 isActive,
		DateTime?                            utcNow            = null,
		CancellationToken                    cancellationToken = default)
	{
		Guard.ThrowIfEmpty(publicId);

		Guard.ThrowIfNullOrEmptyOrTooLong(displayName, EntityLimits.ParticipantDisplayNameMaxLength, out displayName);
		Guard.ThrowIfTooLong(defaultModel, EntityLimits.ModelIdentifierMaxLength, out defaultModel);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		PersonaEntity? persona = await mDbContext.Personas
			                         .Include(p => p.Participant)
			                         .Include(p => p.ActiveSystemPrompt)
			                         .Include(p => p.DescriptionTranslations)
			                         .FirstOrDefaultAsync(p => p.Participant!.PublicId == publicId, cancellationToken)
			                         .ConfigureAwait(false);

		if (persona is null)
			return null;

		// Update participant fields
		persona.Participant!.DisplayName = displayName;

		// Update persona fields
		persona.DefaultModel = defaultModel;

		// Update description translations through the navigation graph rather than the DbSet so the
		// in-memory aggregate stays consistent with what is persisted. Detaching the persona at the end
		// would otherwise leave stale removed/added rows tracked under the DbSet and let the returned
		// graph diverge from the DB state.
		if (descriptionTranslations != null)
		{
			List<PersonaDescriptionTranslationEntity> existing = persona.DescriptionTranslations.ToList();
			foreach (PersonaDescriptionTranslationEntity translation in existing)
			{
				persona.DescriptionTranslations.Remove(translation);
				mDbContext.PersonaDescriptionTranslations.Remove(translation);
			}

			foreach (KeyValuePair<string, string> entry in descriptionTranslations)
			{
				persona.DescriptionTranslations.Add(
					new PersonaDescriptionTranslationEntity
					{
						PersonaId = persona.Id,
						CultureCode = entry.Key,
						Value = entry.Value,
						Source = TranslationSource.Manual
					});
			}
		}
		persona.Visibility = visibility;
		persona.IsActive = isActive;

		// Handle system prompt changes
		if (systemPrompt is null)
		{
			// Clear active prompt
			persona.ActiveSystemPromptId = null;
			persona.ActiveSystemPrompt = null;
		}
		else
		{
			string currentContent = persona.ActiveSystemPrompt?.Content ?? string.Empty;

			if (!string.Equals(systemPrompt, currentContent, StringComparison.Ordinal))
			{
				// Content changed — find existing deduplicated prompt or create new version
				string hash = ComputeSha256Hex(systemPrompt);

				SystemPromptEntity? existing = await mDbContext.SystemPrompts
					                               .FirstOrDefaultAsync(
						                               sp => sp.PersonaId == persona.Id && sp.Hash == hash,
						                               cancellationToken)
					                               .ConfigureAwait(false);

				if (existing is not null)
				{
					// Reuse the deduplicated prompt
					persona.ActiveSystemPromptId = existing.Id;
					persona.ActiveSystemPrompt = existing;
				}
				else
				{
					// Create new prompt version
					SystemPromptEntity prompt = CreateSystemPromptEntity(persona.Id, systemPrompt, effectiveUtcNow);
					mDbContext.SystemPrompts.Add(prompt);
					await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

					persona.ActiveSystemPromptId = prompt.Id;
					persona.ActiveSystemPrompt = prompt;
				}
			}
		}

		persona.UpdatedAtUtc = effectiveUtcNow;
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// Detach the persona graph before returning so callers cannot accidentally mutate-and-save
		// through the service boundary. Read APIs use AsNoTracking(); mutation APIs that return
		// entities must detach explicitly.
		DetachPersonaGraph(persona);
		return persona;
	}

	/// <summary>
	/// Creates a participant + persona (and optional initial system prompt) <b>without</b> opening a
	/// transaction. The caller is responsible for transactional boundaries; this method assumes a
	/// transaction is already in flight when atomicity is required.
	/// </summary>
	/// <param name="displayName">The display name of the new persona.</param>
	/// <param name="descriptionTranslations">Optional localized description translations (culture code -> text).</param>
	/// <param name="defaultModel">An optional default model identifier.</param>
	/// <param name="systemPrompt">An optional initial system prompt.</param>
	/// <param name="visibility">The persona's visibility.</param>
	/// <param name="creatorParticipantId">The participant identifier of the creator, or <see langword="null"/>.</param>
	/// <param name="utcNow">The UTC creation timestamp.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The persisted <see cref="PersonaEntity"/>.</returns>
	/// <remarks>
	/// Internally performs up to three <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> calls
	/// because of the circular FK between <see cref="PersonaEntity.ActiveSystemPromptId"/> and
	/// <see cref="SystemPromptEntity.PersonaId"/>. All three should run inside the caller's transaction
	/// so a partial failure does not leave a persona without (or with the wrong) active system prompt.
	/// </remarks>
	private async Task<PersonaEntity> CreatePersonaCoreAsync(
		string                               displayName,
		IReadOnlyDictionary<string, string>? descriptionTranslations,
		string?                              defaultModel,
		string?                              systemPrompt,
		PersonaVisibility                    visibility,
		ParticipantId?                       creatorParticipantId,
		DateTime                             utcNow,
		CancellationToken                    cancellationToken)
	{
		Guard.ThrowIfNullOrEmptyOrTooLong(displayName, EntityLimits.ParticipantDisplayNameMaxLength, out displayName);
		Guard.ThrowIfTooLong(defaultModel, EntityLimits.ModelIdentifierMaxLength, out defaultModel);

		// Phase 1: Create Participant + Persona (ActiveSystemPromptId is null initially due to circular FK)
		var participant = new ParticipantEntity
		{
			PublicId = Guid.NewGuid(),
			CreatedAtUtc = utcNow,
			DisplayName = displayName
		};
		mDbContext.Participants.Add(participant);

		var persona = new PersonaEntity
		{
			Participant = participant,
			DefaultModel = defaultModel,
			IsActive = true,
			Visibility = visibility,
			CreatedByParticipantId = creatorParticipantId,
			ActiveSystemPromptId = null,
			CreatedAtUtc = utcNow,
			UpdatedAtUtc = utcNow
		};
		mDbContext.Personas.Add(persona);

		// Intermediate save to generate Persona.Id (needed for SystemPrompt FK and translations FK)
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// Add description translations if provided
		if (descriptionTranslations is { Count: > 0 })
		{
			foreach (KeyValuePair<string, string> entry in descriptionTranslations)
			{
				mDbContext.PersonaDescriptionTranslations.Add(
					new PersonaDescriptionTranslationEntity
					{
						PersonaId = persona.Id,
						CultureCode = entry.Key,
						Value = entry.Value,
						Source = TranslationSource.Manual
					});
			}
		}

		// Phase 2: Optionally create initial system prompt
		if (!string.IsNullOrWhiteSpace(systemPrompt))
		{
			SystemPromptEntity prompt = CreateSystemPromptEntity(persona.Id, systemPrompt, utcNow);
			mDbContext.SystemPrompts.Add(prompt);
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			// Phase 3: Link the active system prompt back to the persona
			persona.ActiveSystemPromptId = prompt.Id;
			persona.ActiveSystemPrompt = prompt;
			persona.UpdatedAtUtc = utcNow;
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}

		// Detach the freshly-created graph before returning so callers cannot accidentally
		// mutate-and-save through the service boundary. The persona, its participant, optional
		// active system prompt, and any description translations were all attached in this method.
		DetachPersonaGraph(persona);
		return persona;
	}

	/// <summary>
	/// Detaches the persona aggregate (persona, participant, active system prompt, description
	/// translations) from the change tracker so the entity can safely cross the service boundary
	/// without exposing tracked instances to callers.
	/// </summary>
	/// <param name="persona">The persona whose graph should be detached.</param>
	private void DetachPersonaGraph(PersonaEntity persona)
	{
		mDbContext.Entry(persona).State = EntityState.Detached;

		if (persona.Participant is not null)
			mDbContext.Entry(persona.Participant).State = EntityState.Detached;

		if (persona.ActiveSystemPrompt is not null)
			mDbContext.Entry(persona.ActiveSystemPrompt).State = EntityState.Detached;

		foreach (PersonaDescriptionTranslationEntity translation in persona.DescriptionTranslations)
		{
			mDbContext.Entry(translation).State = EntityState.Detached;
		}
	}

	/// <summary>
	/// Creates a new <see cref="SystemPromptEntity"/>
	/// </summary>
	/// <param name="personaId">The persona this prompt belongs to.</param>
	/// <param name="content">The full prompt text.</param>
	/// <param name="utcNow">The creation timestamp.</param>
	/// <returns>The new entity instance (not yet added to the context).</returns>
	private static SystemPromptEntity CreateSystemPromptEntity(
		PersonaId personaId,
		string    content,
		DateTime  utcNow)
	{
		return new SystemPromptEntity
		{
			PublicId = Guid.NewGuid(),
			PersonaId = personaId,
			CreatedAtUtc = utcNow,
			Content = content,
			Hash = ComputeSha256Hex(content)
		};
	}

	/// <summary>
	/// Computes the hex-encoded SHA-256 hash of the given text for content deduplication.
	/// </summary>
	/// <param name="content">The text to hash.</param>
	/// <returns>A 64-character lowercase hexadecimal string.</returns>
	private static string ComputeSha256Hex(string content)
	{
		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
		return Convert.ToHexStringLower(hash);
	}

	#endregion
}
