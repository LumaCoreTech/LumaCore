// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace LumaCore.Core;

/// <summary>
/// Provides guard methods that validate and normalize string arguments following the BCL
/// <c>ThrowIf</c> convention. The trimmed result is provided via an <see langword="out"/> parameter
/// so the caller can opt in to the normalized value without a return-based API.
/// </summary>
/// <remarks>
///     <para>
///     These guards combine the null/whitespace check from <see cref="ArgumentException.ThrowIfNullOrWhiteSpace"/>
///     with trimming and maximum-length validation. They are intended for boundary validation in service methods
///     where the same validate-trim-check-length pattern would otherwise repeat across many parameters.
///     </para>
///     <para>
///     <b>Design note:</b> The <see langword="out"/> parameter follows the BCL convention of <c>void</c>-returning
///     guard methods (like <see cref="ArgumentNullException.ThrowIfNull(object?,string?)"/>), while still providing
///     the trimmed value to the caller. Callers that do not need the trimmed value can discard it with <c>out _</c>.
///     </para>
/// </remarks>
public static class Guard
{
	/// <summary>
	/// Validates that <paramref name="value"/> is not <see langword="null"/>, empty, or whitespace-only,
	/// and that its trimmed length does not exceed <paramref name="maxLength"/>.
	/// The trimmed value is provided via <paramref name="trimmed"/>.
	/// </summary>
	/// <param name="value">The string value to validate.</param>
	/// <param name="maxLength">The maximum allowed length after trimming.</param>
	/// <param name="trimmed">When this method returns, contains the trimmed value.</param>
	/// <param name="paramName">
	/// The parameter name for exception messages. Automatically inferred via
	/// <see cref="CallerArgumentExpressionAttribute"/>.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="value"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="maxLength"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="value"/> is empty or whitespace-only, or when the trimmed value exceeds
	/// <paramref name="maxLength"/>.
	/// </exception>
	public static void ThrowIfNullOrEmptyOrTooLong(
		[NotNull] string? value,
		int               maxLength,
		out string        trimmed,
		[CallerArgumentExpression(nameof(value))]
		string? paramName = null)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
		ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

		trimmed = value.Trim();
		if (trimmed.Length > maxLength)
		{
			throw new ArgumentException(
				$"Value must be {maxLength} characters or fewer.",
				paramName);
		}
	}

	/// <summary>
	/// Validates that the trimmed length of <paramref name="value"/> does not exceed
	/// <paramref name="maxLength"/>. When <paramref name="value"/> is <see langword="null"/>,
	/// empty, or whitespace-only, <paramref name="trimmed"/> is set to <see langword="null"/>.
	/// </summary>
	/// <param name="value">The optional string value to normalize.</param>
	/// <param name="maxLength">The maximum allowed length after trimming.</param>
	/// <param name="trimmed">
	/// When this method returns, contains the trimmed value, or <see langword="null"/> if the input
	/// was <see langword="null"/>, empty, or whitespace-only.
	/// </param>
	/// <param name="paramName">
	/// The parameter name for exception messages. Automatically inferred via
	/// <see cref="CallerArgumentExpressionAttribute"/>.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="maxLength"/> is less than or equal to 0.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// The trimmed value exceeds <paramref name="maxLength"/>.
	/// </exception>
	public static void ThrowIfTooLong(
		string?     value,
		int         maxLength,
		out string? trimmed,
		[CallerArgumentExpression(nameof(value))]
		string? paramName = null)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

		if (string.IsNullOrWhiteSpace(value))
		{
			trimmed = null;
			return;
		}

		trimmed = value.Trim();

		if (trimmed.Length > maxLength)
		{
			throw new ArgumentException(
				$"Value must be {maxLength} characters or fewer.",
				paramName);
		}
	}

	/// <summary>
	/// Validates that <paramref name="value"/> is not <see cref="Guid.Empty"/>.
	/// </summary>
	/// <param name="value">The <see cref="Guid"/> value to validate.</param>
	/// <param name="paramName">
	/// The parameter name for exception messages. Automatically inferred via
	/// <see cref="CallerArgumentExpressionAttribute"/>.
	/// </param>
	/// <exception cref="ArgumentException">
	/// <paramref name="value"/> is <see cref="Guid.Empty"/>.
	/// </exception>
	/// <remarks>
	/// Mirrors the BCL <c>ThrowIf</c> convention. Use for public identifiers and other GUIDs where the
	/// all-zero sentinel value is never a valid input.
	/// </remarks>
	public static void ThrowIfEmpty(
		Guid value,
		[CallerArgumentExpression(nameof(value))]
		string? paramName = null)
	{
		if (value == Guid.Empty)
		{
			throw new ArgumentException(
				$"Value must not be {nameof(Guid.Empty)}.",
				paramName);
		}
	}
}
