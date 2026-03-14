// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// NOTE: This file is part of a partial class (AesGcmSecretProtectorTests).
// See AesGcmSecretProtectorTests.cs for class-level documentation.
//
// ORGANIZATION: This file uses feature-based organization rather than strict method-based organization.
// Domain separation is a cross-cutting feature that involves both Protect() and Unprotect() working
// together to verify cryptographic isolation between different HKDF domains.
//
// TESTS VERIFY:
// - Different domains produce different derived keys (different fingerprints)
// - Same domain + same key material → compatible encryption/decryption
// - Different domain + same key material → incompatible (prevents cross-domain attacks)
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Security.Cryptography;

using LumaCore.Data.Security;

using Xunit;

namespace LumaCore.Data.Tests.Security;

public sealed partial class AesGcmSecretProtectorTests
{
	/// <summary>
	/// Verifies that the same domain with same key allows encryption and decryption.
	/// </summary>
	[Fact]
	public void DomainSeparation_SameDomain_AllowsDecryption()
	{
		// Arrange
		using AesGcmSecretProtector protector1 = CreateProtector(TestEncryptionKey, TestDomain);
		using AesGcmSecretProtector protector2 = CreateProtector(TestEncryptionKey, TestDomain);
		const string plaintext = "secret data";

		string protectedValue = protector1.Protect(plaintext);

		// Act
		string decrypted = protector2.Unprotect(protectedValue);

		// Assert
		Assert.Equal(plaintext, decrypted);
	}

	/// <summary>
	/// Verifies that different domains produce different fingerprints even with the same key material.
	/// </summary>
	[Fact]
	public void DomainSeparation_DifferentDomains_ProducesDifferentFingerprints()
	{
		// Arrange
		using AesGcmSecretProtector protector1 = CreateProtector(TestEncryptionKey, TestDomain);
		using AesGcmSecretProtector protector2 = CreateProtector(TestEncryptionKey, AlternativeDomain);

		// Act
		string protected1 = protector1.Protect("test");
		string protected2 = protector2.Protect("test");

		// Assert - different domains should produce different fingerprints
		string fingerprint1 = protected1.Split(':')[1];
		string fingerprint2 = protected2.Split(':')[1];
		Assert.NotEqual(fingerprint1, fingerprint2);
	}

	/// <summary>
	/// Verifies that <see cref="SecretProtectorDomains"/> constants are distinct.
	/// </summary>
	[Fact]
	public void DomainSeparation_SecretProtectorDomainsConstants_AreDistinct()
	{
		// Arrange
		string[] domains =
		[
			SecretProtectorDomains.Default,
			SecretProtectorDomains.ModelEndpointCredentials,
			SecretProtectorDomains.UserApiTokens
		];

		// Act + Assert
		Assert.Equal(domains.Length, domains.Distinct().Count());
	}

	/// <summary>
	/// Verifies that different domains produce cryptographically isolated keys,
	/// preventing cross-domain decryption.
	/// </summary>
	[Fact]
	public void DomainSeparation_DifferentDomains_PreventsDecryption()
	{
		// Arrange
		using AesGcmSecretProtector protector1 = CreateProtector(TestEncryptionKey, TestDomain);
		using AesGcmSecretProtector protector2 = CreateProtector(TestEncryptionKey, AlternativeDomain);
		const string plaintext = "secret data";

		string protectedValue = protector1.Protect(plaintext);

		// Act + Assert - same key, different domain should fail
		Assert.ThrowsAny<CryptographicException>(() => protector2.Unprotect(protectedValue));
	}
}
