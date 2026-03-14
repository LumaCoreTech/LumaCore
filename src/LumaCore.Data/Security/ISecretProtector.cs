// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Cryptography;

namespace LumaCore.Data.Security;

/// <summary>
/// Provides symmetric protection for secret values persisted in the database.
/// </summary>
/// <remarks>
/// Implementations are expected to authenticate ciphertext (AEAD) and to be compatible with long-term storage.
/// </remarks>
public interface ISecretProtector : IDisposable
{
	/// <summary>
	/// Encrypts a plaintext secret for persistent storage.
	/// </summary>
	/// <param name="plaintext">The plaintext value to protect.</param>
	/// <returns>The protected value suitable for storage.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="plaintext"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
	string Protect(string plaintext);

	/// <summary>
	/// Decrypts a protected secret.
	/// </summary>
	/// <param name="protectedValue">The stored protected value.</param>
	/// <returns>The plaintext value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="protectedValue"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException"><paramref name="protectedValue"/> is not in a supported format.</exception>
	/// <exception cref="CryptographicException">Authentication or decryption fails.</exception>
	/// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
	string Unprotect(string protectedValue);
}
