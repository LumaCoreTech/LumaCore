// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.User;

/// <summary>
/// Request body for updating the authenticated user's application preferences.
/// </summary>
/// <remarks>
/// The client sends the full preferences object on each update (read-modify-write pattern).
/// Properties set to <see langword="null"/> are treated as "use default / clear".
/// </remarks>
/// <param name="RecentEmojis">
/// The most recently used emojis, ordered from most recent to least recent.
/// <see langword="null"/> to clear the recent emojis list.
/// </param>
public sealed record UpdateUserPreferencesRequest(List<string>? RecentEmojis);
