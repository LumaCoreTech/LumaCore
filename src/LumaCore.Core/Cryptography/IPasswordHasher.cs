// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Cryptography;

namespace LumaCore.Core.Cryptography;

/// <summary>
/// Hashes and verifies user passwords using a salted, iterated key-derivation function and produces
/// version-tagged hash strings that can be safely persisted and migrated over time.
/// </summary>
/// <remarks>
///     <para>
///     A produced hash string is self-describing: it carries the algorithm identifier, the iteration count,
///     the salt and the derived key in a single, compact text form. Verification is therefore stateless —
///     a stored hash can be verified without knowing which algorithm or iteration count was active when it
///     was created. This decoupling enables transparent algorithm and cost upgrades over the lifetime of
///     stored credentials via <see cref="NeedsRehash(ReadOnlySpan{char})"/>.
///     </para>
///     <para>
///     <b>Comparison semantics:</b> verification is performed in constant time with respect to the salted
///     hash bytes to prevent timing-based side-channel attacks. Implementations must use a constant-time
///     byte-comparison primitive (for example, <see cref="CryptographicOperations.FixedTimeEquals"/>).
///     </para>
///     <para>
///     <b>Thread safety:</b> implementations of this interface must be safe for concurrent use from
///     multiple threads.
///     </para>
/// </remarks>
public interface IPasswordHasher
{
	/// <summary>
	/// Derives a new password hash for <paramref name="password"/> using a freshly generated salt and the
	/// implementation's currently configured cost parameters.
	/// </summary>
	/// <param name="password">The cleartext password to hash.</param>
	/// <returns>
	/// A self-describing hash string of the form <c>$&lt;algorithm&gt;$&lt;iterations&gt;$&lt;base64-salt-and-key&gt;</c>
	/// suitable for persistent storage.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="password"/> is empty.
	/// </exception>
	string Hash(ReadOnlySpan<char> password);

	/// <summary>
	/// Verifies <paramref name="password"/> against a previously produced hash string.
	/// </summary>
	/// <param name="password">The cleartext password to check.</param>
	/// <param name="passwordHash">The stored hash string produced by an earlier call to <see cref="Hash"/>.</param>
	/// <returns>
	/// <see langword="true"/> if the password matches the stored hash; otherwise <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// The comparison of derived key bytes is performed in constant time to mitigate timing side channels.
	/// Malformed or unsupported hash strings cause an exception rather than a silent <see langword="false"/>
	/// result so that storage corruption is not mistaken for an authentication failure.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="password"/> is empty, or <paramref name="passwordHash"/> is empty.
	/// </exception>
	/// <exception cref="FormatException">
	/// <paramref name="passwordHash"/> is not in the expected
	/// <c>$&lt;algorithm&gt;$&lt;iterations&gt;$&lt;base64&gt;</c> shape, the iteration field is not a positive
	/// integer, or the base64 payload does not have the expected length.
	/// </exception>
	/// <exception cref="NotSupportedException">
	/// The algorithm identifier embedded in <paramref name="passwordHash"/> is not recognized by this
	/// implementation.
	/// </exception>
	bool Verify(ReadOnlySpan<char> password, ReadOnlySpan<char> passwordHash);

	/// <summary>
	/// Verifies <paramref name="password"/> against a previously produced hash string and, in the same
	/// call, reports whether the stored hash should be replaced with a fresh one on success.
	/// </summary>
	/// <param name="password">The cleartext password to check.</param>
	/// <param name="passwordHash">The stored hash string produced by an earlier call to <see cref="Hash"/>.</param>
	/// <param name="needsRehash">
	/// When this method returns, contains <see langword="true"/> if the stored hash was produced with
	/// weaker parameters than the implementation currently uses and the caller should replace it with the
	/// result of a fresh <see cref="Hash"/> call; otherwise <see langword="false"/>. The value is always
	/// <see langword="false"/> when the method returns <see langword="false"/> — a failed verification is
	/// not an occasion to rehash anything.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the password matches the stored hash; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="password"/> is empty, or <paramref name="passwordHash"/> is empty.
	/// </exception>
	/// <exception cref="FormatException">
	/// <paramref name="passwordHash"/> is not in the expected
	/// <c>$&lt;algorithm&gt;$&lt;iterations&gt;$&lt;base64&gt;</c> shape, the iteration field is not a positive
	/// integer, or the base64 payload does not have the expected length.
	/// </exception>
	/// <exception cref="NotSupportedException">
	/// The algorithm identifier embedded in <paramref name="passwordHash"/> is not recognized by this
	/// implementation.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This overload is the preferred entry point on the authentication hot path: it folds the
	///     rehash-decision into the verification call so the hash string is parsed exactly once. Compared
	///     to calling <see cref="Verify(ReadOnlySpan{char}, ReadOnlySpan{char})"/> followed by
	///     <see cref="NeedsRehash(ReadOnlySpan{char})"/> on every successful login, it removes a redundant
	///     parse, format validation, and algorithm dispatch.
	///     </para>
	///     <para>
	///     The comparison of derived key bytes is performed in constant time to mitigate timing side
	///     channels. Malformed or unsupported hash strings cause an exception rather than a silent
	///     <see langword="false"/> result so that storage corruption is not mistaken for an authentication
	///     failure.
	///     </para>
	/// </remarks>
	bool Verify(ReadOnlySpan<char> password, ReadOnlySpan<char> passwordHash, out bool needsRehash);

	/// <summary>
	/// Indicates whether <paramref name="passwordHash"/> was produced with weaker parameters than the
	/// implementation currently uses and should be re-hashed on the next successful authentication.
	/// </summary>
	/// <param name="passwordHash">The stored hash string to inspect.</param>
	/// <returns>
	/// <see langword="true"/> if the algorithm or iteration count embedded in the hash is weaker than the
	/// current configuration; otherwise <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// Callers typically invoke this immediately after a successful <see cref="Verify"/> and, when it returns
	/// <see langword="true"/>, replace the stored hash with the result of a fresh <see cref="Hash"/> call. This
	/// enables transparent migration to stronger cost parameters without invalidating existing credentials.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="passwordHash"/> is empty.
	/// </exception>
	/// <exception cref="FormatException">
	/// <paramref name="passwordHash"/> is not in the expected
	/// <c>$&lt;algorithm&gt;$&lt;iterations&gt;$&lt;base64&gt;</c> shape, or the iteration field is not a positive
	/// integer.
	/// </exception>
	/// <exception cref="NotSupportedException">
	/// The algorithm identifier embedded in <paramref name="passwordHash"/> is not recognized by this
	/// implementation.
	/// </exception>
	bool NeedsRehash(ReadOnlySpan<char> passwordHash);
}
