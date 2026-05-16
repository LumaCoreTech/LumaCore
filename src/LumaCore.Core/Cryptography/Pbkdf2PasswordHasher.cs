// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

namespace LumaCore.Core.Cryptography;

/// <summary>
/// Default <see cref="IPasswordHasher"/> implementation using PBKDF2 with HMAC-SHA512 as the underlying
/// pseudo-random function.
/// </summary>
/// <remarks>
///     <para>
///     <b>Algorithm:</b> PBKDF2 (RFC 8018) with HMAC-SHA512, a 16-byte cryptographically random salt and a
///     64-byte derived key. PBKDF2-SHA512 is FIPS-approved, ships with the .NET base class library, and at
///     the configured iteration count provides ample resistance against offline GPU-based cracking for the
///     security profile of this application. If a stronger memory-hard primitive is required in the future,
///     a sibling implementation can be introduced without breaking stored hashes — the algorithm identifier
///     embedded in each hash string makes verification routing explicit.
///     </para>
///     <para>
///     <b>Hash string format:</b> <c>$pbkdf2-sha512$&lt;iterations&gt;$&lt;base64(salt || key)&gt;</c>.
///     The leading dollar sign keeps the format compatible with the modular crypt convention used by many
///     POSIX systems and makes hashes visually distinguishable from arbitrary text in logs and database
///     dumps. The iteration count is stored alongside the salt so that verification remains correct even
///     after the configured cost is changed; <see cref="NeedsRehash"/> uses the same field to detect stale
///     hashes.
///     </para>
///     <para>
///     <b>Side-channel hardening:</b> the comparison of derived key bytes uses
///     <see cref="CryptographicOperations.FixedTimeEquals"/>, which executes in time independent of the
///     position of the first differing byte. Empty-input and length-mismatch checks happen <em>before</em>
///     the constant-time comparison and therefore do not expose useful timing information.
///     </para>
///     <para>
///     <b>Denial-of-service hardening:</b> both the cleartext password length and the encoded hash string
///     length are bounded by <see cref="MaxPasswordLength"/> and <see cref="MaxHashStringLength"/>. Without
///     these bounds an attacker controlling either input could force pathological PBKDF2 work or large
///     stack/array allocations purely by submitting oversized blobs.
///     </para>
///     <para>
///     <b>Thread safety:</b> instances are immutable after construction and may be shared across threads.
///     The class is intended to be registered as a DI singleton.
///     </para>
/// </remarks>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
	/// <summary>
	/// Algorithm identifier embedded in produced hash strings.
	/// </summary>
	private const string AlgorithmIdentifier = "pbkdf2-sha512";

	/// <summary>
	/// Salt size in bytes. 16 bytes (128 bits) is the standard recommendation and matches the salt size
	/// used by ASP.NET Core Identity.
	/// </summary>
	private const int SaltSize = 16;

	/// <summary>
	/// Derived key size in bytes. 64 bytes equals the natural output size of HMAC-SHA512, so PBKDF2 needs
	/// only a single block iteration round per output and no key bytes are wasted.
	/// </summary>
	private const int KeySize = 64;

	/// <summary>
	/// Combined size of salt and derived key in bytes (the payload that is base64-encoded into the hash
	/// string).
	/// </summary>
	private const int SaltAndKeySize = SaltSize + KeySize;

	/// <summary>
	/// Maximum accepted cleartext password length, in characters.
	/// </summary>
	/// <remarks>
	/// 1024 characters is far beyond any realistic human-chosen or generated password while bounding the
	/// transient UTF-8 buffer allocated during hashing. Inputs above this limit are rejected up front.
	/// </remarks>
	private const int MaxPasswordLength = 1024;

	/// <summary>
	/// Maximum accepted encoded hash string length, in characters.
	/// </summary>
	/// <remarks>
	/// The legitimate format is on the order of 130 characters. 256 leaves generous headroom for future
	/// algorithm identifiers or higher iteration counts while still rejecting clearly malformed input —
	/// preventing oversized base64 payloads from triggering pathological allocations during parsing.
	/// </remarks>
	private const int MaxHashStringLength = 256;

	/// <summary>
	/// Underlying PBKDF2 hash algorithm. Cached as a field so the constructor argument is the single source
	/// of truth (no scattered string literals).
	/// </summary>
	private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;

	/// <summary>
	/// Initializes a new instance of the <see cref="Pbkdf2PasswordHasher"/> class with the specified options.
	/// </summary>
	/// <param name="options">The configuration options for the hasher.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	/// <exception cref="ValidationException"><paramref name="options"/> is invalid.</exception>
	public Pbkdf2PasswordHasher(PasswordHashingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.ThrowIfInvalid();

		Iterations = options.Iterations;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Pbkdf2PasswordHasher"/> class from an
	/// <see cref="IOptions{TOptions}"/> wrapper. Suitable for direct DI registration.
	/// </summary>
	/// <param name="options">The options wrapper.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="options"/> is <see langword="null"/>, or <see cref="IOptions{TOptions}.Value"/> is
	/// <see langword="null"/>.
	/// </exception>
	/// <exception cref="ValidationException">The options are invalid.</exception>
	public Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options)
		: this(GetValue(options)) { }

	/// <summary>
	/// Gets the iteration count this instance applies to newly produced hashes.
	/// </summary>
	/// <remarks>
	/// Existing stored hashes may have been produced with a different iteration count; that value is
	/// embedded in each hash string and used unchanged during verification.
	/// </remarks>
	public int Iterations { get; }

	/// <inheritdoc/>
	public string Hash(ReadOnlySpan<char> password)
	{
		ValidatePassword(password);

		Span<byte> salt = stackalloc byte[SaltSize];
		Span<byte> key = stackalloc byte[KeySize];
		Span<byte> saltAndKey = stackalloc byte[SaltAndKeySize];

		// Salt must be cryptographically random and unique per hash; reusing salts would defeat the entire
		// purpose of slowing down rainbow-table attacks.
		RandomNumberGenerator.Fill(salt);

		// UTF-8 is the canonical encoding for password material — it is unambiguous, supports the full
		// Unicode range, and matches the encoding produced by every modern client library.
		int maxByteCount = Encoding.UTF8.GetMaxByteCount(password.Length);
		byte[] passwordBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
		try
		{
			int passwordByteCount = Encoding.UTF8.GetBytes(password, passwordBuffer);
			Rfc2898DeriveBytes.Pbkdf2(
				password: passwordBuffer.AsSpan(0, passwordByteCount),
				salt: salt,
				destination: key,
				iterations: Iterations,
				hashAlgorithm: HashAlgorithm);

			// Zero the transient UTF-8 password buffer before returning it to the pool. The pool may hand
			// the same array to unrelated callers later; leaving cleartext in it would be a needless leak.
			CryptographicOperations.ZeroMemory(passwordBuffer.AsSpan(0, passwordByteCount));
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(passwordBuffer);
		}

		salt.CopyTo(saltAndKey);
		key.CopyTo(saltAndKey[SaltSize..]);

		string base64 = Convert.ToBase64String(saltAndKey);
		return string.Create(
			CultureInfo.InvariantCulture,
			$"${AlgorithmIdentifier}${Iterations.ToString(CultureInfo.InvariantCulture)}${base64}");
	}

	/// <inheritdoc/>
	public bool Verify(ReadOnlySpan<char> password, ReadOnlySpan<char> passwordHash)
	{
		return Verify(password, passwordHash, out bool _);
	}

	/// <inheritdoc/>
	public bool Verify(ReadOnlySpan<char> password, ReadOnlySpan<char> passwordHash, out bool needsRehash)
	{
		ValidatePassword(password);
		ParseHash(passwordHash, out int iterationsFromHash, out ReadOnlySpan<char> base64Payload);

		Span<byte> storedSaltAndKey = stackalloc byte[SaltAndKeySize];
		if (!Convert.TryFromBase64Chars(base64Payload, storedSaltAndKey, out int decodedByteCount) ||
		    decodedByteCount != SaltAndKeySize)
		{
			throw new FormatException(
				$"Password hash payload has unexpected length (expected {SaltAndKeySize} bytes after base64 decoding).");
		}

		ReadOnlySpan<byte> storedSalt = storedSaltAndKey[..SaltSize];
		ReadOnlySpan<byte> storedKey = storedSaltAndKey[SaltSize..];

		Span<byte> derivedKey = stackalloc byte[KeySize];
		int maxByteCount = Encoding.UTF8.GetMaxByteCount(password.Length);
		byte[] passwordBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
		try
		{
			int passwordByteCount = Encoding.UTF8.GetBytes(password, passwordBuffer);

			// Use the iteration count embedded in the stored hash, not the currently configured one — the
			// stored hash is the contract, and verification must remain correct across cost upgrades.
			Rfc2898DeriveBytes.Pbkdf2(
				password: passwordBuffer.AsSpan(0, passwordByteCount),
				salt: storedSalt,
				destination: derivedKey,
				iterations: iterationsFromHash,
				hashAlgorithm: HashAlgorithm);

			CryptographicOperations.ZeroMemory(passwordBuffer.AsSpan(0, passwordByteCount));
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(passwordBuffer);
		}

		// Constant-time comparison: do not branch on the first differing byte. FixedTimeEquals() also
		// returns false for length mismatches, but lengths are already guaranteed equal here.
		bool verified = CryptographicOperations.FixedTimeEquals(derivedKey, storedKey);

		// Only signal rehash on a successful verification. A failed verification means the caller has no
		// fresh cleartext to re-hash with, and we would also leak information about whether the stored
		// hash uses weaker parameters to any caller probing with arbitrary passwords.
		needsRehash = verified && iterationsFromHash < Iterations;
		return verified;
	}

	/// <inheritdoc/>
	public bool NeedsRehash(ReadOnlySpan<char> passwordHash)
	{
		ParseHash(passwordHash, out int iterationsFromHash, out ReadOnlySpan<char> _);

		// Only "weaker than current" triggers a rehash. A hash created with a higher iteration count than
		// the current configuration is still acceptable — downgrading silently would weaken security.
		return iterationsFromHash < Iterations;
	}

	private static void ValidatePassword(ReadOnlySpan<char> password)
	{
		if (password.IsEmpty)
		{
			throw new ArgumentException("Password must not be empty.", nameof(password));
		}

		if (password.Length > MaxPasswordLength)
		{
			throw new ArgumentException(
				$"Password length ({password.Length}) exceeds the maximum of {MaxPasswordLength} characters.",
				nameof(password));
		}
	}

	/// <summary>
	/// Parses the algorithm identifier, iteration count and base64 payload out of a stored hash string and
	/// validates that the algorithm is recognized.
	/// </summary>
	/// <param name="passwordHash">The hash string to parse.</param>
	/// <param name="iterations">When this method returns, the parsed iteration count.</param>
	/// <param name="base64Payload">When this method returns, the base64-encoded salt-and-key payload.</param>
	/// <exception cref="ArgumentException"><paramref name="passwordHash"/> is empty.</exception>
	/// <exception cref="FormatException">The hash does not match the expected format or the iteration field is malformed.</exception>
	/// <exception cref="NotSupportedException">The embedded algorithm identifier is unknown.</exception>
	private static void ParseHash(
		ReadOnlySpan<char>     passwordHash,
		out int                iterations,
		out ReadOnlySpan<char> base64Payload)
	{
		if (passwordHash.IsEmpty)
		{
			throw new ArgumentException("Password hash must not be empty.", nameof(passwordHash));
		}

		if (passwordHash.Length > MaxHashStringLength)
		{
			throw new FormatException(
				$"Password hash length ({passwordHash.Length}) exceeds the maximum of {MaxHashStringLength} characters.");
		}

		// Expected format: $<algorithm>$<iterations>$<base64>
		// Parsed in three steps so a malformed segment yields a precise, actionable error message rather
		// than a generic "format incorrect" — operators inspecting bad rows in the database benefit from
		// the specificity.
		if (passwordHash.IsEmpty || passwordHash[0] != '$')
		{
			throw new FormatException("Password hash must start with '$'.");
		}

		ReadOnlySpan<char> remainder = passwordHash[1..];

		int firstSeparator = remainder.IndexOf('$');
		if (firstSeparator < 0)
		{
			throw new FormatException("Password hash is missing the iteration separator.");
		}

		ReadOnlySpan<char> algorithm = remainder[..firstSeparator];
		remainder = remainder[(firstSeparator + 1)..];

		int secondSeparator = remainder.IndexOf('$');
		if (secondSeparator < 0)
		{
			throw new FormatException("Password hash is missing the payload separator.");
		}

		ReadOnlySpan<char> iterationsField = remainder[..secondSeparator];
		base64Payload = remainder[(secondSeparator + 1)..];

		if (!algorithm.Equals(AlgorithmIdentifier, StringComparison.Ordinal))
		{
			throw new NotSupportedException($"Password hash algorithm '{algorithm}' is not supported by this hasher.");
		}

		if (!int.TryParse(iterationsField, NumberStyles.None, CultureInfo.InvariantCulture, out iterations) ||
		    iterations < 1)
		{
			throw new FormatException(
				$"Password hash iteration field ('{iterationsField}') is not a positive integer.");
		}

		if (base64Payload.IsEmpty)
		{
			throw new FormatException("Password hash payload is empty.");
		}
	}

	private static PasswordHashingOptions GetValue(IOptions<PasswordHashingOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return options.Value ?? throw new ArgumentNullException(nameof(options), "Options value is null.");
	}
}
