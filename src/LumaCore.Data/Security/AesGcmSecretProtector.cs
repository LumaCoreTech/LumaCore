// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Buffers;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

namespace LumaCore.Data.Security;

/// <summary>
/// Protects secrets using AES-GCM with a key derived from <see cref="DatabaseOptions.EncryptionKey"/>.
/// </summary>
/// <remarks>
///     <para>
///     Output format: <c>v1:&lt;fingerprint&gt;:&lt;base64(nonce|tag|ciphertext)&gt;</c>.
///     </para>
///     <para>
///     The <c>fingerprint</c> is a compact identifier (16 lowercase hex characters) derived from the first
///     8 bytes of the SHA-256 hash of the derived AES key. Embedding the fingerprint allows the protector to
///     select the correct key during decryption after key rotation.
///     </para>
///     <para>
///     The protected payload contains three components:
///     <list type="bullet">
///         <item>
///             <term>
///                 <c>nonce</c>
///             </term>
///             <description>
///             A per-encryption initialization vector (IV). In this implementation <see cref="NonceSize"/> is used
///             (12 bytes / 96 bits). The nonce makes encryption nondeterministic and must not be reused with the same key.
///             It is not secret and is stored alongside the ciphertext.
///             </description>
///         </item>
///         <item>
///             <term>
///                 <c>tag</c>
///             </term>
///             <description>
///             The authentication tag (message authentication code) produced by AES-GCM. Here <see cref="TagSize"/>
///             (16 bytes / 128 bits) is used. The tag guarantees integrity and authenticity; decryption fails if the tag
///             does not verify.
///             </description>
///         </item>
///         <item>
///             <term>
///                 <c>ciphertext</c>
///             </term>
///             <description>
///             The encrypted form of the plaintext. For AES-GCM the ciphertext length equals the plaintext length.
///             The ciphertext, together with the nonce and tag, is required for successful decryption.
///             </description>
///         </item>
///     </list>
///     </para>
///     <para>
///     This implementation is optimized for minimal GC pressure using <see cref="ArrayPool{T}"/> and
///     <see cref="Span{T}"/>.
///     </para>
/// </remarks>
public sealed class AesGcmSecretProtector : ISecretProtector
{
	/// <summary>
	/// The prefix used to identify the protected value format version.
	/// </summary>
	private const string VersionPrefix = "v1:";

	/// <summary>
	/// The separator character used between components in the protected value format.
	/// </summary>
	private const char ComponentSeparator = ':';

	/// <summary>
	/// Represents the size, in bytes, of the nonce used for cryptographic operations.
	/// </summary>
	private const int NonceSize = 12;

	/// <summary>
	/// Represents the size, in bytes, of the authentication tag used in cryptographic operations.
	/// </summary>
	private const int TagSize = 16;

	/// <summary>
	/// Represents the fixed length, in bytes, of a fingerprint value.
	/// </summary>
	/// <remarks>
	/// A value of 8 bytes results in a 16-character hexadecimal string.
	/// This length is selected to provide a balance between compactness and uniqueness for typical deployment scenarios.
	/// </remarks>
	private const int FingerprintLength = 8;

	/// <summary>
	/// The length of the fingerprint when represented as a hexadecimal string.
	/// </summary>
	/// <remarks>
	/// Each byte is represented as two hex characters, so this is always <c><see cref="FingerprintLength"/> * 2</c>.
	/// </remarks>
	private const int FingerprintHexLength = FingerprintLength * 2;

	/// <summary>
	/// HKDF "info" parameter for domain separation when deriving keys for AES-GCM.
	/// </summary>
	/// <remarks>
	/// This value is set during construction and determines the cryptographic domain.
	/// Different domains produce different derived keys from the same key material.
	/// </remarks>
	private readonly string mDomain;

	/// <summary>
	/// The fingerprint of the primary encryption key, used for key identification during decryption.
	/// </summary>
	private readonly string mPrimaryFingerprint;

	/// <summary>
	/// Raw bytes of the primary fingerprint (leading bytes of the key hash) used for fixed-time comparisons.
	/// </summary>
	private readonly byte[] mPrimaryFingerprintBytes;

