// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Cryptography;

using Xunit;

namespace LumaCore.Core.Tests.Cryptography;

public partial class Pbkdf2PasswordHasherTests
{
	// --- 1. Valid scenarios ---

	/// <summary>
	/// Verifies that a hash created with the current configuration does not need a rehash.
	/// </summary>
	[Fact]
	public void NeedsRehash_WhenHashUsesCurrentIterations_ReturnsFalse()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		string hash = sut.Hash(TestPassword.AsSpan());

		// Act
		bool result = sut.NeedsRehash(hash.AsSpan());

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that an older hash (produced with weaker iterations than now configured) is flagged for
	/// rehashing on next login — this is the migration mechanism in action.
	/// </summary>
	[Fact]
	public void NeedsRehash_WhenHashUsesFewerIterations_ReturnsTrue()
	{
		// Arrange
		// Produce a hash at the configured minimum, then ask a hasher configured higher whether it should
		// be replaced — yes, because the stored cost is now weaker than the policy demands.
		Pbkdf2PasswordHasher oldHasher = CreateHasher(iterations: TestIterations);
		string oldHash = oldHasher.Hash(TestPassword.AsSpan());
		Pbkdf2PasswordHasher newHasher = CreateHasher(iterations: TestIterations + 50_000);

		// Act
		bool result = newHasher.NeedsRehash(oldHash.AsSpan());

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Verifies that a hash with a <em>higher</em> iteration count than currently configured is not flagged
	/// for rehash — silently downgrading would weaken security.
	/// </summary>
	[Fact]
	public void NeedsRehash_WhenHashUsesMoreIterations_ReturnsFalse()
	{
		// Arrange
		Pbkdf2PasswordHasher strongHasher = CreateHasher(iterations: TestIterations + 50_000);
		string strongHash = strongHasher.Hash(TestPassword.AsSpan());
		Pbkdf2PasswordHasher currentHasher = CreateHasher(iterations: TestIterations);

		// Act
		bool result = currentHasher.NeedsRehash(strongHash.AsSpan());

		// Assert
		Assert.False(result);
	}

	// --- 2. Invalid scenarios ---

	/// <summary>
	/// Verifies that <c>NeedsRehash</c> rejects an empty hash string up front rather than reporting a
	/// misleading boolean answer for malformed storage.
	/// </summary>
	[Fact]
	public void NeedsRehash_WhenHashIsEmpty_ThrowsArgumentException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => sut.NeedsRehash(ReadOnlySpan<char>.Empty));
		Assert.Equal("passwordHash", ex.ParamName);
	}

	/// <summary>
	/// Verifies that a malformed hash surfaces as a format exception, mirroring <c>Verify</c>.
	/// </summary>
	[Fact]
	public void NeedsRehash_WhenHashIsMalformed_ThrowsFormatException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();

		// Act + Assert
		var ex = Assert.Throws<FormatException>(() => sut.NeedsRehash("not-a-valid-hash".AsSpan()));
		Assert.Equal("Password hash must start with '$'.", ex.Message);
	}

	/// <summary>
	/// Verifies that an unknown algorithm identifier surfaces as <see cref="NotSupportedException"/>.
	/// </summary>
	[Fact]
	public void NeedsRehash_WhenAlgorithmIsUnknown_ThrowsNotSupportedException()
	{
		// Arrange
		Pbkdf2PasswordHasher sut = CreateHasher();
		const string foreignHash = "$argon2id$100000$AAAA";

		// Act + Assert
		var ex = Assert.Throws<NotSupportedException>(() => sut.NeedsRehash(foreignHash.AsSpan()));
		Assert.Equal("Password hash algorithm 'argon2id' is not supported by this hasher.", ex.Message);
	}
}
