// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Initialization;

/// <summary>
/// Categorizes database initialization failures to determine appropriate recovery behavior.
/// </summary>
/// <remarks>
/// The <see cref="DatabaseConnectionMonitorService"/> uses this category to decide whether automatic retry
/// attempts are likely to succeed or whether manual intervention is required.
/// </remarks>
public enum DatabaseFailureCategory
{
	/// <summary>
	/// A transient error occurred (connection lost, timeout, temporary unavailability).
	/// Automatic retry is likely to succeed once the underlying issue resolves.
	/// </summary>
	/// <remarks>
	/// Examples: network timeout, database server restart, connection pool exhaustion.
	/// The recovery service will continue retrying at the configured interval.
	/// </remarks>
	Transient,

	/// <summary>
	/// The current configuration prevents automatic recovery.
	/// Manual configuration change or manual migration is required.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Examples:
	///     </para>
	///     <list type="bullet">
	///         <item><c>Database:AutoCreate</c> is <see langword="false"/> and database is empty</item>
	///         <item><c>Database:AutoMigration:Enabled</c> is <see langword="false"/> and migrations are pending</item>
	///     </list>
	///     <para>
	///     The recovery service will stop retrying until the application is restarted with corrected configuration
	///     or the database is manually migrated.
	///     </para>
	/// </remarks>
	ConfigurationRequired,

	/// <summary>
	/// The database is in an inconsistent or unknown state that cannot be automatically recovered.
	/// Manual intervention is required (e.g., restore from backup, fix schema manually).
	/// </summary>
	/// <remarks>
	///     <para>
	///     Examples:
	///     </para>
	///     <list type="bullet">
	///         <item>Migration failed and automatic restore also failed</item>
	///         <item>Migration failed and no backup was available</item>
	///         <item>Multiple consecutive initialization failures (likely a code bug)</item>
	///     </list>
	///     <para>
	///     The recovery service will stop retrying. The <see cref="DatabaseInitializationStatus.FailureMessage"/>
	///     provides details for operators and can be displayed in the UI.
	///     </para>
	/// </remarks>
	ManualInterventionRequired
}
