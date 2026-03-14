// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;
using System.Net.Sockets;

using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.Initialization;

using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Providers;

/// <summary>
/// Abstracts all database-provider-specific operations into a single interface.
/// </summary>
/// <remarks>
///     <para>
///     This interface centralizes provider-specific logic. Each supported database provider has a single
///     implementation that encapsulates all of its quirks (SQL dialect, system catalog queries, DDL syntax, etc.).
///     </para>
///     <para>
///     To add support for a new database provider, create a new implementation of this interface and
///     register it in <see cref="DatabaseProviderFactory"/>.
///     </para>
/// </remarks>
public interface IDatabaseProviderOperations
{
	/// <summary>
	/// Gets the provider identifier.
	/// </summary>
	/// <remarks>
	/// Must match one of the constants in <see cref="DatabaseProviders"/>.
	/// </remarks>
	string ProviderName { get; }

	/// <summary>
	/// Quotes an identifier (table or column name) for safe use in a SQL query targeting this database provider.
	/// </summary>
	/// <param name="identifier">The identifier to quote.</param>
	/// <returns>The quoted identifier with provider-specific special characters escaped.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="identifier"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Each provider uses its own quoting syntax: double-quotes for SQLite and PostgreSQL, square brackets
	/// for SQL Server, and backticks for MySQL. Any embedded special characters are escaped by doubling them.
	/// </remarks>
	string QuoteIdentifier(string identifier);

	/// <summary>
	/// Drops all user-defined schema objects (tables, views, FKs, sequences, etc.) while preserving
	/// the specified tables.
	/// </summary>
	/// <param name="dbContext">The database context for executing raw SQL.</param>
	/// <param name="tablesToPreserve">
	/// Table names that must survive the drop (e.g., the restore checkpoint table).
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <param name="logger">Optional logger for non-fatal diagnostic output (e.g., VACUUM warnings).</param>
	/// <remarks>
	/// Implementations that modify session-level settings (e.g., foreign key enforcement) must restore
	/// them unconditionally via <c>try/finally</c>, using <see cref="CancellationToken.None"/> for the
	/// restore command to prevent connection pool pollution when the original token is cancelled.
	/// </remarks>
	Task DropSchemaObjectsAsync(
		LumaCoreDbContext    dbContext,
		IReadOnlySet<string> tablesToPreserve,
		CancellationToken    cancellationToken,
		ILogger?             logger = null);

	/// <summary>
	/// Checks whether a table with the given name exists in the database.
	/// </summary>
	/// <param name="connection">
	/// The database connection. Opened automatically if not already open.
	/// </param>
	/// <param name="tableName">The table name to check.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <param name="schema">Optional schema name (for providers that support schemas like PostgreSQL/SQL Server).</param>
	/// <returns><see langword="true"/> if the table exists; otherwise, <see langword="false"/>.</returns>
	Task<bool> TableExistsAsync(
		DbConnection      connection,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null);

	/// <summary>
	/// Reads the restore checkpoint data from the checkpoint table, if it exists.
	/// </summary>
	/// <param name="connection">
	/// The database connection. Opened automatically if not already open.
	/// </param>
	/// <param name="tableName">The name of the checkpoint table.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <param name="schema">Optional schema name (for providers that support schemas like PostgreSQL/SQL Server).</param>
	/// <returns>
	/// The checkpoint data if the table exists and contains a row; otherwise, <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// Checks whether the table exists via <see cref="TableExistsAsync"/>, then reads the single checkpoint
	/// row if present. Safe to call even when the database schema is in an inconsistent state.
	/// </remarks>
	Task<RestoreCheckpointData?> ReadCheckpointAsync(
		DbConnection      connection,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null);

