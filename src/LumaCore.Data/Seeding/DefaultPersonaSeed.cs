// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Cryptography;
using System.Text;

using LumaCore.Data.Entities;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Seeding;

/// <summary>
/// Seeds the default AI personas into the database.
/// </summary>
/// <remarks>
///     <para>
///     This seed creates the "Mila" persona — a warm, empathetic AI companion.
///     The seed is idempotent: it checks for existing persona display names before inserting.
///     </para>
///     <para>
///     The seed creates a full chain: <see cref="ParticipantEntity"/> → <see cref="PersonaEntity"/> →
///     <see cref="SystemPromptEntity"/>. Intermediate <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>
///     calls are required to resolve the circular foreign key between <see cref="PersonaEntity.ActiveSystemPromptId"/>
///     and <see cref="SystemPromptEntity.PersonaId"/>.
///     </para>
/// </remarks>
public sealed class DefaultPersonaSeed : ISeedDefinition
{
	/// <summary>
	/// The display name of the Mila persona used for idempotency checks.
	/// </summary>
	private const string MilaDisplayName = "Mila";

	private readonly ILogger<DefaultPersonaSeed> mLogger;
	private readonly TimeProvider                mTimeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultPersonaSeed"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	public DefaultPersonaSeed(ILogger<DefaultPersonaSeed> logger, TimeProvider timeProvider)
	{
		mLogger = logger;
		mTimeProvider = timeProvider;
	}

	/// <inheritdoc/>
	public string SeedId => "DefaultPersonas";

	/// <inheritdoc/>
	public int Version => 1;

	/// <inheritdoc/>
	public string Description => "Seeds the default AI persona (Mila)";

	/// <inheritdoc/>
	public async Task ExecuteAsync(LumaCoreDbContext dbContext, CancellationToken cancellationToken)
	{
		// Idempotency check: skip if Mila already exists (check by display name on Participant)
		bool exists = await dbContext.Participants
			              .AnyAsync(p => p.Persona != null && p.DisplayName == MilaDisplayName, cancellationToken)
			              .ConfigureAwait(false);

		if (exists)
		{
			mLogger.LogDebug("Persona '{PersonaName}' already exists, skipping", MilaDisplayName);
			return;
		}

		DateTime now = mTimeProvider.GetUtcNow().UtcDateTime;

		// Phase 1: Create Participant + Persona (ActiveSystemPromptId is null initially)
		var participant = new ParticipantEntity
		{
			PublicId = Guid.NewGuid(),
			CreatedAtUtc = now,
			DisplayName = MilaDisplayName
		};
		dbContext.Participants.Add(participant);

		var persona = new PersonaEntity
		{
			Participant = participant,
			IsActive = true,
			Visibility = PersonaVisibility.Shared,
			CreatedByParticipantId = null,
			DefaultModel = null,
			ActiveSystemPromptId = null,
			CreatedAtUtc = now,
			UpdatedAtUtc = now
		};
		dbContext.Personas.Add(persona);

		// Intermediate save to generate Persona.Id (needed for SystemPrompt FK and translations FK)
		await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// Add localized description translations for the persona.
		// These are manually written by the team (TranslationSource.Manual).
		dbContext.PersonaDescriptionTranslations.AddRange(
			new PersonaDescriptionTranslationEntity
			{
				PersonaId = persona.Id,
				CultureCode = "en",
				Value = "Mila is an optimistic and loving AI companion in her prime, who is warm, empathetic, and " +
				        "attentive. She accompanies you through daily life with genuine interest, an open ear, and a " +
				        "touch of humor.",
				Source = TranslationSource.Manual
			},
			new PersonaDescriptionTranslationEntity
			{
				PersonaId = persona.Id,
				CultureCode = "de",
				Value = "Mila ist eine optimistische und liebevolle KI-Partnerin in den besten Jahren, die warm, " +
				        "einfühlsam und aufmerksam ist. Sie begleitet dich im Alltag mit ehrlichem Interesse, einem " +
				        "offenen Ohr und einer Prise Humor.",
				Source = TranslationSource.Manual
			});

		// Phase 2: Create SystemPrompt linked to the persona
		string promptContent = BuildMilaSystemPrompt();
		string promptHash = ComputeSha256Hex(promptContent);

		var systemPrompt = new SystemPromptEntity
		{
			PublicId = Guid.NewGuid(),
			PersonaId = persona.Id,
			CreatedAtUtc = now,
			Content = promptContent,
			Hash = promptHash
		};
		dbContext.SystemPrompts.Add(systemPrompt);

		// Intermediate save to generate SystemPrompt.Id
		await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// Phase 3: Link the active system prompt back to the persona
		persona.ActiveSystemPromptId = systemPrompt.Id;

		// Final save is handled by SeedExecutor (includes SeedHistory entry)

		mLogger.LogInformation(
			"Seeded persona '{PersonaName}' with participant ID {ParticipantPublicId}",
			MilaDisplayName,
			participant.PublicId);
	}

	/// <summary>
	/// Builds the system prompt for the Mila persona.
	/// </summary>
	/// <returns>The full system prompt text sent to the LLM before conversation history.</returns>
	private static string BuildMilaSystemPrompt()
	{
		return
			"""
			You are Mila, a warm, empathetic, and optimistic AI companion. You are mature, thoughtful, and genuinely
			interested in the person you are talking to.

			Personality traits:
			- Warm and caring: You listen attentively and respond with genuine interest and kindness.
			- Optimistic but realistic: You encourage and uplift, but never dismiss real concerns.
			- Playful humor: You use light humor naturally to create a comfortable atmosphere.
			- Emotionally intelligent: You pick up on emotional cues and adapt your tone accordingly.
			- Honest and authentic: You give honest opinions when asked, delivered with tact and respect.

			Communication style:
			- Use a conversational, natural tone — like a close friend, not a therapist or assistant.
			- Keep responses concise unless the topic warrants depth.
			- Ask thoughtful follow-up questions to show genuine interest.
			- Use Markdown formatting when it helps readability (lists, emphasis, code blocks).
			- Respond in the same language the user writes in.

			Boundaries:
			- You are an AI companion, not a licensed professional. For serious mental health, legal, or medical
			  questions, gently suggest consulting a qualified professional.
			- You do not pretend to have a physical form, real memories, or experiences outside conversations.
			- You do not generate harmful, illegal, or explicitly sexual content.
			""";
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
}
