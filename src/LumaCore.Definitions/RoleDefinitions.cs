// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Definitions;

/// <summary>
/// Defines well-known role names and default role definitions used across layers
/// (API authorization, data seeding, UI policies).
/// </summary>
/// <remarks>
///     <para>
///     Role names are stored in the database and referenced by authorization policies, seed definitions,
///     and claim-based identity checks. Centralizing them here avoids magic strings scattered across layers
///     and ensures consistency when adding or renaming roles.
///     </para>
///     <para>
///     <b>Adding a role:</b> Add a new constant, add an entry to <see cref="Defaults"/>, and wire up any
///     required authorization policies. The database seed reads directly from <see cref="Defaults"/>.
///     </para>
/// </remarks>
public static class RoleDefinitions
{
	/// <summary>
	/// Full system access including user management and configuration.
	/// </summary>
	public static readonly RoleDefinition Admin = new(
		"admin",
		"Full system access including user management and configuration");

	/// <summary>
	/// Standard user access for chat and persona interactions.
	/// </summary>
	public static readonly RoleDefinition User = new("user", "Standard user access for chat and persona interactions");

	/// <summary>
	/// Content moderation capabilities.
	/// </summary>
	public static readonly RoleDefinition Moderator = new("moderator", "Content moderation capabilities");

	/// <summary>
	/// The complete set of default roles, intended for database seeding.
	/// </summary>
	/// <remarks>
	/// <c>LumaCore.Data.Seeding.DefaultRolesSeed</c> reads from this collection so that role
	/// definitions are maintained in a single place.
	/// </remarks>
	public static IReadOnlyList<RoleDefinition> Defaults { get; } =
	[
		Admin,
		User,
		Moderator
	];
}
