// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents an AI persona that can participate in conversations.
/// </summary>
/// <remarks>
///     <para>
///     Personas are AI characters with distinct personalities, behaviors, and configurations.
///     Each persona has a linked <see cref="ParticipantEntity"/> that provides the unified identity for conversations.
///     </para>
///     <para>
///     The persona's behavior is primarily defined by its system prompts, which are stored separately in
///     <see cref="SystemPromptEntity"/> to enable versioning and deduplication.
///     </para>
///     <para>
///         <b>Identifiers:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             This entity uses an internal numeric <see cref="Id"/> as primary key.
///             </description>
///         </item>
///         <item>
///             <description>
///             External references should use the linked participant's <see cref="ParticipantEntity.PublicId"/>.
///             </description>
///         </item>
///     </list>
///     <para>
///         <b>Constraints:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             The database enforces a 1:1 relationship between <see cref="PersonaEntity"/> and
///             <see cref="ParticipantEntity"/>
///             via <see cref="ParticipantId"/>.
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="CreatedByParticipantId"/> is an optional FK to the <see cref="ParticipantEntity"/> that
///             created this persona. <see langword="null"/> indicates a system-created persona (e.g. seeded defaults).
///             </description>
///         </item>
///     </list>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class PersonaEntity
{
	// --- 1. Primary key ---

	/// <summary>
	/// Gets or sets the internal unique identifier for database relationships.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Auto-incremented by the database. Never exposed via APIs.
	///     </para>
	///     <para>
	///     <b>Index:</b> Primary key.
	///     </para>
	/// </remarks>
	public PersonaId Id { get; set; }

	// --- 2. Public identifier (none) ---

	// --- 3. Foreign keys + Navigation properties ---

	/// <summary>
	/// Gets or sets the foreign key to the associated participant.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Points to <see cref="ParticipantEntity.Id"/>.
	///     The database enforces uniqueness (1:1 with <see cref="ParticipantEntity"/>).
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public ParticipantId ParticipantId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the associated participant.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This relationship is required at the database level via <see cref="ParticipantId"/>, but the navigation may be
	///     <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ParticipantEntity? Participant { get; set; }

	/// <summary>
	/// Gets or sets the foreign key to the participant that created this persona.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Points to <see cref="ParticipantEntity.Id"/>.
	///     <see langword="null"/> indicates a system-created persona (e.g. the default seed).
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index to support filtering personas by creator.
	///     </para>
	/// </remarks>
	public ParticipantId? CreatedByParticipantId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the participant that created this persona.
	/// </summary>
	/// <remarks>
	///     <para>
	///     May be <see langword="null"/> if the creator is not loaded or if this is a system-created persona.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ParticipantEntity? CreatedByParticipant { get; set; }

	/// <summary>
	/// Gets or sets the foreign key to the currently active system prompt.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Points to <see cref="SystemPromptEntity.Id"/>.
	///     </para>
	///     <para>
	///     <see langword="null"/> if no active prompt is configured.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index.
	///     </para>
	///     <para>
	///     This property identifies which prompt is currently used for new message generation.
	///     Historical prompts remain available via <see cref="SystemPrompts"/> and can be referenced by
	///     <see cref="MessageGenerationMetadataEntity.SystemPromptId"/> for auditing.
	///     </para>
	/// </remarks>
	public SystemPromptId? ActiveSystemPromptId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the currently active system prompt.
	/// </summary>
	/// <remarks>
	///     <para>
	///     May be <see langword="null"/> if no active prompt is configured.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public SystemPromptEntity? ActiveSystemPrompt { get; set; }

	// --- 4. Timestamps ---

	/// <summary>
	/// Gets or sets the UTC timestamp when this persona was created.
	/// </summary>
	/// <remarks>
	/// Stamped once on insert by the data layer and never modified afterwards. The data layer treats this column as
	/// required so consumers can rely on a meaningful value without coalescing. Distinct from <see cref="UpdatedAtUtc"/>,
	/// which tracks the most recent mutation; <see cref="CreatedAtUtc"/> supports audit, cohort, and "newly authored
	/// personas" queries.
	/// </remarks>
	public DateTime CreatedAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this persona was last updated.
	/// </summary>
	/// <remarks>
	/// Initialized to the persona's creation timestamp on insert and refreshed by every <c>Update*</c>
	/// service method that mutates this entity (including system-prompt switches). The data layer treats
	/// this column as required so consumers can rely on a meaningful value without coalescing.
	/// </remarks>
	public DateTime UpdatedAtUtc { get; set; }

	// --- 5. Scalar domain fields ---

	/// <summary>
	/// Gets or sets the default model identifier for this persona.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Specifies which LLM model to use by default when generating responses for this persona.
	///     Can be overridden per conversation or message.
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.ModelIdentifierMaxLength"/>.
	///     </para>
	///     <para>
	///     Examples: <c>mistral:7b</c>, <c>llama3.1:8b-instruct-q4_0</c>, <c>gpt-4</c>
	///     </para>
	/// </remarks>
	public string? DefaultModel { get; set; }

	/// <summary>
	/// Gets or sets whether this persona is currently active and available for conversations.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Inactive personas are not available for new conversations, but retain their history.
	///     The database sets the default to <see langword="true"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Non-unique index to support filtering active personas.
	///     </para>
	/// </remarks>
	public bool IsActive { get; set; } = true;

	/// <summary>
	/// Gets or sets the visibility scope of this persona.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <see cref="PersonaVisibility.Private"/> personas are only visible to their creator.
	///     <see cref="PersonaVisibility.Shared"/> personas are discoverable by all authenticated users.
	///     </para>
	///     <para>
	///     The database stores this as an integer column with a default of <see cref="PersonaVisibility.Private"/>.
	///     </para>
	/// </remarks>
	public PersonaVisibility Visibility { get; set; } = PersonaVisibility.Private;

	// --- 6. Collection navigation properties ---

	/// <summary>
	/// Gets the collection of system prompts associated with this persona.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Multiple prompts allow for versioning; the most recent active prompt is typically used for new conversations.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<SystemPromptEntity> SystemPrompts { get; set; } = [];

	/// <summary>
	/// Gets the localized description translations for this persona.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Each entry maps a BCP 47 culture code (e.g., "en", "de") to a translated description.
	///     When displaying a persona, the UI selects the entry matching the user's locale.
	///     </para>
	///     <para>
	///     Unlike <see cref="SystemPrompts"/>, this collection is included in full-fat Read API queries
	///     despite being an <see cref="ICollection{T}"/>. The rationale: it is bounded by the number of
	///     supported locales (typically 2–10 entries), has no nested navigations, and is essential for
	///     displaying the persona description — without it the persona would appear incomplete.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<PersonaDescriptionTranslationEntity> DescriptionTranslations { get; set; } = [];
}
