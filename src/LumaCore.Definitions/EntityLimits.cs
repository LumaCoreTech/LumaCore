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
/// </remarks>
public static class EntityLimits
{
	/// <summary>
	/// Maximum length for avatar URLs.
	/// </summary>
	public const int AvatarUrlMaxLength = 500;

	/// <summary>
	/// Maximum length for conversation titles.
	/// </summary>
	public const int ConversationTitleMaxLength = 200;

	/// <summary>
	/// Maximum length for participant display names.
	/// </summary>
	public const int DisplayNameMaxLength = 100;

	/// <summary>
	/// Maximum length for email addresses.
	/// Chosen as a pragmatic maximum aligned with common RFC guidance and widely used across systems.
	/// </summary>
	public const int EmailMaxLength = 254;

	/// <summary>
	/// Maximum length for model identifiers.
	/// </summary>
	public const int ModelIdentifierMaxLength = 100;

	/// <summary>
	/// Maximum length for model endpoint base URLs.
	/// </summary>
	public const int ModelEndpointBaseUrlMaxLength = 500;

	/// <summary>
	/// Maximum length for model endpoint descriptions.
	/// </summary>
	public const int ModelEndpointDescriptionMaxLength = 1000;

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
	/// Maximum length for password hashes.
	/// </summary>
	public const int PasswordHashMaxLength = 255;

	/// <summary>
	/// Maximum length for persona descriptions.
	/// </summary>
	public const int PersonaDescriptionMaxLength = 1000;

	/// <summary>
	/// Maximum length for role descriptions.
	/// </summary>
	public const int RoleDescriptionMaxLength = 500;

	/// <summary>
	/// Maximum length for role names.
	/// </summary>
	public const int RoleNameMaxLength = 50;

	/// <summary>
	/// Maximum length for SHA-256 hashes stored as hex strings.
	/// SHA-256 is 32 bytes; as a hex string this is 64 characters.
	/// </summary>
	public const int Sha256HexLength = 64;

	/// <summary>
	/// Maximum length for seed history descriptions.
	/// </summary>
	public const int SeedHistoryDescriptionMaxLength = 500;

	/// <summary>
	/// Maximum length for seed operation identifiers.
	/// </summary>
	public const int SeedIdMaxLength = 100;

	/// <summary>
	/// Maximum length for usernames.
	/// </summary>
	public const int UsernameMaxLength = 50;

	/// <summary>
	/// Maximum length for the serialized user preferences JSON blob.
	/// </summary>
	/// <remarks>
	/// Sized generously to accommodate future preference growth (recent emojis, layout settings,
	/// theme preferences, keybindings, etc.) without requiring a migration.
	/// </remarks>
	public const int UserPreferencesJsonMaxLength = 8000;
}
