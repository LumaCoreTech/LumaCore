// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.User;

/// <summary>
/// Response containing the authenticated user's application preferences.
/// </summary>
/// <remarks>
/// The preferences object is schema-flexible: new properties can be added without breaking existing clients.
/// Unknown properties are silently ignored during deserialization.
/// </remarks>
/// <param name="RecentEmojis">
/// The most recently used emojis, ordered from most recent to least recent.
/// <see langword="null"/> when the user has no recent emojis.
/// </param>
public sealed record UserPreferencesResponse(List<string>? RecentEmojis);
