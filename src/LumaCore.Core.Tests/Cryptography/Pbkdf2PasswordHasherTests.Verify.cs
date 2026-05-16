// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Cryptography;

using Xunit;

namespace LumaCore.Core.Tests.Cryptography;

public partial class Pbkdf2PasswordHasherTests
{
	#region Verify(password, hash)

	// --- 1. Valid scenarios ---

	/// <summary>
	/// Verifies the canonical round-trip: a freshly hashed password verifies successfully against its hash.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenPasswordMatchesHash_ReturnsTrue()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string hash = CachedHash.Value;

		// Act
		bool result = sut.Verify(TestPassword.AsSpan(), hash.AsSpan());

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that a different password is rejected against an existing hash.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenPasswordDoesNotMatchHash_ReturnsFalse()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string hash = CachedHash.Value;

		// Act
		bool result = sut.Verify("wrong password".AsSpan(), hash.AsSpan());

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that a hash produced with a different iteration count still verifies correctly.
	/// </summary>
	/// <remarks>
	/// The stored hash carries its own iteration count, so verification must use that value rather than the
	/// hasher's current configuration. Otherwise raising the cost in configuration would invalidate every
	/// existing credential at once.
	/// </remarks>
	[Fact]
	public void Verify_WithoutRehash_WhenHashUsesDifferentIterations_StillVerifiesCorrectly()
	{
		// Arrange
		// Produce a hash with a higher iteration count, then verify with a hasher configured to the lower
		// minimum. The verifier must read iterations from the hash string, not from its own configuration.
		Pbkdf2PasswordHasher oldHasher = CreateHasher(iterations: TestIterations + 50_000);
		string hash = oldHasher.Hash(TestPassword.AsSpan());
		Pbkdf2PasswordHasher newHasher = CreateHasher(iterations: TestIterations);

		// Act
		bool result = newHasher.Verify(TestPassword.AsSpan(), hash.AsSpan());

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that tampering with the base64 payload of a valid hash causes verification to fail.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenHashPayloadIsTampered_ReturnsFalse()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string hash = CachedHash.Value;
		// Flip the first base64 payload character to a guaranteed-different value. The first payload
		// character is always a data character (never base64 padding), so the decoded byte count stays at
		// the expected 80 bytes and the change only affects the derived key portion of the hash.
		int payloadStart = hash.LastIndexOf('$') + 1;
		char firstPayloadChar = hash[payloadStart];
		char replacement = firstPayloadChar == 'A' ? 'B' : 'A';
		string tampered = string.Concat(
			hash.AsSpan(0, payloadStart),
			replacement.ToString(),
			hash.AsSpan(payloadStart + 1));

		// Act
		bool result = sut.Verify(TestPassword.AsSpan(), tampered.AsSpan());

		// Assert
		Assert.False(result);
	}

	// --- 2. Invalid scenarios ---

