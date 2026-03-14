// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a system prompt used to instruct an AI persona's behavior.
/// </summary>
/// <remarks>
///     <para>
///     System prompts define the persona's personality, tone, knowledge boundaries, and behavioral guidelines.
///     They are stored separately from the persona to enable versioning and deduplication.
///     </para>
///     <para>
///     When a prompt is used for message generation, the <see cref="MessageGenerationMetadataEntity"/> references
///     this entity. This ensures that even if a persona's prompt changes, historical messages retain a reference
///     to the exact prompt that was used.
///     </para>
///     <para>
///     The <see cref="Hash"/> field enables deduplication: if the same prompt text is used multiple times,
///     only one row is stored and referenced.
///     </para>
///     <para>
///         <b>Identifiers:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <see cref="Id"/> is the internal identifier used for database relationships.
///             </description>
///         </item>
///         <item>
///             <description>
///             <see cref="PublicId"/> is intended for stable external references.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Constraints:</b> The database enforces uniqueness for <see cref="Hash"/> per <see cref="PersonaId"/>
///     (i.e. a persona cannot store the same prompt content twice).
///     </para>
///     <para>
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class SystemPromptEntity
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
	public SystemPromptId Id { get; set; }

	/// <summary>
	/// Gets or sets the public unique identifier for external references.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Exposed via APIs and remains stable across database migrations.
	///     </para>
	///     <para>
	///     <b>Index:</b> Unique index.
	///     </para>
	/// </remarks>
	public Guid PublicId { get; set; }

	/// <summary>
	/// Gets or sets the foreign key to the persona this prompt belongs to.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Points to <see cref="PersonaEntity.Id"/>.
	///     </para>
	///     <para>
	///     <b>Index:</b> Composite unique index <c>(PersonaId, Hash)</c>.
	///     </para>
	/// </remarks>
	public PersonaId PersonaId { get; set; }

	/// <summary>
	/// Gets or sets the navigation property to the persona.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     This relationship is required at the database level via <see cref="PersonaId"/>,
	///     but the navigation may be <see langword="null"/> if it was not loaded.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public PersonaEntity? Persona { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this prompt was created.
	/// </summary>
	public DateTime CreatedAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the full text of the system prompt.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is the complete instruction set sent to the LLM before the conversation history.
	///     </para>
	///     <para>
	///     Can be several kilobytes in size for detailed persona definitions.
	///     </para>
	///     <para>
	///     The database does not currently enforce a maximum length; consider provider-specific limits and application-level
	///     safeguards.
	///     </para>
	/// </remarks>
	// ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
	public string Content { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the SHA-256 hash of the content for deduplication.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Before inserting a new prompt, check if a prompt with the same hash already exists for this persona.
	///     If so, reuse the existing row instead of creating a duplicate.
	///     </para>
	///     <para>
	///     The database enforces a fixed maximum length (hex-encoded SHA-256).
	///     </para>
	/// </remarks>
	public string Hash { get; set; } = string.Empty;

	/// <summary>
	/// Gets the collection of message generation metadata that used this prompt.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Navigation property for Entity Framework Core.
	///     </para>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<MessageGenerationMetadataEntity> GenerationMetadata { get; set; } = [];
}
