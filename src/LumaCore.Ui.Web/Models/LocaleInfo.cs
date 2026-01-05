// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Ui.Web.Models;

/// <summary>
/// Represents information about an available locale.
/// </summary>
/// <param name="Code">The locale code (e.g., <c>en</c>, <c>de</c>).</param>
/// <param name="NativeName">The native name of the language (e.g., <c>English</c>, <c>Deutsch</c>).</param>
public sealed record LocaleInfo(string Code, string NativeName);
