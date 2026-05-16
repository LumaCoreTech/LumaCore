// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Cryptography;

using Microsoft.Extensions.Options;

namespace LumaCore.Core.Tests.Cryptography;

public partial class Pbkdf2PasswordHasherTests
{
	/// <summary>
	/// Iteration count used by tests. Equal to the configured minimum so hashing stays fast in CI while
	/// still exercising the same code paths and validation rules as production.
	/// </summary>
	private const int TestIterations = 100_000;

	/// <summary>
	/// Cleartext password reused across tests where the actual value is irrelevant to the assertion.
	/// </summary>
	private const string TestPassword = "correct horse battery staple";

	/// <summary>
	/// Lazy cache of one known-good hash produced from <see cref="TestPassword"/>. Computing it once and
	/// reusing the value across tests keeps the suite fast while still letting individual tests assert on a
	/// real, format-conforming hash string.
	/// </summary>
	private static readonly Lazy<string> CachedHash = new(
		() => CreateHasher().Hash(TestPassword.AsSpan()),
		isThreadSafe: true);

	/// <summary>
	/// Creates a hasher configured with the test iteration count.
	/// </summary>
	/// <param name="iterations">Optional override for the iteration count.</param>
	/// <returns>A fresh <see cref="Pbkdf2PasswordHasher"/> instance.</returns>
	private static Pbkdf2PasswordHasher CreateHasher(int iterations = TestIterations)
	{
		return new Pbkdf2PasswordHasher(new PasswordHashingOptions { Iterations = iterations });
	}

	/// <summary>
	/// Wraps options in <see cref="IOptions{TOptions}"/> for tests that exercise the IOptions constructor.
	/// </summary>
	/// <param name="options">
	/// The options to wrap. Pass <see langword="null"/> to obtain a wrapper whose
	/// <see cref="IOptions{TOptions}.Value"/> is <see langword="null"/>.
	/// </param>
	/// <returns>An <see cref="IOptions{TOptions}"/> wrapper.</returns>
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
	private static IOptions<PasswordHashingOptions> Wrap(PasswordHashingOptions? options)
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
	{
		return new TestOptionsWrapper(options);
	}

	/// <summary>
	/// Minimal <see cref="IOptions{TOptions}"/> implementation that allows a <see langword="null"/> value
	/// (the BCL <see cref="Options.Create{TOptions}"/> rejects <see langword="null"/> in its argument).
	/// </summary>
	/// <param name="value">The wrapped value.</param>
	private sealed class TestOptionsWrapper(PasswordHashingOptions? value) : IOptions<PasswordHashingOptions>
	{
		public PasswordHashingOptions Value { get; } = value!;
	}
}
