// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Seeding;

namespace LumaCore.Data.Entities;

/// <summary>
/// Tracks which seed operations have been applied to the database and their versions.
/// </summary>
/// <remarks>
/// This entity enables idempotent seeding by recording which seeds have been executed and preventing
/// duplicate runs. It also supports seed versioning, allowing seed logic to evolve over time.
/// </remarks>
public sealed class SeedHistoryEntity
{
	/// <summary>
	/// Gets or sets the primary key.
	/// </summary>
	public int Id { get; set; }

	/// <summary>
	/// Gets or sets the unique identifier for the seed operation.
	/// </summary>
	/// <remarks>
	/// This corresponds to <see cref="ISeedDefinition.SeedId"/>.
	/// </remarks>
	public string SeedId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the version of the seed that was applied.
	/// </summary>
	/// <remarks>
	/// This corresponds to <see cref="ISeedDefinition.Version"/>.
	/// </remarks>
	public int Version { get; set; }

	/// <summary>
	/// Gets or sets the description of the seed operation.
	/// </summary>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the UTC timestamp when the seed was applied.
	/// </summary>
	public DateTime AppliedAtUtc { get; set; }
}