	/// <summary>
	/// The derived primary AES-256 key used for encryption operations.
	/// </summary>
	private readonly byte[] mPrimaryKey;

	/// <summary>
	/// Collection of fallback keys with their fingerprints, used for decryption after key rotation.
	/// </summary>
	private readonly IReadOnlyList<(string fingerprint, byte[] fingerprintBytes, byte[] key)> mFallbackKeys;

	/// <summary>
	/// Indicates whether this instance has been disposed.
	/// </summary>
	private bool mDisposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AesGcmSecretProtector"/> class with the default domain.
	/// </summary>
	/// <param name="options">The database options containing <see cref="DatabaseOptions.EncryptionKey"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Uses <see cref="SecretProtectorDomains.Default"/> for backward compatibility with existing encrypted data.
	/// </remarks>
	public AesGcmSecretProtector(IOptions<DatabaseOptions> options)
		: this(options, SecretProtectorDomains.Default) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="AesGcmSecretProtector"/> class with a specific domain.
	/// </summary>
	/// <param name="options">The database options containing <see cref="DatabaseOptions.EncryptionKey"/>.</param>
	/// <param name="domain">
	/// The HKDF domain identifier for key derivation (e.g., <see cref="SecretProtectorDomains.ModelEndpointCredentials"/>).
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="options"/> is <see langword="null"/>,
	/// or <paramref name="domain"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="domain"/> is empty or consists only of whitespace characters.
	/// </exception>
	/// <remarks>
	///     <para>
	///     The domain parameter provides cryptographic isolation between different use cases.
	///     Secrets protected with one domain cannot be decrypted by a protector using a different domain.
	///     </para>
	///     <para>
	///     Use <see cref="SecretProtectorDomains"/> constants for well-known domains.
	///     </para>
	/// </remarks>
	public AesGcmSecretProtector(IOptions<DatabaseOptions> options, string domain)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(domain);

		mDomain = domain;

		DatabaseOptions dbOptions = options.Value;
		mPrimaryKey = DeriveKey(dbOptions.EncryptionKey);

		// Compute SHA-256 hash and derive fixed-length fingerprint for primary key.
		byte[] primaryHash = SHA256.HashData(mPrimaryKey);
		mPrimaryFingerprint = ToHexFingerprint(primaryHash, FingerprintLength);
		mPrimaryFingerprintBytes = primaryHash[..FingerprintLength].ToArray();

