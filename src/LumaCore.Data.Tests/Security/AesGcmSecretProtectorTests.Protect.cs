// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Security;

using Xunit;

namespace LumaCore.Data.Tests.Security;

public sealed partial class AesGcmSecretProtectorTests
{
	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Protect"/> returns a protected value
	/// with the correct version prefix for valid plaintext input.
	/// </summary>
	[Fact]
	public void Protect_WithValidPlaintext_ReturnsProtectedValueWithVersionPrefix()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		const string plaintext = "Hello, World!";

		// Act
		string protectedValue = protector.Protect(plaintext);

		// Assert
		Assert.NotNull(protectedValue);
		Assert.StartsWith("v1:", protectedValue, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Protect"/> produces output in the correct
	/// format: <c>v1:&lt;fingerprint&gt;:&lt;base64&gt;</c>.
	/// </summary>
	[Fact]
	public void Protect_WithValidInput_ProducesCorrectOutputFormat()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		const string plaintext = "test";

		// Act
		string protectedValue = protector.Protect(plaintext);

		// Assert
		string[] parts = protectedValue.Split(':');
		Assert.Equal(3, parts.Length);
		Assert.Equal("v1", parts[0]);
		Assert.Equal(16, parts[1].Length); // 8 bytes = 16 hex chars
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Protect"/> is non-deterministic,
	/// producing different outputs for the same plaintext due to random nonce generation.
	/// </summary>
	[Fact]
	public void Protect_WithSamePlaintext_ProducesDifferentOutputs()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		const string plaintext = "Same plaintext";

		// Act
		string protected1 = protector.Protect(plaintext);
		string protected2 = protector.Protect(plaintext);

		// Assert
		Assert.NotEqual(protected1, protected2);
	}

	/// <summary>
	/// Verifies that different protector instances with the same key produce
	/// the same fingerprint, demonstrating deterministic key derivation.
	/// </summary>
	[Fact]
	public void Protect_WithSameKey_ProducesSameFingerprint()
	{
		// Arrange
		using AesGcmSecretProtector protector1 = CreateProtector(TestEncryptionKey);
		using AesGcmSecretProtector protector2 = CreateProtector(TestEncryptionKey);

		// Act
		string protected1 = protector1.Protect("test");
		string protected2 = protector2.Protect("test");

		// Assert
		string fingerprint1 = protected1.Split(':')[1];
		string fingerprint2 = protected2.Split(':')[1];
		Assert.Equal(fingerprint1, fingerprint2);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Protect"/> handles an empty string correctly.
	/// </summary>
	[Fact]
	public void Protect_WithEmptyString_ReturnsProtectedValue()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);

		// Act
		string protectedValue = protector.Protect("");

		// Assert
		Assert.NotNull(protectedValue);
		Assert.StartsWith("v1:", protectedValue, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Protect"/> handles Unicode text with emojis correctly.
	/// </summary>
	[Fact]
	public void Protect_WithUnicodeText_ReturnsProtectedValue()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		const string plaintext = "日本語テキスト 🎉 émojis";

		// Act
		string protectedValue = protector.Protect(plaintext);

		// Assert
		Assert.NotNull(protectedValue);
		Assert.StartsWith("v1:", protectedValue, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Protect"/> handles large text (100KB) correctly.
	/// </summary>
	[Fact]
	public void Protect_WithLargeText_Succeeds()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		string largeText = new('A', 100_000);

		// Act
		string protectedValue = protector.Protect(largeText);

		// Assert
		Assert.NotNull(protectedValue);
		Assert.StartsWith("v1:", protectedValue, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Protect"/> throws
	/// <see cref="ArgumentNullException"/> when plaintext is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Protect_WithNullPlaintext_ThrowsArgumentNullException()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => protector.Protect(null!));
		Assert.Equal("plaintext", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Protect"/> throws
	/// <see cref="ObjectDisposedException"/> when called after disposal.
	/// </summary>
	[Fact]
	public void Protect_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		protector.Dispose();

		// Act + Assert
		Assert.Throws<ObjectDisposedException>(() => protector.Protect("test"));
	}
}
