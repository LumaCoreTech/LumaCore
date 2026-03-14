// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Cryptography;

using LumaCore.Data.Security;

using Xunit;

// Unused parameters in theory data are used for test output identification,
// so we intentionally ignore the "unused parameter" warning here.
#pragma warning disable xUnit1026

namespace LumaCore.Data.Tests.Security;

public sealed partial class AesGcmSecretProtectorTests
{
	/// <summary>
	/// Test data for <see cref="Unprotect_Roundtrip_ReturnsOriginalPlaintext"/>.
	/// </summary>
	public static TheoryData<string, string> UnprotectRoundtripTestData => new()
	{
		// scenario, plaintext
		{ "Empty string", "" },
		{ "Unicode text with emojis", "日本語テキスト 🎉 émojis" },
		{ "Large text (100KB)", new string('A', 100_000) },
		{ "Simple ASCII", "Hello, World!" }
	};

	/// <summary>
	/// Test data for <see cref="Unprotect_WithInvalidFormat_ThrowsFormatException"/>.
	/// Each row represents a different invalid format scenario.
	/// </summary>
	public static TheoryData<string, string, string> UnprotectFormatErrorTestData => new()
	{
		// scenario, invalidInput, expectedMessagePart

		// Invalid version prefix
		{ "Wrong version prefix", "v2:abcdef1234567890:AAAA", "Unsupported protected value format" },

		// Missing/invalid separator structure
		{ "No separator after fingerprint", "v1:noseparatorhere", "Invalid protected value payload" },
		{ "Empty fingerprint", "v1::AAAA", "Invalid protected value payload" },

		// Invalid fingerprint length
		{ "Fingerprint too short", "v1:abcd:AAAA", "Invalid fingerprint length" },
		{ "Fingerprint too long", "v1:abcdef1234567890extra:AAAA", "Invalid fingerprint length" },

		// Invalid fingerprint format
		{ "Non-hex fingerprint", "v1:ghijklmnopqrstuv:AAAA", "Invalid fingerprint format" },

		// Invalid Base64 payload
		{ "Invalid Base64", "v1:abcdef1234567890:!!!invalid!!!", "Invalid base64 payload" },

		// Truncated payload (less than nonce + tag size)
		{
			"Truncated payload", $"v1:abcdef1234567890:{Convert.ToBase64String(new byte[10])}",
			"Invalid protected value payload"
		}
	};

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Unprotect"/> correctly decrypts
	/// a protected value and returns the original plaintext.
	/// </summary>
	[Fact]
	public void Unprotect_WithValidProtectedValue_ReturnsOriginalPlaintext()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		const string original = "Hello, World!";
		string protectedValue = protector.Protect(original);

		// Act
		string decrypted = protector.Unprotect(protectedValue);

		// Assert
		Assert.Equal(original, decrypted);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Unprotect"/> handles various plaintext inputs correctly
	/// in a roundtrip (protect then unprotect).
	/// </summary>
	/// <param name="scenario">Description of the test scenario.</param>
	/// <param name="plaintext">The plaintext to protect and unprotect.</param>
	[Theory]
	[MemberData(nameof(UnprotectRoundtripTestData))]
	public void Unprotect_Roundtrip_ReturnsOriginalPlaintext(string scenario, string plaintext)
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		string protectedValue = protector.Protect(plaintext);

		// Act
		string decrypted = protector.Unprotect(protectedValue);

		// Assert
		Assert.Equal(plaintext, decrypted);
	}

	/// <summary>
	/// Verifies that different protector instances with the same key can decrypt each other's data.
	/// </summary>
	[Fact]
	public void Unprotect_WithDifferentInstanceSameKey_ReturnsOriginalPlaintext()
	{
		// Arrange
		using AesGcmSecretProtector protector1 = CreateProtector(TestEncryptionKey);
		using AesGcmSecretProtector protector2 = CreateProtector(TestEncryptionKey);
		const string original = "shared secret";
		string protectedValue = protector1.Protect(original);

		// Act
		string decrypted = protector2.Unprotect(protectedValue);

		// Assert
		Assert.Equal(original, decrypted);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Unprotect"/> throws
	/// <see cref="ArgumentNullException"/> when protected value is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Unprotect_WithNullProtectedValue_ThrowsArgumentNullException()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => protector.Unprotect(null!));
		Assert.Equal("protectedValue", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Unprotect"/> throws
	/// <see cref="ObjectDisposedException"/> when called after disposal.
	/// </summary>
	[Fact]
	public void Unprotect_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		string protectedValue = protector.Protect("test");
		protector.Dispose();

		// Act + Assert
		Assert.Throws<ObjectDisposedException>(() => protector.Unprotect(protectedValue));
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Unprotect"/> throws
	/// <see cref="FormatException"/> for various invalid input formats.
	/// </summary>
	/// <param name="scenario">Description of the invalid input scenario.</param>
	/// <param name="invalidInput">The invalid protected value to test.</param>
	/// <param name="expectedMessagePart">Expected substring in the exception message.</param>
	[Theory]
	[MemberData(nameof(UnprotectFormatErrorTestData))]
	public void Unprotect_WithInvalidFormat_ThrowsFormatException(
		string scenario,
		string invalidInput,
		string expectedMessagePart)
	{
		_ = scenario; // Used for test output identification

		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => protector.Unprotect(invalidInput));
		Assert.Contains(expectedMessagePart, ex.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Unprotect"/> throws
	/// <see cref="CryptographicException"/> when the ciphertext has been tampered with.
	/// </summary>
	[Fact]
	public void Unprotect_WithTamperedCiphertext_ThrowsCryptographicException()
	{
		// Arrange
		using AesGcmSecretProtector protector = CreateProtector(TestEncryptionKey);
		string protectedValue = protector.Protect("test");
		string[] parts = protectedValue.Split(':');
		char[] payload = parts[2].ToCharArray();
		payload[^5] = payload[^5] == 'A' ? 'B' : 'A';
		string tamperedValue = $"{parts[0]}:{parts[1]}:{new string(payload)}";

		// Act + Assert
		// ThrowsAny because .NET may throw AuthenticationTagMismatchException (subclass of CryptographicException)
		Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(tamperedValue));
	}

	/// <summary>
	/// Verifies that <see cref="AesGcmSecretProtector.Unprotect"/> throws
	/// <see cref="CryptographicException"/> when using a completely wrong key.
	/// </summary>
	[Fact]
	public void Unprotect_WithWrongKey_ThrowsCryptographicException()
	{
		// Arrange
		using AesGcmSecretProtector protector1 = CreateProtector(TestEncryptionKey);
		using AesGcmSecretProtector protector2 = CreateProtector(AlternativeKey);
		string protectedValue = protector1.Protect("secret");

		// Act + Assert
		// ThrowsAny because .NET may throw AuthenticationTagMismatchException (subclass of CryptographicException)
		Assert.ThrowsAny<CryptographicException>(() => protector2.Unprotect(protectedValue));
	}
}
