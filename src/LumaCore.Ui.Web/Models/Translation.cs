// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Ui.Web.Models;

/// <summary>
/// Represents the result of a translation lookup with metadata about where the translation was searched.
/// </summary>
/// <param name="Value">The translated string value, or <see langword="null"/> if not found.</param>
/// <param name="SearchedLocation">
/// The file path where the translation was searched (e.g.,
/// <c>locales/en/translations.json</c>).
/// </param>
/// <remarks>
/// This struct supports implicit conversion to <see langword="string"/> for convenient usage.
/// </remarks>
public readonly record struct Translation(string? Value, string SearchedLocation)
{
	/// <summary>
	/// Implicitly converts a <see cref="Translation"/> to its string value.
	/// </summary>
	/// <param name="translation">The translation to convert.</param>
	/// <returns>The translated string value, or <see langword="null"/> if not found.</returns>
	public static implicit operator string?(Translation translation) => translation.Value;
}
