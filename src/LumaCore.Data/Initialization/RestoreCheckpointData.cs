// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Initialization;

/// <summary>
/// Holds the data read from the restore checkpoint table.
/// </summary>
/// <param name="ShuttleId">The unique identity of the Shuttle backup file (a GUID written during export finalization).</param>
/// <param name="BaselineMigrationId">The target migration ID to restore to.</param>
/// <param name="Phase">
/// The current restore phase: <see cref="PhaseSchemaCleanup"/>, <see cref="PhaseMigration"/>, or
/// <see cref="PhaseImport"/>.
/// </param>
/// <param name="StartedUtc">The ISO 8601 timestamp when the restore was originally started.</param>
public sealed record RestoreCheckpointData(
	string ShuttleId,
	string BaselineMigrationId,
	string Phase,
	string StartedUtc)
{
	/// <summary>
	/// Phase value indicating that the schema cleanup (drop all tables) has not yet completed.
	/// </summary>
	public const string PhaseSchemaCleanup = "schema_cleanup";

	/// <summary>
	/// Phase value indicating that the baseline migration has not yet been re-applied.
	/// </summary>
	public const string PhaseMigration = "migration";

	/// <summary>
	/// Phase value indicating that the data import has not yet completed.
	/// </summary>
	public const string PhaseImport = "import";
}