		// Prepare fallback keys for decryption (if any).
		mFallbackKeys = dbOptions.PreviousEncryptionKeys
			.Where(k => !string.IsNullOrWhiteSpace(k))
			.Select(k =>
			{
				byte[] key = DeriveKey(k);
				byte[] hash = SHA256.HashData(key);
				return (fingerprint: ToHexFingerprint(hash, FingerprintLength),
				        fingerprintBytes: hash[..FingerprintLength].ToArray(), key);
			})
			.ToArray();
	}

	/// <summary>
	/// Securely clears all key material from memory.
	/// </summary>
	public void Dispose()
	{
		if (mDisposed)
			return;

		// Zero out key material.
		// Note: strings are immutable and cannot be zeroed; however, fingerprints are not secret.
		CryptographicOperations.ZeroMemory(mPrimaryKey);
		CryptographicOperations.ZeroMemory(mPrimaryFingerprintBytes);
		foreach ((string _, byte[] fingerprintBytes, byte[] key) in mFallbackKeys)
		{
			CryptographicOperations.ZeroMemory(fingerprintBytes);
			CryptographicOperations.ZeroMemory(key);
		}

		mDisposed = true;
	}

	/// <summary>
	/// Gets the number of fallback keys registered with this protector.
	/// </summary>
	/// <remarks>
	/// This property is internal for testing purposes and returns the count of valid fallback keys
	/// that were retained after filtering out empty/null values during construction.
	/// </remarks>
	internal int FallbackKeyCount => mFallbackKeys.Count;

	/// <inheritdoc/>
	public string Protect(string plaintext)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);
		ArgumentNullException.ThrowIfNull(plaintext);

		// Use stackalloc for nonce and tag (small, fixed-size buffers).
		Span<byte> nonce = stackalloc byte[NonceSize];
		Span<byte> tag = stackalloc byte[TagSize];
		RandomNumberGenerator.Fill(nonce);

		// Calculate required buffer size.
		int maxPlaintextByteCount = Encoding.UTF8.GetMaxByteCount(plaintext.Length);
		int combinedBufferSize = NonceSize + TagSize + maxPlaintextByteCount;

		// Rent buffers from ArrayPool to minimize GC pressure.
		byte[] plaintextBuffer = ArrayPool<byte>.Shared.Rent(maxPlaintextByteCount);
		byte[] ciphertextBuffer = ArrayPool<byte>.Shared.Rent(maxPlaintextByteCount);
		byte[] combinedBuffer = ArrayPool<byte>.Shared.Rent(combinedBufferSize);

		try
		{
			// Encode plaintext directly into rented buffer.
			int plaintextByteCount = Encoding.UTF8.GetBytes(plaintext, plaintextBuffer);
			ReadOnlySpan<byte> plaintextBytes = plaintextBuffer.AsSpan(0, plaintextByteCount);
			Span<byte> ciphertext = ciphertextBuffer.AsSpan(0, plaintextByteCount);

			// Encrypt
			using var aesGcm = new AesGcm(mPrimaryKey, TagSize);
			aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

			// Combine nonce, tag, and ciphertext into single buffer.
			nonce.CopyTo(combinedBuffer);
			tag.CopyTo(combinedBuffer.AsSpan(NonceSize));
			ciphertext.CopyTo(combinedBuffer.AsSpan(NonceSize + TagSize));

			int totalLength = NonceSize + TagSize + plaintextByteCount;

			// Use string.Create() to build result string with single allocation.
			int prefixLength = VersionPrefix.Length + mPrimaryFingerprint.Length + 1; // "v1:fingerprint:"
			int base64Length = (totalLength + 2) / 3 * 4;
			int totalStringLength = prefixLength + base64Length;
			return string.Create(
				totalStringLength,
				(combinedBuffer, totalLength, _primaryFingerprint: mPrimaryFingerprint),
				static (buffer, state) =>
				{
					int pos = 0;

					// Write "v1:".
					VersionPrefix.AsSpan().CopyTo(buffer);
					pos += VersionPrefix.Length;

					// Write fingerprint.
					state._primaryFingerprint.AsSpan().CopyTo(buffer[pos..]);
					pos += state._primaryFingerprint.Length;

					// Write separator.
					buffer[pos++] = ComponentSeparator;

					// Write Base64 directly into string buffer.
					Convert.TryToBase64Chars(
						state.combinedBuffer.AsSpan(0, state.totalLength),
						buffer[pos..],
						out int _);
				});
		}
		finally
		{
			// Return buffers to pool.
			ArrayPool<byte>.Shared.Return(plaintextBuffer, clearArray: true);
			ArrayPool<byte>.Shared.Return(ciphertextBuffer, clearArray: true);
			ArrayPool<byte>.Shared.Return(combinedBuffer, clearArray: true);
		}
	}

	/// <inheritdoc/>
	public string Unprotect(string protectedValue)
	{
		ObjectDisposedException.ThrowIf(mDisposed, this);
		ArgumentNullException.ThrowIfNull(protectedValue);

		ReadOnlySpan<char> protectedSpan = protectedValue.AsSpan();

		// Use span-based string parsing to avoid allocations.
		if (!protectedSpan.StartsWith(VersionPrefix.AsSpan(), StringComparison.Ordinal))
			throw new FormatException("Unsupported protected value format.");

		ReadOnlySpan<char> rest = protectedSpan[VersionPrefix.Length..];
		int separatorIndex = rest.IndexOf(ComponentSeparator);
		if (separatorIndex <= 0)
			throw new FormatException("Invalid protected value payload.");

		ReadOnlySpan<char> fingerprintSpan = rest[..separatorIndex];
		ReadOnlySpan<char> payloadSpan = rest[(separatorIndex + 1)..];

		// Validate fingerprint length before further processing.
		// A valid fingerprint is exactly FingerprintHexLength characters (16 hex chars for 8 bytes).
		if (fingerprintSpan.Length != FingerprintHexLength)
			throw new FormatException($"Invalid fingerprint length. Expected {FingerprintHexLength} characters.");

		// Convert fingerprint hex string to bytes for timing-safe comparison.
		// SECURITY: We must decode the hex string to raw bytes, not just cast char -> byte.
		// Note: Using heap allocation here because the bytes need to be captured in lambdas below.
		byte[] fingerprintBytes;
		try
		{
			fingerprintBytes = Convert.FromHexString(fingerprintSpan);
		}
		catch (FormatException)
		{
			throw new FormatException("Invalid fingerprint format. Expected hexadecimal.");
		}

		// Decode base64 into rented buffer.
		// Note: fingerprintBytes.Length is guaranteed to be FingerprintLength (8) because
		// we validated fingerprintSpan.Length == FingerprintHexLength (16) and hex decoding
		// produces exactly half as many bytes.
		int maxDecodedLength = (payloadSpan.Length * 3 + 3) / 4;
		byte[] combinedBuffer = ArrayPool<byte>.Shared.Rent(maxDecodedLength);

		try
		{
			if (!Convert.TryFromBase64Chars(payloadSpan, combinedBuffer, out int bytesWritten))
				throw new FormatException("Invalid base64 payload.");

			ReadOnlySpan<byte> combined = combinedBuffer.AsSpan(0, bytesWritten);

			if (combined.Length < NonceSize + TagSize)
				throw new FormatException("Invalid protected value payload.");

			ReadOnlySpan<byte> nonce = combined[..NonceSize];
			ReadOnlySpan<byte> tag = combined.Slice(NonceSize, TagSize);
			ReadOnlySpan<byte> ciphertext = combined[(NonceSize + TagSize)..];

			// Rent buffer for plaintext.
			byte[] plaintextBuffer = ArrayPool<byte>.Shared.Rent(ciphertext.Length);

			try
			{
				Span<byte> plaintextBytes = plaintextBuffer.AsSpan(0, ciphertext.Length);

				// SECURITY: Determine which key to try first based on the embedded fingerprint.
				// Using FixedTimeEquals prevents timing attacks that could reveal which key is in use.
				// We always use byte comparison (not string comparison) to ensure constant-time behavior.
				bool isPrimaryKey = CryptographicOperations.FixedTimeEquals(fingerprintBytes, mPrimaryFingerprintBytes);

				// Build key order: fingerprint-matched key first, then remaining keys.
				// This optimization reduces decryption attempts while still supporting key rotation.
				IEnumerable<byte[]> keysToTry;
				if (isPrimaryKey)
				{
					// Primary key matches fingerprint - try it first, then fallbacks.
					keysToTry = new[] { mPrimaryKey }.Concat(mFallbackKeys.Select(x => x.key));
				}
				else
				{
					// SECURITY: Use timing-safe byte comparison for all fallback keys.
					// Find matching fallback key(s) first, then try primary, then non-matching fallbacks.
					IEnumerable<byte[]> matchingFallbacks = mFallbackKeys
						.Where(x => CryptographicOperations.FixedTimeEquals(x.fingerprintBytes, fingerprintBytes))
						.Select(x => x.key);
					IEnumerable<byte[]> nonMatchingFallbacks = mFallbackKeys
						.Where(x => !CryptographicOperations.FixedTimeEquals(x.fingerprintBytes, fingerprintBytes))
						.Select(x => x.key);
					keysToTry = matchingFallbacks
						.Concat([mPrimaryKey])
						.Concat(nonMatchingFallbacks);
				}

				// Try each key until one successfully decrypts the payload.
				// SECURITY NOTE: Fingerprint collisions are theoretically possible (though highly unlikely
				// with 64-bit fingerprints), but they are harmless. The GCM authentication tag ensures that
				// only the correct key can successfully decrypt. A wrong key will always fail authentication,
				// causing a CryptographicException, and the next key will be tried automatically.
				// This makes the system resilient: even if a fingerprint collision occurs, decryption will
				// eventually succeed with the correct key (at the cost of one additional decryption attempt).
				CryptographicException? lastError = null;
				foreach (byte[] key in keysToTry)
				{
					try
					{
						using var aesGcm = new AesGcm(key, TagSize);
						aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);
						return Encoding.UTF8.GetString(plaintextBytes);
					}
					catch (CryptographicException ex)
					{
						lastError = ex;
					}
				}

				// lastError is never null here because keysToTry always contains at least the primary key.
				throw lastError!;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(plaintextBuffer, clearArray: true);
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(combinedBuffer, clearArray: true);
		}
	}

	/// <summary>
	/// Derives a fixed-length AES-256 key from the configured key material using HKDF-SHA256.
	/// </summary>
	/// <param name="keyMaterial">The configured key material (passphrase or high-entropy secret).</param>
	/// <returns>A 32-byte key suitable for AES-256-GCM.</returns>
	/// <remarks>
	///     <para>
	///     Uses HKDF (RFC 5869) with SHA-256 for key derivation:
	///     <list type="bullet">
	///         <item><b>Salt:</b> Empty (zero-length) per RFC 5869 §3.1 recommendation for high-entropy IKM.</item>
	///         <item><b>Info:</b> Domain-separation string <see cref="mDomain"/> to bind the derived key to this use case.</item>
	///     </list>
	///     </para>
	///     <para>
	///     SECURITY: The input key material should be a high-entropy secret (≥256 bits of entropy).
	///     If the input is a low-entropy password, consider using a password-based KDF like Argon2 instead.
	///     </para>
	/// </remarks>
	private byte[] DeriveKey(string keyMaterial)
	{
		if (string.IsNullOrEmpty(keyMaterial))
			throw new ArgumentException("Encryption key must not be null or empty.", nameof(keyMaterial));

		const int keyLength = 32; // AES-256 key size

		// Convert key material to bytes using pooled buffer for minimal allocations.
		int maxByteCount = Encoding.UTF8.GetMaxByteCount(keyMaterial.Length);
		byte[] materialBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);

		try
		{
			int bytesWritten = Encoding.UTF8.GetBytes(keyMaterial, materialBuffer);

			// Use the Span-based HKDF overload to derive the key directly from the pooled buffer.
			// This avoids .ToArray() which would create an uncleared copy of the key material on the
			// managed heap. The pooled buffer itself is cleared on return (see finally block).
			byte[] derivedKey = new byte[keyLength];
			HKDF.DeriveKey(
				HashAlgorithmName.SHA256,
				materialBuffer.AsSpan(0, bytesWritten),
				derivedKey,
				salt: default,                          // Empty salt per RFC 5869 for high-entropy IKM
				info: Encoding.UTF8.GetBytes(mDomain)); // Domain separation
			return derivedKey;
		}
		finally
		{
			// SECURITY: Clear key material from pooled buffer before returning.
			ArrayPool<byte>.Shared.Return(materialBuffer, clearArray: true);
		}
	}

	/// <summary>
	/// Convert the leading bytes of a hash into a lowercase hex fingerprint.
	/// </summary>
	/// <param name="hash">Full hash bytes (e.g., SHA-256 output).</param>
	/// <param name="lengthBytes">Number of leading bytes to use for the fingerprint.</param>
	/// <returns>Hex string (lowercase) representing the fingerprint.</returns>
	private static string ToHexFingerprint(ReadOnlySpan<byte> hash, int lengthBytes)
	{
		// Use ToHexStringLower (available since .NET 8) for direct lowercase conversion.
		// This is more efficient than ToHexString().ToLowerInvariant() as it avoids an extra allocation.
		// Note: lengthBytes is always FingerprintLength (8) when called from this class.
		return Convert.ToHexStringLower(hash[..lengthBytes]);
	}
}
