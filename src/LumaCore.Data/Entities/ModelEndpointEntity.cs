// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

namespace LumaCore.Data.Entities;

/// <summary>
/// Represents a configured model endpoint/backend used for generating AI responses.
/// </summary>
/// <remarks>
///     <para>
///     Model endpoints represent stable backend configurations (e.g. an Ollama instance or an OpenAI-compatible HTTP API)
///     that can be referenced by many generated messages.
///     </para>
///     <para>
///     For historical integrity, endpoint connection details that affect where requests are sent (e.g. base URL and
///     protocol type) should be treated as immutable after creation. Cosmetic fields (name/description) and credentials
///     may be updated.
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
///     Database constraints, indexes, and delete behaviors are configured in <see cref="LumaCoreDbContext"/>.
///     </para>
/// </remarks>
public class ModelEndpointEntity
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
	public ModelEndpointId Id { get; set; }

	// --- 2. Public identifier ---

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

	// --- 3. Foreign keys + Navigation properties (none) ---

	// --- 4. Timestamps ---

	/// <summary>
	/// Gets or sets the UTC timestamp when this endpoint was created.
	/// </summary>
	public DateTime CreatedAtUtc { get; set; }

	/// <summary>
	/// Gets or sets the UTC timestamp when this endpoint was last updated.
	/// </summary>
	/// <remarks>
	/// Initialized to <see cref="CreatedAtUtc"/> on insert and refreshed by every <c>Update*</c> service
	/// method that mutates this entity. The data layer treats this column as required (no nullable,
	/// no implicit fallback) so consumers can rely on a meaningful value without coalescing.
	/// </remarks>
	public DateTime UpdatedAtUtc { get; set; }

	// --- 5. Scalar domain fields ---

	/// <summary>
	/// Gets or sets the endpoint type/protocol identifier.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Examples: <c>ollama</c>, <c>openai-compatible</c>, <c>anthropic</c>.
	///     Treat this value as immutable after creation.
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.ModelEndpointProviderTypeMaxLength"/>.
	///     </para>
	/// </remarks>
	public string ProviderType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the base URL of the endpoint.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Treat this value as immutable after creation to preserve historical reproducibility.
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.ModelEndpointBaseUrlMaxLength"/>.
	///     </para>
	/// </remarks>
	public string BaseUrl { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a human-friendly name for this endpoint.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Cosmetic field shown in administration UIs.
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.ModelEndpointNameMaxLength"/>.
	///     </para>
	/// </remarks>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets an optional description for this endpoint.
	/// </summary>
	/// <remarks>
	/// Maximum length: <see cref="EntityLimits.ModelEndpointDescriptionMaxLength"/>.
	/// </remarks>
	public string? Description { get; set; }

	/// <summary>
	/// Gets or sets whether this endpoint is currently active.
	/// </summary>
	/// <remarks>
	/// <b>Index:</b> Non-unique index to support filtering active endpoints.
	/// </remarks>
	public bool IsActive { get; set; } = true;

	/// <summary>
	/// Gets or sets encrypted credentials for authenticating to this endpoint.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Stored encrypted so a database leak does not directly expose secrets.
	///     The encryption key/material must be provided via configuration or environment variables.
	///     </para>
	///     <para>
	///     Maximum length: <see cref="EntityLimits.ModelEndpointEncryptedCredentialsMaxLength"/>.
	///     </para>
	/// </remarks>
	public string? EncryptedCredentials { get; set; }

	// --- 6. Collection navigation properties ---

	/// <summary>
	/// Gets the collection of generation metadata rows associated with this endpoint.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Load explicitly (e.g. via <c>Include(...)</c>) when required.
	///     </para>
	/// </remarks>
	public ICollection<MessageGenerationMetadataEntity> GenerationMetadata { get; set; } = [];
}
