// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Definitions;

/// <summary>
/// Defines shared maximum length constraints for entity-like data that is validated across layers.
/// </summary>
/// <remarks>
///     <para>
///     This type is intended for <b>cross-layer validation</b> (UI &amp; API) and is shared via the
///     <c>LumaCore.Definitions</c> project to avoid coupling the Blazor WebAssembly client to the EF Core data layer.
///     </para>
///     <para>
///     <b>How this relates to the database:</b> These constants define the <i>shared validation</i> limits.
///     The database schema is defined by the EF Core model configuration in <c>LumaCore.Data.LumaCoreDbContext</c>
///     and the generated migrations/snapshot. The values must stay consistent across all three.
///     </para>
///     <para>
///     <b>Changing limits:</b> Changing a length can require a database schema update (migration). Update the constant,
///     update the EF Core model configuration, and adjust migrations/snapshot accordingly.
///     </para>
///     <para>
///     <b>Rationale:</b> Some limits are derived from well-known standards (e.g. practical email address maximums),
///     some are derived from technical representations (e.g. SHA-256 hex), and others are product decisions intended
///     to keep UI/UX consistent and storage/index sizes reasonable.
///     </para>
///     <para>
///     <b>Description-field convention:</b> Description fields use one of three tiers, chosen per use case to
///     avoid wasting in-row storage on fields that are typically short:
///     </para>
///     <list type="bullet">
///         <item>
///             <term>Compact (200)</term>
///             <description>
///             Administrative tags or system-log entries. Used by <see cref="RoleDescriptionMaxLength"/> and
///             <see cref="SeedHistoryDescriptionMaxLength"/>.
///             </description>
///         </item>
///         <item>
///             <term>Brief (500)</term>
///             <description>
///             User-facing summaries, typically a sentence or two. Used by
///             <see cref="ConversationDescriptionMaxLength"/> and <see cref="ModelEndpointDescriptionMaxLength"/>.
///             </description>
///         </item>
///         <item>
///             <term>Narrative (2000)</term>
///             <description>
///             Free-form content with real text volume (biographies, character descriptions). Used by
///             <see cref="PersonaDescriptionMaxLength"/>. Still well within the SQL Server 8060-byte in-row page limit.
///             </description>
///         </item>
///     </list>
/// </remarks>
public static class EntityLimits
{
	/// <summary>
	/// Maximum allowed size in bytes for stored avatar images after resizing (1 MB).
	/// This is the server-side safety net — the client resizes before upload.
	/// </summary>
	public const int AvatarMaxSizeBytes = 1_048_576;

	/// <summary>
	/// Maximum allowed size in bytes for raw avatar uploads before browser resizing (10 MB).
	/// The client accepts files up to this size, then resizes them via the browser Canvas API
	/// before uploading. The server enforces <see cref="AvatarMaxSizeBytes"/> as the final limit.
	/// </summary>
	public const int AvatarUploadMaxSizeBytes = 10_485_760;

	/// <summary>
	/// Maximum length for conversation descriptions.
	/// </summary>
	/// <remarks>
	/// Brief tier — see the description-field convention on <see cref="EntityLimits"/>.
	/// </remarks>
	public const int ConversationDescriptionMaxLength = 500;

	/// <summary>
	/// Maximum length for conversation titles.
	/// </summary>
	public const int ConversationTitleMaxLength = 200;

	/// <summary>
	/// Maximum length for email addresses.
	/// </summary>
	/// <remarks>
	/// Aligned with the practical maximum derived from RFC 5321 §4.5.3.1.3 (the <c>MAIL FROM</c> path is limited
	/// to 256 octets including the surrounding angle brackets, leaving 254 for the address itself). This value is
	/// also the de-facto interoperability limit used across mail servers and validation libraries.
	/// </remarks>
	public const int EmailMaxLength = 254;

	/// <summary>
	/// Maximum length for model endpoint base URLs.
	/// </summary>
	public const int ModelEndpointBaseUrlMaxLength = 500;

	/// <summary>
	/// Maximum length for model endpoint descriptions.
	/// </summary>
	/// <remarks>
	/// Brief tier — see the description-field convention on <see cref="EntityLimits"/>.
	/// </remarks>
	public const int ModelEndpointDescriptionMaxLength = 500;

	/// <summary>
	/// Maximum length for encrypted credentials stored on model endpoints.
	/// Sized to accommodate encrypted+encoded credential payloads (e.g. API keys, tokens).
	/// </summary>
	public const int ModelEndpointEncryptedCredentialsMaxLength = 4000;

	/// <summary>
	/// Maximum length for model endpoint names.
	/// </summary>
	public const int ModelEndpointNameMaxLength = 100;

	/// <summary>
	/// Maximum length for model endpoint provider type identifiers.
	/// </summary>
	public const int ModelEndpointProviderTypeMaxLength = 50;

	/// <summary>
	/// Maximum length for model identifiers.
	/// </summary>
	/// <remarks>
	/// Examples: <c>mistral:7b</c>, <c>llama3.1:8b-instruct-q4_0</c>, <c>gpt-4-turbo</c>.
	/// Shared between <c>PersonaEntity.DefaultModel</c> and <c>MessageGenerationMetadataEntity.Model</c>.
	/// </remarks>
	public const int ModelIdentifierMaxLength = 100;

	/// <summary>
	/// Maximum length for participant display names.
	/// </summary>
	public const int ParticipantDisplayNameMaxLength = 100;

