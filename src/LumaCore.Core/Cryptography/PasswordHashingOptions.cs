// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

namespace LumaCore.Core.Cryptography;

/// <summary>
/// Configuration options for <see cref="Pbkdf2PasswordHasher"/>.
/// </summary>
/// <remarks>
///     <para>
///     This type is designed to be bound from a configuration section using the
///     <c>IOptions&lt;T&gt;</c> pattern:
///     </para>
///     <code>
/// services.Configure&lt;PasswordHashingOptions&gt;(configuration.GetSection(PasswordHashingOptions.SectionName));
/// services.AddSingleton&lt;IPasswordHasher, Pbkdf2PasswordHasher&gt;();
///     </code>
///     <para>
///     The default of 600 000 iterations exceeds the OWASP 2023 baseline of 210 000 iterations for
///     PBKDF2-SHA512 by a factor of roughly three. This raises the per-attempt cost for offline cracking
///     while keeping a single login under ~350 ms on a typical server CPU. When future hardware shifts the
///     trade-off, the iteration count can be raised in configuration; existing stored hashes are migrated
///     transparently on next login through <see cref="IPasswordHasher.NeedsRehash"/>.
///     </para>
/// </remarks>
public sealed class PasswordHashingOptions : IValidatableObject
{
	/// <summary>
	/// The default configuration section name for binding from <c>IConfiguration</c>.
	/// </summary>
	public const string SectionName = "PasswordHashing";

	/// <summary>
	/// The default number of PBKDF2 iterations: 600 000.
	/// </summary>
	/// <remarks>
	/// Chosen as roughly three times the OWASP 2023 minimum of 210 000 for PBKDF2-SHA512 to add headroom
	/// against future hardware improvements while keeping a single login under ~350 ms on a typical server
	/// CPU. Tune higher only after measuring the resulting login latency on production-grade hardware.
	/// </remarks>
	public const int DefaultIterations = 600_000;

	/// <summary>
	/// Hard lower bound on the configured iteration count: 100 000.
	/// </summary>
	/// <remarks>
	/// Below this value the cost per offline guess drops to a level that no longer meaningfully resists
	/// modern GPU-based cracking. The bound is intentionally lower than <see cref="DefaultIterations"/> so
	/// that test suites and CI environments may opt in to a faster (but still defensible) cost via
	/// configuration without bypassing validation.
	/// </remarks>
	private const int MinAllowedIterations = 100_000;

	/// <summary>
	/// Hard upper bound on the configured iteration count: 10 000 000.
	/// </summary>
	/// <remarks>
	/// Acts as a guard against accidental misconfiguration (extra zero, value treated as a memory size, …)
	/// that would turn the login endpoint into a self-inflicted denial-of-service vector. Legitimate
	/// production cost increases will sit several orders of magnitude below this limit.
	/// </remarks>
	private const int MaxAllowedIterations = 10_000_000;

	/// <summary>
	/// Gets or sets the number of PBKDF2 iterations applied when hashing a new password.
	/// </summary>
	/// <value>
	/// The default is <see cref="DefaultIterations"/> (600 000). Must be between 100 000 and 10 000 000
	/// (inclusive).
	/// </value>
	/// <remarks>
	/// This value applies only to <em>new</em> hashes produced by <see cref="IPasswordHasher.Hash"/>.
	/// Existing stored hashes carry their own iteration count and continue to verify correctly; raising this
	/// value causes <see cref="IPasswordHasher.NeedsRehash"/> to flag older hashes for transparent migration
	/// on the next successful login.
	/// </remarks>
	public int Iterations { get; set; } = DefaultIterations;

	/// <summary>
	/// Validates cross-property constraints that cannot be expressed with data annotations alone.
	/// </summary>
	/// <param name="validationContext">The validation context.</param>
	/// <returns>A collection of validation results; empty if validation succeeds.</returns>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (Iterations < MinAllowedIterations)
		{
			yield return new ValidationResult(
				$"{nameof(Iterations)} must be at least {MinAllowedIterations}, but was {Iterations}.",
				[nameof(Iterations)]);
		}
		else if (Iterations > MaxAllowedIterations)
		{
			yield return new ValidationResult(
				$"{nameof(Iterations)} must not exceed {MaxAllowedIterations}, but was {Iterations}.",
				[nameof(Iterations)]);
		}
	}

	/// <summary>
	/// Validates the options and throws a <see cref="ValidationException"/> if any constraint is violated.
	/// </summary>
	/// <exception cref="ValidationException">The options are invalid.</exception>
	public void ThrowIfInvalid()
	{
		((IValidatableObject)this).ThrowIfInvalid();
	}
}
