// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Entities;

/// <summary>
/// Defines the visibility scope of a persona.
/// </summary>
/// <remarks>
/// The value is persisted in the database as an integer column. Application logic uses this to determine which
/// personas are visible to which users: <see cref="Private"/> personas are only visible to their creator, while
/// <see cref="Shared"/> personas are discoverable by all authenticated users.
/// </remarks>
public enum PersonaVisibility
{
	/// <summary>
	/// The persona is visible only to its creator.
	/// </summary>
	Private = 0,

	/// <summary>
	/// The persona is visible to all authenticated users.
	/// </summary>
	/// <remarks>
	/// Shared personas can be cloned by other users to create a private copy with independent configuration.
	/// </remarks>
	Shared = 1
}