	/// <summary>
	/// Verifies that an empty password is rejected with a clear argument error rather than a silent false.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenPasswordIsEmpty_ThrowsArgumentException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string hash = CachedHash.Value;

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => sut.Verify(ReadOnlySpan<char>.Empty, hash.AsSpan()));
		Assert.Equal("password", ex.ParamName);
	}

	/// <summary>
	/// Verifies that an empty hash string is rejected as malformed.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenHashIsEmpty_ThrowsArgumentException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => sut.Verify(TestPassword.AsSpan(), ReadOnlySpan<char>.Empty));
		Assert.Equal("passwordHash", ex.ParamName);
	}

	/// <summary>
	/// Verifies that a hash string missing the leading '$' is reported as a format error.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenHashMissingLeadingDollar_ThrowsFormatException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		// Strip the leading '$' from a valid hash to leave the algorithm token bare.
		string malformed = CachedHash.Value[1..];

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => sut.Verify(TestPassword.AsSpan(), malformed.AsSpan()));
		Assert.Equal("Password hash must start with '$'.", ex.Message);
	}

	/// <summary>
	/// Verifies that a hash missing the iteration separator is rejected with a precise diagnostic.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenHashMissingIterationSeparator_ThrowsFormatException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		const string malformed = "$pbkdf2-sha512";

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => sut.Verify(TestPassword.AsSpan(), malformed.AsSpan()));
		Assert.Equal("Password hash is missing the iteration separator.", ex.Message);
	}

	/// <summary>
	/// Verifies that a hash missing the payload separator is rejected with a precise diagnostic.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenHashMissingPayloadSeparator_ThrowsFormatException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		const string malformed = "$pbkdf2-sha512$100000";

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => sut.Verify(TestPassword.AsSpan(), malformed.AsSpan()));
		Assert.Equal("Password hash is missing the payload separator.", ex.Message);
	}

	/// <summary>
	/// Verifies that a hash with a non-numeric iteration field is reported as a format error.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenIterationFieldIsNotNumeric_ThrowsFormatException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		const string malformed = "$pbkdf2-sha512$notanumber$AAAA";

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => sut.Verify(TestPassword.AsSpan(), malformed.AsSpan()));
		Assert.Equal(
			"Password hash iteration field ('notanumber') is not a positive integer.",
			ex.Message);
	}

	/// <summary>
	/// Verifies that an unknown algorithm identifier is reported as unsupported — not as a generic format
	/// error — so an operator can immediately tell whether storage is corrupted or whether the hash was
	/// produced by a different implementation.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenAlgorithmIsUnknown_ThrowsNotSupportedException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		const string malformed = "$argon2id$100000$AAAA";

		// Act + Assert
		var ex = Assert.Throws<NotSupportedException>(() => sut.Verify(TestPassword.AsSpan(), malformed.AsSpan()));
		Assert.Equal("Password hash algorithm 'argon2id' is not supported by this hasher.", ex.Message);
	}

	/// <summary>
	/// Verifies that an empty base64 payload is reported as a format error.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenPayloadIsEmpty_ThrowsFormatException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		const string malformed = "$pbkdf2-sha512$100000$";

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => sut.Verify(TestPassword.AsSpan(), malformed.AsSpan()));
		Assert.Equal("Password hash payload is empty.", ex.Message);
	}

	/// <summary>
	/// Verifies that a base64 payload decoding to the wrong number of bytes is reported as a format error.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenPayloadDecodesToWrongLength_ThrowsFormatException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		// 4 base64 chars decode to 3 bytes, well below the required 80.
		const string malformed = "$pbkdf2-sha512$100000$AAAA";

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => sut.Verify(TestPassword.AsSpan(), malformed.AsSpan()));
		Assert.Equal(
			"Password hash payload has unexpected length (expected 80 bytes after base64 decoding).",
			ex.Message);
	}

	/// <summary>
	/// Verifies that a hash string exceeding the documented maximum length is rejected before any
	/// parsing or base64 decoding happens.
	/// </summary>
	[Fact]
	public void Verify_WithoutRehash_WhenHashExceedsMaxLength_ThrowsFormatException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string oversized = new('A', 257);

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => sut.Verify(TestPassword.AsSpan(), oversized.AsSpan()));
		Assert.Equal(
			"Password hash length (257) exceeds the maximum of 256 characters.",
			ex.Message);
	}

	#endregion

	#region Verify(password, hash, out needsRehash)

	// --- 1. Valid scenarios ---

	/// <summary>
	/// Verifies that the combined overload returns <see langword="true"/> for a matching password and
	/// reports no rehash necessary when the stored hash already matches the current cost policy.
	/// </summary>
	[Fact]
	public void Verify_WithRehash_WhenPasswordMatchesAndIterationsAreCurrent_ReturnsTrueAndNeedsRehashFalse()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string hash = sut.Hash(TestPassword.AsSpan());

		// Act
		bool verified = sut.Verify(TestPassword.AsSpan(), hash.AsSpan(), out bool needsRehash);

		// Assert
		Assert.True(verified);
		Assert.False(needsRehash);
	}

	/// <summary>
	/// Verifies that a successful verification against a hash produced with fewer iterations than the
	/// hasher's current configuration reports <c>needsRehash = true</c>, signaling the caller to replace
	/// the stored hash on the next opportunity — the transparent cost-upgrade path.
	/// </summary>
	[Fact]
	public void Verify_WithRehash_WhenPasswordMatchesAndHashUsesFewerIterations_ReturnsTrueAndNeedsRehashTrue()
	{
		// Arrange
		// Old hash: produced at the configured minimum. Current hasher: configured higher. The successful
		// verification still uses the iteration count embedded in the old hash; needsRehash flags that the
		// stored credential is now below policy and should be re-derived.
		Pbkdf2PasswordHasher oldHasher = CreateHasher(iterations: TestIterations);
		string oldHash = oldHasher.Hash(TestPassword.AsSpan());
		Pbkdf2PasswordHasher newHasher = CreateHasher(iterations: TestIterations + 50_000);

		// Act
		bool verified = newHasher.Verify(TestPassword.AsSpan(), oldHash.AsSpan(), out bool needsRehash);

		// Assert
		Assert.True(verified);
		Assert.True(needsRehash);
	}

	/// <summary>
	/// Verifies that a hash produced with a <em>higher</em> iteration count than the hasher's current
	/// configuration is not flagged for rehash — silently downgrading would weaken security.
	/// </summary>
	[Fact]
	public void Verify_WithRehash_WhenPasswordMatchesAndHashUsesMoreIterations_ReturnsTrueAndNeedsRehashFalse()
	{
		// Arrange
		Pbkdf2PasswordHasher strongHasher = CreateHasher(iterations: TestIterations + 50_000);
		string strongHash = strongHasher.Hash(TestPassword.AsSpan());
		Pbkdf2PasswordHasher currentHasher = CreateHasher(iterations: TestIterations);

		// Act
		bool verified = currentHasher.Verify(TestPassword.AsSpan(), strongHash.AsSpan(), out bool needsRehash);

		// Assert
		Assert.True(verified);
		Assert.False(needsRehash);
	}

	/// <summary>
	/// Verifies that a wrong password against a current-cost hash returns <see langword="false"/> and
	/// reports <c>needsRehash = false</c>. Failed verifications must never request a rehash — the caller
	/// has no fresh cleartext anyway, and flagging weakness on every wrong guess would leak cost
	/// information to an attacker probing arbitrary passwords.
	/// </summary>
	[Fact]
	public void Verify_WithRehash_WhenPasswordDoesNotMatchAndIterationsAreCurrent_ReturnsFalseAndNeedsRehashFalse()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string hash = sut.Hash(TestPassword.AsSpan());

		// Act
		bool verified = sut.Verify("wrong password".AsSpan(), hash.AsSpan(), out bool needsRehash);

		// Assert
		Assert.False(verified);
		Assert.False(needsRehash);
	}

	/// <summary>
	/// Verifies the information-leak guard: even when the stored hash is technically below current policy,
	/// a wrong password must not surface that fact via <c>needsRehash = true</c>. Otherwise an attacker
	/// could enumerate which accounts use weaker hashes by submitting arbitrary passwords.
	/// </summary>
	[Fact]
	public void Verify_WithRehash_WhenPasswordDoesNotMatchAndHashUsesFewerIterations_ReturnsFalseAndNeedsRehashFalse()
	{
		// Arrange
		Pbkdf2PasswordHasher oldHasher = CreateHasher(iterations: TestIterations);
		string oldHash = oldHasher.Hash(TestPassword.AsSpan());
		Pbkdf2PasswordHasher newHasher = CreateHasher(iterations: TestIterations + 50_000);

		// Act
		bool verified = newHasher.Verify("wrong password".AsSpan(), oldHash.AsSpan(), out bool needsRehash);

		// Assert
		Assert.False(verified);
		Assert.False(needsRehash);
	}

	// --- 2. Invalid scenarios ---

	/// <summary>
	/// Verifies that the combined overload surfaces malformed input as a format exception — mirroring
	/// the single-argument <c>Verify</c> overload — rather than silently returning <see langword="false"/>
	/// with an unspecified rehash flag.
	/// </summary>
	[Fact]
	public void Verify_WithRehash_WhenHashIsMalformed_ThrowsFormatException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => sut.Verify(
			TestPassword.AsSpan(),
			"not-a-valid-hash".AsSpan(),
			out bool _));
		Assert.Equal("Password hash must start with '$'.", ex.Message);
	}

	#endregion
}
