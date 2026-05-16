// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;

using LumaCore.Core.Cryptography;

using Xunit;

namespace LumaCore.Core.Tests.Cryptography;

public partial class Pbkdf2PasswordHasherTests
{
	// --- 1. Valid scenarios ---

	/// <summary>
	/// Verifies that <c>Hash</c> produces a string conforming to the documented
	/// <c>$pbkdf2-sha512$&lt;iterations&gt;$&lt;base64&gt;</c> shape.
	/// </summary>
	[Fact]
	public void Hash_WithValidPassword_ProducesExpectedFormat()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();

		// Act
		string hash = sut.Hash(TestPassword.AsSpan());

		// Assert
		string[] parts = hash.Split('$');
		// Leading '$' produces an empty first segment, so a well-formed hash has exactly 4 parts.
		Assert.Equal(4, parts.Length);
		Assert.Equal(string.Empty, parts[0]);
		Assert.Equal("pbkdf2-sha512", parts[1]);
		Assert.Equal(TestIterations.ToString(CultureInfo.InvariantCulture), parts[2]);
		Assert.NotEmpty(parts[3]);

		// The base64 payload must decode to exactly salt (16) + key (64) = 80 bytes.
		byte[] decoded = Convert.FromBase64String(parts[3]);
		Assert.Equal(80, decoded.Length);
	}

	/// <summary>
	/// Verifies that two consecutive hashes of the same password produce different output strings.
	/// </summary>
	/// <remarks>
	/// This is the salt-uniqueness guarantee — without a fresh per-hash salt, rainbow tables would defeat
	/// the whole construction. Two identical outputs would indicate a deterministic or static-salt bug.
	/// </remarks>
	[Fact]
	public void Hash_CalledTwice_ProducesDifferentHashes()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();

		// Act
		string hashA = sut.Hash(TestPassword.AsSpan());
		string hashB = sut.Hash(TestPassword.AsSpan());

		// Assert
		Assert.NotEqual(hashA, hashB);
	}

	/// <summary>
	/// Verifies that a single-character password is accepted (the minimum-length boundary).
	/// </summary>
	[Fact]
	public void Hash_WhenPasswordIsSingleCharacter_Succeeds()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();

		// Act
		string hash = sut.Hash("x".AsSpan());

		// Assert
		Assert.StartsWith("$pbkdf2-sha512$", hash, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies that a password at the maximum allowed length (1024 characters) is accepted.
	/// </summary>
	[Fact]
	public void Hash_WhenPasswordAtMaxLength_Succeeds()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string password = new('a', 1024);

		// Act
		string hash = sut.Hash(password.AsSpan());

		// Assert
		Assert.StartsWith("$pbkdf2-sha512$", hash, StringComparison.Ordinal);
	}

	// --- 2. Invalid scenarios ---

	/// <summary>
	/// Verifies that an empty password is rejected — empty inputs are never legitimate credentials and
	/// hashing them would only mask an upstream bug.
	/// </summary>
	[Fact]
	public void Hash_WhenPasswordIsEmpty_ThrowsArgumentException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => sut.Hash(ReadOnlySpan<char>.Empty));
		Assert.Equal("password", ex.ParamName);
		Assert.Equal("Password must not be empty. (Parameter 'password')", ex.Message);
	}

	/// <summary>
	/// Verifies that a password exceeding the documented maximum length (1024 characters) is rejected.
	/// </summary>
	[Fact]
	public void Hash_WhenPasswordExceedsMaxLength_ThrowsArgumentException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string oversized = new('a', 1025);

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => sut.Hash(oversized.AsSpan()));
		Assert.Equal("password", ex.ParamName);
		Assert.Equal(
			"Password length (1025) exceeds the maximum of 1024 characters. (Parameter 'password')",
			ex.Message);
	}
}
