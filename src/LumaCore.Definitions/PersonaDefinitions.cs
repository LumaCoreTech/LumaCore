// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Definitions;

/// <summary>
/// Defines well-known persona-related constants shared across layers (data layer, API, UI).
/// </summary>
/// <remarks>
/// Centralizing these values avoids magic strings scattered across the codebase. Persona avatars
/// are stored through the resource system, which requires an original file name even though the
/// physical content is keyed by a generated storage path.
/// </remarks>
public static class PersonaDefinitions
{
	/// <summary>
	/// Synthetic original file name recorded on persona avatar resource references.
	/// </summary>
	/// <remarks>
	/// Persona avatars are uploaded via the resource service which requires an original file name on
	/// every reference. Avatars are not user-provided files (the client resizes raw uploads into a
	/// canonical PNG-like blob), so a fixed placeholder is used instead of preserving an upload name.
	/// </remarks>
	public const string AvatarOriginalFileName = "avatar";
}
