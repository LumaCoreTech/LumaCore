// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Data.Common;
using System.Net.Sockets;

using LumaCore.Core.Diagnostics;
using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Export.Implementations;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.DataPort.Import.Implementations;
using LumaCore.Data.Initialization;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Providers;

/// <summary>
/// SQLite implementation of <see cref="IDatabaseProviderOperations"/>.
/// </summary>
public sealed class SqliteProviderOperations : IDatabaseProviderOperations
{
	/// <inheritdoc/>
	public string ProviderName => DatabaseProviders.Sqlite;

	/// <inheritdoc/>
	/// <remarks>
	/// SQLite uses double-quotes for identifier quoting. Any embedded double-quotes are escaped by doubling them.
	/// </remarks>
	public string QuoteIdentifier(string identifier) => SqlIdentifierHelper.QuoteSqlite(identifier);

	/// <inheritdoc/>
	/// <remarks>
	/// SQLite is a lightweight, file-based database. Schema cleanup drops objects individually (triggers,
	/// views, tables) rather than deleting the database file, so the restore checkpoint table can survive.
	/// A <c>VACUUM</c> is executed afterwards to reclaim disk space.
	/// </remarks>
	public async Task DropSchemaObjectsAsync(
		LumaCoreDbContext    dbContext,
		IReadOnlySet<string> tablesToPreserve,
		CancellationToken    cancellationToken,
		ILogger?             logger = null)
	{
		logger?.LogInformation("Starting schema cleanup for provider {ProviderName}...", ProviderName);

		// SQLite does not support dynamic SQL (EXECUTE), so we must query the catalog first,
		// then build the script in C# and send it as a single batch.
		DbConnection connection = dbContext.Database.GetDbConnection();
		if (connection.State != ConnectionState.Open)
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// SQLite treats table names case-insensitively; normalize the preserve set accordingly.
		var preserve = new HashSet<string>(tablesToPreserve, StringComparer.OrdinalIgnoreCase);

		try
		{
			// Disable FK enforcement for the session to avoid constraint errors when dropping
			// tables. Inside the try block so that the finally always restores enforcement —
			// even if cancellation fires between this await and the drops.
			await ExecuteNonQueryAsync(
					connection,
					"PRAGMA foreign_keys = OFF",
					cancellationToken)
				.ConfigureAwait(false);

			var dropStatements = new List<string>();

			// Collect triggers (must be dropped before the tables they reference).
			await CollectObjectNamesAsync(
					connection,
					"SELECT name FROM sqlite_master WHERE type = 'trigger' AND name NOT LIKE 'sqlite_%'",
					name => dropStatements.Add($"DROP TRIGGER IF EXISTS {QuoteIdentifier(name)};"),
					cancellationToken)
				.ConfigureAwait(false);

			// Collect views.
			await CollectObjectNamesAsync(
					connection,
					"SELECT name FROM sqlite_master WHERE type = 'view'",
					name => dropStatements.Add($"DROP VIEW IF EXISTS {QuoteIdentifier(name)};"),
					cancellationToken)
				.ConfigureAwait(false);

			// Collect tables (excluding preserved tables and internal SQLite tables).
			await CollectObjectNamesAsync(
					connection,
					"SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'",
					name =>
					{
						if (!preserve.Contains(name))
							dropStatements.Add($"DROP TABLE IF EXISTS {QuoteIdentifier(name)};");
					},
					cancellationToken)
				.ConfigureAwait(false);

			// Clean up sqlite_sequence for dropped tables.
			// This prevents auto-increment values from persisting or being invalid when tables are recreated.
			// We keep entries only for preserved tables.
			if (await TableExistsAsync(connection, "sqlite_sequence", cancellationToken).ConfigureAwait(false))
			{
				if (preserve.Count > 0)
				{
					// SQL string comparison is case-sensitive by default. Since we matched table names
					// case-insensitively above (OrdinalIgnoreCase), we must force a case-insensitive
					// check here to ensure the sequence entry is found even if casing differs.
					string sequencePreserveList = string.Join(
						", ",
						preserve.Select(t => $"'{t.Replace("'", "''")}'"));
					dropStatements.Add(
						$"DELETE FROM sqlite_sequence WHERE name COLLATE NOCASE NOT IN ({sequencePreserveList});");
				}
				else
				{
					dropStatements.Add("DELETE FROM sqlite_sequence;");
				}
			}

			if (dropStatements.Count > 0)
			{
				// Execute all DROP statements as a single batch.
				string batchSql = string.Join('\n', dropStatements);
				await dbContext
					.Database
					.ExecuteSqlRawAsync(batchSql, cancellationToken)
					.ConfigureAwait(false);
			}
		}
		finally
		{
			// Always restore FK enforcement, even if drops failed, to prevent session pollution
			// if the connection is returned to the pool. CancellationToken.None is intentional:
			// if the original token is already cancelled, we must still send this command to
			// avoid returning a connection with foreign keys disabled to the pool.
			await ExecuteNonQueryAsync(
					connection,
					"PRAGMA foreign_keys = ON",
					CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Reclaim disk space. VACUUM rewrites the entire database file, leaving it nearly as compact
		// as a freshly created file. This must be executed outside any transaction.
		// VACUUM failure is non-fatal: the schema cleanup succeeded, and the database is fully usable
		// even with unreleased pages. VACUUM can fail e.g. when disk space is low (it writes a copy).
		try
		{
			ExecutionStageMonitor.ReportStage("SqliteProviderOperations.BeforeVacuum");
			await dbContext
				.Database
				.ExecuteSqlRawAsync("VACUUM;", cancellationToken)
				.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger?.LogWarning(
				ex,
				"VACUUM failed after schema cleanup. The database is usable but may contain unreleased disk space");
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// SQLite has no schema concept — all tables reside in the single attached database file. The
	/// <paramref name="schema"/> parameter is ignored. Table existence is checked against
	/// <c>sqlite_master</c>, SQLite's built-in catalog table. The table name is sent as a query
	/// parameter to prevent SQL injection.
	/// </remarks>
	public async Task<bool> TableExistsAsync(
		DbConnection      connection,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// Prevent crash if called with a closed connection (e.g., from a pool or stale context).
		// ExecuteScalarAsync requires an open connection.
		if (connection.State != ConnectionState.Open)
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		DbCommand cmd = connection.CreateCommand();
		try
		{
			// sqlite_master is SQLite's built-in catalog table that lists all user-created objects.
			// Unlike server-based databases, there is no schema qualifier — just the object name.
			cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @tableName";
			DbParameter param = cmd.CreateParameter();
			param.ParameterName = "@tableName";
			param.Value = tableName;
			cmd.Parameters.Add(param);

			object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			return result is not null && result != DBNull.Value;
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// SQLite has no schema concept, so the <paramref name="schema"/> parameter is ignored. The
	/// checkpoint table is accessed by its unqualified name.
	/// </remarks>
	public async Task<RestoreCheckpointData?> ReadCheckpointAsync(
		DbConnection      connection,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// SQLite ignores schema parameter — all tables live in a single flat namespace.
		// Prevent crash if called with a closed connection (e.g., from a pool or stale context).
		if (connection.State != ConnectionState.Open)
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		bool exists = await TableExistsAsync(connection, tableName, cancellationToken).ConfigureAwait(false);
		if (!exists)
			return null;

		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText =
				$"""
				 SELECT {QuoteIdentifier("shuttle_id")},
				   {QuoteIdentifier("baseline_migration_id")},
				   {QuoteIdentifier("phase")},
				   {QuoteIdentifier("started_utc")}
				 FROM {QuoteIdentifier(tableName)}
				 """;

			DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
					return null;

				return new RestoreCheckpointData(
					ShuttleId: reader.GetString(0),
					BaselineMigrationId: reader.GetString(1),
					Phase: reader.GetString(2),
					StartedUtc: reader.GetString(3));
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// SQLite ignores the <paramref name="schema"/> parameter. The table is created with <c>TEXT</c>
	/// columns via <c>CREATE TABLE IF NOT EXISTS</c>.
	/// </remarks>
	public async Task WriteCheckpointAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		string            shuttleId,
		string            baselineMigrationId,
		string            startedUtc,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// Step 1: Create table (idempotent). SQLite ignores schema.
		string qt = QuoteIdentifier(tableName);
		string createSql =
			$"""
			 CREATE TABLE IF NOT EXISTS {qt} (
			   {QuoteIdentifier("shuttle_id")}            TEXT NOT NULL,
			   {QuoteIdentifier("baseline_migration_id")} TEXT NOT NULL,
			   {QuoteIdentifier("phase")}                 TEXT NOT NULL,
			   {QuoteIdentifier("started_utc")}           TEXT NOT NULL,
			   {QuoteIdentifier("updated_utc")}           TEXT NOT NULL
			 )
			 """;
		await dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken).ConfigureAwait(false);

		// Step 2: Clear stale rows (idempotency for retry scenarios).
		string deleteSql = $"DELETE FROM {qt}";
		await dbContext.Database.ExecuteSqlRawAsync(deleteSql, cancellationToken).ConfigureAwait(false);

		// Step 3: Insert new checkpoint with initial phase 'schema_cleanup'.
		string insertSql =
			$$"""
			  INSERT INTO {{qt}} (
			    {{QuoteIdentifier("shuttle_id")}},
			    {{QuoteIdentifier("baseline_migration_id")}},
			    {{QuoteIdentifier("phase")}},
			    {{QuoteIdentifier("started_utc")}},
			    {{QuoteIdentifier("updated_utc")}})
			  VALUES ({0}, {1}, 'schema_cleanup', {2}, {3})
			  """;
		await dbContext
			.Database
			.ExecuteSqlRawAsync(insertSql, [shuttleId, baselineMigrationId, startedUtc, startedUtc], cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// SQLite has no schema concept, so the <paramref name="schema"/> parameter is ignored.
	/// </remarks>
	public async Task UpdateCheckpointPhaseAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		string            phase,
		string            updatedUtc,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// SQLite ignores schema parameter — all tables live in a single flat namespace.
		string sql =
			$$"""
			  UPDATE {{QuoteIdentifier(tableName)}}
			  SET {{QuoteIdentifier("phase")}} = {0}, {{QuoteIdentifier("updated_utc")}} = {1}
			  """;
		await dbContext
			.Database
			.ExecuteSqlRawAsync(sql, [phase, updatedUtc], cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// SQLite has no schema concept, so the <paramref name="schema"/> parameter is ignored.
	/// </remarks>
	public async Task DropCheckpointTableAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// SQLite ignores schema parameter — all tables live in a single flat namespace.
		string sql = $"DROP TABLE IF EXISTS {QuoteIdentifier(tableName)}";
		await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public IDataExportReader CreateExportReader(DatabaseOptions options, ILogger logger) =>
		new SqliteExportReader(options.ConnectionString, MapToShuttleStorageType);

	/// <inheritdoc/>
	public IDataImportWriter CreateImportWriter(string connectionString, ILogger logger, TimeProvider timeProvider) =>
		new SqliteImportWriter(connectionString, timeProvider, logger);

	/// <inheritdoc/>
	/// <remarks>
	/// SQLite types are already valid shuttle storage types. This method returns the input unchanged.
	/// </remarks>
	public string MapToShuttleStorageType(string providerDbType) => providerDbType;

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     SQLite is a file-based database, so "service unavailable" conditions are different from
	///     client-server databases. This method checks for errors that prevent database access:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <b>File Access:</b> Cannot open database file, permission denied, read-only filesystem
	///             </description>
	///         </item>
	///         <item>
	///             <description><b>Locking:</b> Database/table locked, busy</description>
	///         </item>
	///         <item>
	///             <description><b>Resource Exhaustion:</b> Disk full, I/O error, out of memory</description>
	///         </item>
	///         <item>
	///             <description><b>Corruption:</b> Database disk image is malformed</description>
	///         </item>
	///     </list>
	///     <para>
	///     Generic checks include <see cref="TimeoutException"/>, <see cref="SocketException"/>, and
	///     <see cref="EndOfStreamException"/>.
	///     </para>
	///     <para>
	///     Reference: <see href="https://www.sqlite.org/rescode.html"/>
	///     </para>
	/// </remarks>
	public bool IsServiceUnavailable(Exception exception)
	{
		// Depth-first traversal of the exception tree. Uses a stack to handle both linear
		// InnerException chains and AggregateException trees (multiple InnerExceptions).
		Stack<Exception> stack = new();
		stack.Push(exception);

		while (stack.Count > 0)
		{
			Exception current = stack.Pop();

			// User-initiated cancellations (e.g., navigation away, request abort) are not infrastructure
			// failures. Skip them but keep traversing: an AggregateException can contain both a cancelled
			// task and a genuine infrastructure error (e.g., SocketException). Returning early here would
			// mask the real failure.
			if (current is OperationCanceledException)
				continue;

			// SQLite: Check SqliteException.SqliteErrorCode for connection-related error codes.
			if (current is SqliteException sqliteEx && IsSqliteConnectionError(sqliteEx.SqliteErrorCode))
				return true;

			// Generic: TimeoutException indicates connection timeout.
			if (current is TimeoutException)
				return true;

			// Generic: SocketException indicates network-level failures (connection refused,
			// host unreachable, network down, etc.). While SQLite is file-based, this covers
			// edge cases with network-attached storage or WAL-mode over NFS.
			if (current is SocketException)
				return true;

			// Generic: EndOfStreamException during a database operation indicates an unexpected
			// stream closure, which can occur with corrupted or truncated database files.
			if (current is EndOfStreamException)
				return true;

			// Navigate the exception tree: AggregateException has multiple children,
			// regular exceptions have a single InnerException.
			if (current is AggregateException aggregate)
			{
				foreach (Exception inner in aggregate.InnerExceptions)
				{
					stack.Push(inner);
				}
			}
			else if (current.InnerException is not null)
			{
				stack.Push(current.InnerException);
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether the SQLite error code indicates a connection-related failure.
	/// </summary>
	/// <param name="errorCode">The <see cref="SqliteException.SqliteErrorCode"/> value.</param>
	/// <returns>
	/// <see langword="true"/> if the error indicates the database service is unavailable;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	private static bool IsSqliteConnectionError(int errorCode)
	{
		// SQLite uses primary result codes (lower 8 bits) and extended result codes.
		// We check the primary result code by masking with 0xFF.
		int primaryCode = errorCode & 0xFF;

		// @formatter:off
		return primaryCode switch
		{
			3  => true, // SQLITE_PERM: Access permission denied (OS-level file permission failure)
			5  => true, // SQLITE_BUSY: Database file is locked (another process has the lock)
			6  => true, // SQLITE_LOCKED: A table in the database is locked (same connection deadlock)
			7  => true, // SQLITE_NOMEM: Memory allocation failed
			8  => true, // SQLITE_READONLY: Attempt to write to a read-only database (e.g., filesystem remount)
			10 => true, // SQLITE_IOERR: Disk I/O error (base code, many extended variants)
			11 => true, // SQLITE_CORRUPT: The database disk image is malformed
			13 => true, // SQLITE_FULL: Database or disk is full (insertion failed)
			14 => true, // SQLITE_CANTOPEN: Unable to open the database file
			15 => true, // SQLITE_PROTOCOL: Database lock protocol error
			26 => true, // SQLITE_NOTADB: File opened that is not a database file (header mismatch)

			var _ => false
		};
		// @formatter:on
	}

	/// <summary>
	/// Queries <c>sqlite_master</c> for object names and invokes the callback for each result.
	/// </summary>
	/// <param name="connection">The open database connection.</param>
	/// <param name="sql">A query returning a single column of object names (index 0).</param>
	/// <param name="onName">Callback invoked for each object name returned by the query.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	private static async Task CollectObjectNamesAsync(
		DbConnection      connection,
		string            sql,
		Action<string>    onName,
		CancellationToken cancellationToken)
	{
		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = sql;
			DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					onName(reader.GetString(0));
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Executes a non-query SQL command on the given connection with proper disposal.
	/// </summary>
	/// <param name="connection">The open database connection.</param>
	/// <param name="sql">The SQL command text to execute.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	private static async Task ExecuteNonQueryAsync(
		DbConnection      connection,
		string            sql,
		CancellationToken cancellationToken)
	{
		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = sql;
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}
}
