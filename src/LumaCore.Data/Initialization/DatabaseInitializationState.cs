// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Initialization;

/// <summary>
/// Represents the possible states of database availability.
/// </summary>
public enum DatabaseInitializationState
{
	/// <summary>
	/// Initialization has not yet started. This is the initial state before <see cref="DatabaseInitializer.StartAsync"/>
	/// is called.
	/// </summary>
	NotStarted,

	/// <summary>
	/// Initialization or recovery is currently in progress.
	/// </summary>
	InProgress,

	/// <summary>
	/// The database is fully operational and ready to accept requests.
	/// </summary>
	Completed,

	/// <summary>
	/// Initialization failed during startup. The database may not be in a consistent state. The background recovery
	/// service will attempt to complete initialization.
	/// </summary>
	Failed,

	/// <summary>
	/// The database connection was lost during runtime. The background recovery service will attempt to restore the
	/// connection. Once restored, the state transitions back to <see cref="Completed"/>.
	/// </summary>
	Disconnected
}
