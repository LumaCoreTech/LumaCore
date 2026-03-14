// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

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
///     </list>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class PersonaEntity
{
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
	///     Navigation property for Entity Framework Core.
	///     </para>
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
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     May be <see langword="null"/> if no active prompt is configured.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public SystemPromptEntity? ActiveSystemPrompt { get; set; }

	/// <summary>
	/// Gets or sets the default model identifier for this persona.
	/// </summary>
	/// <remarks>
	/// Specifies which LLM model to use by default when generating responses for this persona.
	/// Can be overridden per conversation or message.
	/// 
	/// The database enforces a maximum length.
	/// 
	/// Examples: <c>mistral:7b</c>, <c>llama3.1:8b-instruct-q4_0</c>, <c>gpt-4</c>
	/// </remarks>
	public string? DefaultModel { get; set; }

	/// <summary>
	/// Gets or sets a brief description of the persona's character and purpose.
	/// </summary>
	/// <remarks>
	/// Provides a short human-readable description of the persona.
	/// The database enforces a maximum length.
	/// </remarks>
	public string? Description { get; set; }

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
	/// Gets the collection of system prompts associated with this persona.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     Multiple prompts allow for versioning; the most recent active prompt is typically used for new conversations.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<SystemPromptEntity> SystemPrompts { get; set; } = [];
}
