// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Security;

/// <summary>
/// Defines well-known domain identifiers for secret protection.
/// </summary>
/// <remarks>
///     <para>
///     Each domain produces a cryptographically isolated key via HKDF domain separation.
///     Secrets protected with one domain cannot be decrypted with a protector using a different domain.
///     </para>
///     <para>
///     These identifiers are used as service keys for <see cref="ISecretProtector"/> Keyed Services registration.
///     </para>
///     <para>
///     <b>Security:</b> Changing a domain value after secrets have been protected with it will make
///     those secrets unrecoverable. Domain values should be treated as immutable.
///     </para>
/// </remarks>
public static class SecretProtectorDomains
{
	/// <summary>
	/// Domain for protecting model endpoint credentials (API keys, connection strings).
	/// </summary>
	/// <remarks>
	/// Used by <see cref="Entities.ModelEndpointEntity.EncryptedCredentials"/>.
	/// </remarks>
	public const string ModelEndpointCredentials = "LumaCore:Data:ModelEndpointCredentials:v1";

	/// <summary>
	/// Domain for protecting user API tokens.
	/// </summary>
	/// <remarks>
	/// Reserved for future use when API token authentication is implemented.
	/// </remarks>
	public const string UserApiTokens = "LumaCore:Data:UserApiTokens:v1";

	/// <summary>
	/// Default domain for backward compatibility.
	/// </summary>
	/// <remarks>
	/// This matches the original hardcoded HKDF info value and ensures existing encrypted data remains decryptable.
	/// New code should prefer domain-specific constants.
	/// </remarks>
	public const string Default = "LumaCore:Data:AesGcm:v1";
}
