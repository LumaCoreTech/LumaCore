// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Security;

using Microsoft.Extensions.Options;

namespace LumaCore.Data.Tests.Security;

public sealed partial class AesGcmSecretProtectorTests
{
	/// <summary>
	/// Test encryption key with sufficient length for AES-256 key derivation.
	/// </summary>
	private const string TestEncryptionKey = "ThisIsATestEncryptionKeyWith32+CharsForSecurity!";

	/// <summary>
	/// Alternative encryption key used for key rotation and mismatch testing.
	/// </summary>
	private const string AlternativeKey = "AnotherEncryptionKeyForTestingKeyRotationScenarios";

	/// <summary>
	/// Third encryption key used for testing multiple fallback key scenarios.
	/// </summary>
	private const string ThirdKey = "YetAnotherKeyForTestingMultipleFallbackKeysInList!";

	/// <summary>
	/// Test domain for domain isolation testing.
	/// </summary>
	private const string TestDomain = "Test:Domain:v1";

	/// <summary>
	/// Alternative test domain for cross-domain isolation testing.
	/// </summary>
	private const string AlternativeDomain = "Test:AlternativeDomain:v1";

	/// <summary>
	/// Creates a new <see cref="AesGcmSecretProtector"/> instance with the specified encryption key
	/// and optional fallback keys, using the default domain.
	/// </summary>
	/// <param name="encryptionKey">The primary encryption key.</param>
	/// <param name="previousKeys">Optional collection of previous encryption keys for key rotation support.</param>
	/// <returns>A configured <see cref="AesGcmSecretProtector"/> instance.</returns>
	private static AesGcmSecretProtector CreateProtector(string encryptionKey, IEnumerable<string>? previousKeys = null)
	{
		var options = new DatabaseOptions
		{
			EncryptionKey = encryptionKey,
			PreviousEncryptionKeys = previousKeys?.ToList() ?? []
		};

		return new AesGcmSecretProtector(Options.Create(options));
	}

	/// <summary>
	/// Creates a new <see cref="AesGcmSecretProtector"/> instance with the specified encryption key,
	/// domain, and optional fallback keys.
	/// </summary>
	/// <param name="encryptionKey">The primary encryption key.</param>
	/// <param name="domain">The HKDF domain for key derivation.</param>
	/// <param name="previousKeys">Optional collection of previous encryption keys for key rotation support.</param>
	/// <returns>A configured <see cref="AesGcmSecretProtector"/> instance.</returns>
	private static AesGcmSecretProtector CreateProtector(
		string               encryptionKey,
		string               domain,
		IEnumerable<string>? previousKeys = null)
	{
		var options = new DatabaseOptions
		{
			EncryptionKey = encryptionKey,
			PreviousEncryptionKeys = previousKeys?.ToList() ?? []
		};

		return new AesGcmSecretProtector(Options.Create(options), domain);
	}
}