	/// <summary>
	/// Maximum length for password hashes.
	/// </summary>
	public const int PasswordHashMaxLength = 255;

	/// <summary>
	/// Maximum length for persona descriptions.
	/// </summary>
	/// <remarks>
	/// Narrative tier — see the description-field convention on <see cref="EntityLimits"/>.
	/// Sized for character biographies that may run several paragraphs.
	/// </remarks>
	public const int PersonaDescriptionMaxLength = 2000;

	/// <summary>
	/// Maximum length for resource content type strings (e.g. <c>image/png</c>, <c>application/pdf</c>).
	/// </summary>
	/// <remarks>
	/// Covers all registered IANA media types. Stored on <c>ResourceReferenceEntity</c> because
	/// different references to the same physical file may declare different content types.
	/// </remarks>
	public const int ResourceContentTypeMaxLength = 255;

	/// <summary>
	/// Maximum number of file attachments that can be associated with a single message.
	/// </summary>
	public const int ResourceMaxAttachmentsPerMessage = 10;

	/// <summary>
	/// Maximum allowed size in bytes for a single resource upload (25 MB).
	/// </summary>
	/// <remarks>
	/// This is the server-side safety net for general-purpose file uploads (images, documents, etc.).
	/// The client should display the limit in the UI to prevent unnecessary upload attempts.
	/// </remarks>
	public const int ResourceMaxUploadSizeBytes = 26_214_400;

	/// <summary>
	/// Maximum length for the original file name provided by the uploader.
	/// </summary>
	/// <remarks>
	/// Stored on <c>ResourceReferenceEntity</c>, not on the resource itself, because the same
	/// physical file can be referenced under different names by different owners.
	/// </remarks>
	public const int ResourceOriginalFileNameMaxLength = 255;

	/// <summary>
	/// Maximum length for the storage path of a resource file relative to the storage root.
	/// </summary>
	/// <remarks>
	/// The path is a GUID-based filename (36 characters) with an optional directory prefix
	/// for filesystem sharding (e.g. <c>a1/a1b2c3d4-e5f6-7890-abcd-ef1234567890</c>).
	/// </remarks>
	public const int ResourceStoragePathMaxLength = 100;

	/// <summary>
	/// Maximum length for the JWT ID (<c>jti</c> claim) stored as the primary key of a revoked JWT entry.
	/// </summary>
	/// <remarks>
	/// Sized for a canonical GUID string representation (36 characters); JWT issuers are free to use
	/// other formats as long as they fit within this bound.
	/// </remarks>
	public const int RevokedJwtJtiMaxLength = 36;

	/// <summary>
	/// Maximum length for the human-readable reason recorded when a JWT is revoked
	/// (e.g. <c>"Logout"</c>, <c>"Admin revocation"</c>).
	/// </summary>
	public const int RevokedJwtReasonMaxLength = 100;

	/// <summary>
	/// Maximum length for the JWT subject (<c>sub</c> claim) recorded with a revocation entry.
	/// </summary>
	/// <remarks>
	/// The subject identifies the principal the token was issued for. We size it independently of
	/// <see cref="UsernameMaxLength"/> because the <c>sub</c> claim is not contractually a username
	/// — it may also carry a stringified user ID or another stable identifier — and we don't want
	/// future username changes to silently widen the revocation column. The chosen value matches
	/// <see cref="UsernameMaxLength"/> today, which keeps the existing schema unchanged.
	/// </remarks>
	public const int RevokedJwtSubjectMaxLength = 50;

	/// <summary>
	/// Maximum length for role descriptions.
	/// </summary>
	/// <remarks>
	/// Compact tier — see the description-field convention on <see cref="EntityLimits"/>.
	/// </remarks>
	public const int RoleDescriptionMaxLength = 200;

	/// <summary>
	/// Maximum length for role names.
	/// </summary>
	public const int RoleNameMaxLength = 50;

	/// <summary>
	/// Maximum length for seed history descriptions.
	/// </summary>
	/// <remarks>
	/// Compact tier — see the description-field convention on <see cref="EntityLimits"/>.
	/// </remarks>
	public const int SeedHistoryDescriptionMaxLength = 200;

	/// <summary>
	/// Maximum length for seed operation identifiers.
	/// </summary>
	public const int SeedIdMaxLength = 100;

	/// <summary>
	/// Maximum length for SHA-256 hashes stored as hex strings.
	/// SHA-256 is 32 bytes; as a hex string this is 64 characters.
	/// </summary>
	public const int Sha256HexLength = 64;

	/// <summary>
	/// Maximum length for usernames.
	/// </summary>
	public const int UsernameMaxLength = 50;

	/// <summary>
	/// Maximum length for the serialized user preferences JSON blob.
	/// </summary>
	/// <remarks>
	/// Sized generously to accommodate future preference growth (recent emojis, layout settings,
	/// theme preferences, keybindings, etc.) without requiring a migration. Intentionally capped at
	/// 4000 characters so that on SQL Server the column maps to <c>nvarchar(4000)</c> (= 8000 bytes,
	/// just under the 8060-byte in-row page limit) instead of <c>nvarchar(max)</c>. This keeps the
	/// blob in-row, avoiding LOB off-page allocation and the extra I/O it would cost on every read.
	/// Per-entity values such as endpoint configurations are stored separately and are not subject
	/// to this cap.
	/// </remarks>
	public const int UserPreferencesJsonMaxLength = 4000;
}
