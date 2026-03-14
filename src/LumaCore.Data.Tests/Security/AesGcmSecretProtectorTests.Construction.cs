// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Security;

using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Data.Tests.Security;

public sealed partial class AesGcmSecretProtectorTests
{
	/// <summary>
	/// Verifies that the constructor creates a valid instance when provided with valid options.
	/// </summary>
	[Fact]
	public void Constructor_WithValidOptions_CreatesInstance()
	{
		// Arrange
		// (no arrangement needed - using helper method)

		// Act
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);

		// Assert
		Assert.NotNull(protector);
	}

	/// <summary>
	/// Verifies that the constructor creates a valid instance when fallback keys are provided.
	/// </summary>
	[Fact]
	public void Constructor_WithFallbackKeys_CreatesInstance()
	{
		// Arrange
		string[] fallbackKeys = [AlternativeKey, ThirdKey];

		// Act
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey, fallbackKeys);

		// Assert
		Assert.NotNull(protector);
	}

	/// <summary>
	/// Verifies that the constructor with explicit domain creates a valid instance.
	/// </summary>
	[Fact]
	public void Constructor_WithExplicitDomain_CreatesInstance()
	{
		// Arrange + Act
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey, TestDomain);

		// Assert
		Assert.NotNull(protector);
	}

	/// <summary>
	/// Verifies that the default constructor uses <see cref="SecretProtectorDomains.Default"/> domain
	/// and produces output compatible with an explicit default domain protector.
	/// </summary>
	[Fact]
	public void Constructor_WithDefaultDomain_IsBackwardCompatible()
	{
		// Arrange
		using AesGcmSecretProtector defaultProtector = CreateProtector(TestEncryptionKey);
		using AesGcmSecretProtector explicitDefaultProtector = CreateProtector(
			TestEncryptionKey,
			SecretProtectorDomains.Default);
		const string plaintext = "test secret";

		// Act
		string protectedByDefault = defaultProtector.Protect(plaintext);
		string decryptedByExplicit = explicitDefaultProtector.Unprotect(protectedByDefault);

		// Assert - both should produce compatible results
		Assert.Equal(plaintext, decryptedByExplicit);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when options is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new AesGcmSecretProtector(null!));
		Assert.Equal("options", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentException"/> when the encryption key is empty.
	/// </summary>
	[Fact]
	public void Constructor_WithEmptyEncryptionKey_ThrowsArgumentException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => CreateProtector(""));
		Assert.Equal("keyMaterial", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentException"/> when the encryption key is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithNullEncryptionKey_ThrowsArgumentException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => CreateProtector(null!));
		Assert.Equal("keyMaterial", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentException"/> when domain is empty.
	/// </summary>
	[Fact]
	public void Constructor_WithEmptyDomain_ThrowsArgumentException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => CreateProtector(TestEncryptionKey, ""));
		Assert.Equal("domain", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentException"/> when domain is whitespace.
	/// </summary>
	[Fact]
	public void Constructor_WithWhitespaceDomain_ThrowsArgumentException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => CreateProtector(TestEncryptionKey, "   "));
		Assert.Equal("domain", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when domain is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithNullDomain_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new DatabaseOptions { EncryptionKey = TestEncryptionKey };
		string? nullDomain = null;

		// Act + Assert
		var ex = Assert.ThrowsAny<ArgumentException>(() => new AesGcmSecretProtector(
			Options.Create(options),
			nullDomain!));
		Assert.Equal("domain", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor filters out empty and whitespace-only fallback keys
	/// while retaining valid fallback keys for decryption.
	/// </summary>
	[Fact]
	public void Constructor_WithEmptyFallbackKeys_IgnoresEmptyKeys()
	{
		// Arrange - encrypt with a key that will become a fallback
		using AesGcmSecretProtector oldProtector = CreateProtector(AlternativeKey);
		const string plaintext = "test secret";
		string protectedValue = oldProtector.Protect(plaintext);

		// Act - create protector with mixed valid/invalid fallback keys
		string[] fallbackKeys = ["", "   ", null!, AlternativeKey];
		using AesGcmSecretProtector newProtector = CreateProtector(TestEncryptionKey, fallbackKeys);

		// Assert - exactly 1 fallback key should have been retained
		Assert.Equal(1, newProtector.FallbackKeyCount);

		// Assert - valid fallback key should still work
		string decrypted = newProtector.Unprotect(protectedValue);
		Assert.Equal(plaintext, decrypted);
	}

	/// <summary>
	/// Verifies that the constructor successfully ignores fallback keys that are entirely invalid
	/// (all empty, whitespace, or <see langword="null"/>), resulting in a protector with no fallback keys.
	/// </summary>
	[Fact]
	public void Constructor_WithOnlyInvalidFallbackKeys_IgnoresAllKeys()
	{
		// Arrange + Act - create protector with ONLY invalid fallback keys
		string[] onlyInvalidKeys = ["", "   ", null!, "  \t  "];
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey, onlyInvalidKeys);

		// Assert - NO fallback keys should have been added
		Assert.Equal(0, protector.FallbackKeyCount);
	}
}
