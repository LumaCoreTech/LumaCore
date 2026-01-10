// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Ui.Web.Models;

namespace LumaCore.Ui.Web.Services;

partial class TranslationRepository
{
	/// <summary>
	/// Immutable snapshot of repository state.
	/// </summary>
	/// <param name="AllTranslations">Dictionary mapping locale codes to translation tables.</param>
	/// <param name="AvailableLocales">The list of available locales from the manifest.</param>
	private sealed record RepositoryState(
		IReadOnlyDictionary<string, TranslationTable> AllTranslations,
		IReadOnlyList<LocaleInfo>                     AvailableLocales);
}