	/// <summary>
	/// Creates the restore checkpoint table (idempotent) and writes the initial checkpoint row.
	/// </summary>
	/// <param name="dbContext">The database context for executing provider-specific statements.</param>
	/// <param name="tableName">The name of the checkpoint table.</param>
	/// <param name="shuttleId">The unique identity of the Shuttle backup file being restored.</param>
	/// <param name="baselineMigrationId">The target migration ID from the backup's migration history.</param>
	/// <param name="startedUtc">The ISO 8601 timestamp when the restore was started.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <param name="schema">Optional schema name (for providers that support schemas like PostgreSQL/SQL Server).</param>
	/// <remarks>
	/// Any existing rows are cleared before inserting (idempotent for retry scenarios). The initial phase is
	/// set to <c>"schema_cleanup"</c>.
	/// </remarks>
	Task WriteCheckpointAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		string            shuttleId,
		string            baselineMigrationId,
		string            startedUtc,
		CancellationToken cancellationToken,
		string?           schema = null);

	/// <summary>
	/// Updates the phase column of the restore checkpoint to track progress across restarts.
	/// </summary>
	/// <param name="dbContext">The database context for executing provider-specific statements.</param>
	/// <param name="tableName">The name of the checkpoint table.</param>
	/// <param name="phase">The new phase value (e.g., <c>"migration"</c> or <c>"import"</c>).</param>
	/// <param name="updatedUtc">The ISO 8601 timestamp of the phase transition.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <param name="schema">Optional schema name (for providers that support schemas like PostgreSQL/SQL Server).</param>
	Task UpdateCheckpointPhaseAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		string            phase,
		string            updatedUtc,
		CancellationToken cancellationToken,
		string?           schema = null);

	/// <summary>
	/// Drops the restore checkpoint table if it exists.
	/// </summary>
	/// <param name="dbContext">The database context for executing provider-specific statements.</param>
	/// <param name="tableName">The name of the checkpoint table.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <param name="schema">Optional schema name (for providers that support schemas like PostgreSQL/SQL Server).</param>
	/// <remarks>
	/// This operation is idempotent — calling it when the table does not exist is a no-op.
	/// </remarks>
	Task DropCheckpointTableAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null);

	/// <summary>
	/// Creates a data export reader for Shuttle backup creation.
	/// </summary>
	/// <param name="options">The database options containing the connection string and provider-specific settings.</param>
	/// <param name="logger">A logger instance for providers that require diagnostic output.</param>
	/// <returns>A new <see cref="IDataExportReader"/> instance.</returns>
	IDataExportReader CreateExportReader(DatabaseOptions options, ILogger logger);

	/// <summary>
	/// Creates a data import writer for Shuttle backup restoration.
	/// </summary>
	/// <param name="connectionString">The connection string for the target database.</param>
	/// <param name="logger">A logger instance for diagnostic output.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <returns>A new <see cref="IDataImportWriter"/> instance.</returns>
	IDataImportWriter CreateImportWriter(string connectionString, ILogger logger, TimeProvider timeProvider);

	/// <summary>
	/// Maps a provider-specific database type name to the corresponding SQLite storage type for the
	/// LumaCore Shuttle format.
	/// </summary>
	/// <param name="providerDbType">
	/// The type name as reported by this provider (e.g., <c>timestamp with time zone</c> for PostgreSQL,
	/// <c>nvarchar</c> for SQL Server, <c>TEXT</c> for SQLite).
	/// </param>
	/// <returns>
	/// A SQLite storage type: <c>TEXT</c>, <c>INTEGER</c>, <c>REAL</c>, <c>NUMERIC</c>, or <c>BLOB</c>.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="providerDbType"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Each provider implementation maps only its own type system. Unknown types should fall back to
	/// <c>TEXT</c>, which preserves the string representation of any value.
	/// </remarks>
	string MapToShuttleStorageType(string providerDbType);

	/// <summary>
	/// Determines whether the specified exception indicates a service-unavailable condition for this database provider.
	/// </summary>
	/// <param name="exception">The exception to inspect. Must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> if the exception (or any exception in its tree) indicates a connection failure,
	/// resource exhaustion, or other condition that makes the database unable to serve requests;
	/// <see langword="false"/> if the exception represents a non-infrastructure error (e.g., SQL syntax error,
	/// constraint violation) or a user-initiated cancellation.
	/// </returns>
	/// <remarks>
	///     <para>
	///     Implementations must perform a depth-first traversal of the full exception tree, including
	///     <see cref="AggregateException.InnerExceptions"/> (multiple children) and regular
	///     <see cref="Exception.InnerException"/> (single child), to detect connection-related errors
	///     regardless of how deeply they are nested.
	///     </para>
	///     <para>
	///     The check must include both provider-specific error codes and generic indicators such as
	///     <see cref="TimeoutException"/>, <see cref="SocketException"/>, and <see cref="EndOfStreamException"/>.
	///     </para>
	///     <para>
	///     <see cref="OperationCanceledException"/> nodes are skipped during traversal. They do not
	///     contribute to a <see langword="true"/> result, but they must not short-circuit the traversal
	///     either: an <see cref="AggregateException"/> can contain both a cancelled task and a genuine
	///     infrastructure error (e.g., <see cref="SocketException"/>). If any infrastructure indicator
	///     is found alongside cancellation, the method must return <see langword="true"/>. Only when the
	///     entire tree contains nothing but cancellation or non-infrastructure exceptions does the method
	///     return <see langword="false"/>.
	///     </para>
	/// </remarks>
	bool IsServiceUnavailable(Exception exception);
}
