// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Ui.Web.Models;

/// <summary>
/// Represents a collection of translations for a single locale along with metadata about the source file.
/// </summary>
/// <param name="Mappings">The nested dictionary structure containing key-value translation mappings.</param>
/// <param name="SearchedLocation">
/// The file path where these translations were loaded from (e.g., <c>locales/de/translations.json</c>).
/// </param>
/// <remarks>
/// The <see cref="SearchedLocation"/> is stored once per locale rather than duplicated for each translation,
/// making this architecture memory-efficient while still providing debugging information.
/// </remarks>
public sealed record TranslationTable(
	IReadOnlyDictionary<string, object> Mappings,
	string                              SearchedLocation);
