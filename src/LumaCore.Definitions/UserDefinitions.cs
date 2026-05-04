// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Definitions;

/// <summary>
/// Defines well-known user-related constants shared across layers (data layer, API, UI).
/// </summary>
/// <remarks>
/// Centralizing these values avoids magic strings scattered across the codebase and ensures
/// consistent rendering of placeholder data in conversations, message lists, and admin views.
/// </remarks>
public static class UserDefinitions
{
	/// <summary>
	/// Display name written to the participant record after the underlying user is deleted.
	/// </summary>
	/// <remarks>
	/// The participant row is intentionally retained so conversation participant lists and message
	/// history remain structurally intact; the personal display name is replaced with this neutral
	/// placeholder to remove identifying information.
	/// </remarks>
	public const string DeletedUserDisplayName = "Deleted user";
}
