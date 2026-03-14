// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Services;

/// <summary>
/// Defines maintenance operations for the application's database, such as creating portable backups.
/// </summary>
public interface IDatabaseMaintenanceService
{
	/// <summary>
	/// Creates a portable LumaCore Shuttle backup of the configured database.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The full path to the created backup file.</returns>
	Task<string> CreateShuttleBackupAsync(CancellationToken cancellationToken = default);
}
