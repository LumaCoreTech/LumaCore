// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// NOTE: This file is part of a partial class (AesGcmSecretProtectorTests).
// See AesGcmSecretProtectorTests.cs for class-level documentation.
//
// ORGANIZATION: This file uses feature-based organization rather than strict method-based organization.
// Key rotation is a cross-cutting feature that involves both Protect() (with old/new keys) and
// Unprotect() (with fallback keys) working together to verify backward-compatible decryption
// after key rotation.
//
// TESTS VERIFY:
// - Data encrypted with old keys can be decrypted with a new protector (if old key is in fallback list)
// - Fingerprint optimization works (matching fingerprint tried first)
// - Multiple fallback keys are tried in correct order
// - Missing keys result in appropriate exceptions
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Security.Cryptography;

using LumaCore.Data.Security;

using Xunit;

namespace LumaCore.Data.Tests.Security;

public sealed partial class AesGcmSecretProtectorTests
{
	/// <summary>
	/// Verifies that decryption succeeds when the primary key matches the fingerprint
	/// embedded in the protected value.
	/// </summary>
	[Fact]
	public void KeyRotation_PrimaryKeyMatchingFingerprint_Succeeds()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey, [AlternativeKey]);
		const string original = "secret data";
		string protectedValue = protector.Protect(original);

		// Act
		string decrypted = protector.Unprotect(protectedValue);

		// Assert
		Assert.Equal(original, decrypted);
	}

	/// <summary>
	/// Verifies that decryption succeeds when a fallback key matches the fingerprint
	/// after key rotation (new primary key, old key as fallback).
	/// </summary>
	[Fact]
	public void KeyRotation_FallbackKeyMatchingFingerprint_Succeeds()
	{
		// Arrange - encrypt with old key
		using AesGcmSecretProtector oldProtector = CreateProtector(AlternativeKey);
		const string original = "secret data";
		string protectedValue = oldProtector.Protect(original);

		// Arrange - create new protector with rotated keys
		using AesGcmSecretProtector newProtector = CreateProtector(TestEncryptionKey, [AlternativeKey]);

		// Act
		string decrypted = newProtector.Unprotect(protectedValue);

		// Assert
		Assert.Equal(original, decrypted);
	}

	/// <summary>
	/// Verifies that the correct key is found among multiple fallback keys
	/// based on fingerprint matching.
	/// </summary>
	[Fact]
	public void KeyRotation_MultipleFallbackKeys_FindsCorrectKey()
	{
		// Arrange - encrypt with third key
		using AesGcmSecretProtector oldProtector = CreateProtector(ThirdKey);
		const string original = "secret data";
		string protectedValue = oldProtector.Protect(original);

		// Arrange - create new protector with multiple fallback keys
		using AesGcmSecretProtector newProtector = CreateProtector(TestEncryptionKey, [AlternativeKey, ThirdKey]);

		// Act
		string decrypted = newProtector.Unprotect(protectedValue);

		// Assert
		Assert.Equal(original, decrypted);
	}

	/// <summary>
	/// Verifies that when the fingerprint does not match the primary key,
	/// the protector falls back to trying other keys and eventually succeeds.
	/// </summary>
	[Fact]
	public void KeyRotation_FingerprintDoesNotMatchPrimary_FallsBackToOtherKeys()
	{
		// Arrange - encrypt with first key
		using AesGcmSecretProtector protector1 = CreateProtector(TestEncryptionKey);
		string protectedValue = protector1.Protect("secret");

		// Arrange - create protector with different primary but original as fallback
		using AesGcmSecretProtector protector2 = CreateProtector(AlternativeKey, [TestEncryptionKey]);

		// Act
		string decrypted = protector2.Unprotect(protectedValue);

		// Assert
		Assert.Equal("secret", decrypted);
	}

	/// <summary>
	/// Verifies that when the primary key matches the fingerprint but decryption fails
	/// (e.g., due to corrupted data), the protector correctly tries fallback keys.
	/// This test ensures the tuple deconstruction in the fallback key enumeration is covered.
	/// </summary>
	[Fact]
	public void KeyRotation_PrimaryKeyMatchesFingerprintButCorruptedData_TriesFallbackKeys()
	{
		// Arrange - encrypt with primary key and include a fallback
		using AesGcmSecretProtector protector1 = CreateProtector(TestEncryptionKey, [AlternativeKey]);
		const string original = "secret data";
		string protectedValue = protector1.Protect(original);

		// Corrupt the protected value slightly (change one character in the base64 payload)
		// The fingerprint stays the same (matches primary), but the ciphertext/tag is invalid.
		string[] parts = protectedValue.Split(':');
		string corruptedBase64 = parts[2].Length > 10
			                         ? parts[2][..^10] + "XXXXXXXXXX" // Replace last 10 chars
			                         : parts[2];
		string corruptedValue = $"{parts[0]}:{parts[1]}:{corruptedBase64}";

		// Act + Assert - should fail because neither primary nor fallback can decrypt corrupted data
		Assert.ThrowsAny<CryptographicException>(() => protector1.Unprotect(corruptedValue));
	}

	/// <summary>
	/// Verifies that decryption throws <see cref="CryptographicException"/>
	/// when no configured key can decrypt the value (missing key scenario).
	/// </summary>
	[Fact]
	public void KeyRotation_NoMatchingKey_ThrowsCryptographicException()
	{
		// Arrange - encrypt with first key
		using AesGcmSecretProtector protector1 = CreateProtector(TestEncryptionKey);
		string protectedValue = protector1.Protect("secret");

		// Arrange - create protector with completely different keys
		using AesGcmSecretProtector protector2 = CreateProtector(AlternativeKey, [ThirdKey]);

		// Act + Assert
		Assert.ThrowsAny<CryptographicException>(() => protector2.Unprotect(protectedValue));
	}
}
