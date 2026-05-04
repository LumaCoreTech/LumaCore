// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Data.Common;
using System.Net.Sockets;
using System.Reflection;

using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.Initialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// TODO: Re-enable when Pomelo releases EF Core 10 compatible version.
// Track: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues
// using MySqlConnector;

namespace LumaCore.Data.Providers;

/// <summary>
/// MySQL/MariaDB implementation of <see cref="IDatabaseProviderOperations"/>.
/// </summary>
public sealed class MySqlProviderOperations : IDatabaseProviderOperations
{
	/// <summary>
	/// Message used by <see cref="NotSupportedException"/> for DataPort operations until Pomelo releases
	/// an EF Core 10 compatible provider.
	/// </summary>
	private const string DataPortNotSupportedMessage =
		"MySQL DataPort (data export/import) is not yet available. Pomelo.EntityFrameworkCore.MySql has " +
		"not released an EF Core 10 compatible version. Track progress at: " +
		"https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues";

	/// <inheritdoc/>
	public string ProviderName => DatabaseProviders.MySql;

	/// <inheritdoc/>
	/// <remarks>
	/// MySQL uses backticks for identifier quoting. Any embedded backticks are escaped by doubling them.
	/// </remarks>
	public string QuoteIdentifier(string identifier) => SqlIdentifierHelper.QuoteMySql(identifier);

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     MySQL/MariaDB do not support <c>DROP SCHEMA CASCADE</c> (without dropping the schema itself),
	///     so all objects must be dropped individually. This implementation queries
	///     <c>INFORMATION_SCHEMA</c> from C# and builds DROP statements directly, avoiding
	///     the <c>GROUP_CONCAT</c>/<c>PREPARE</c> pattern which does not support multi-statement execution.
	///     </para>
	///     <para>
	///     <c>FOREIGN_KEY_CHECKS</c> is disabled during the operation (and restored via <c>try/finally</c>)
	///     so that tables can be dropped in any order without FK constraint errors. This also eliminates the
	///     need for an explicit FK drop step.
	///     </para>
	/// </remarks>
	public async Task DropSchemaObjectsAsync(
		LumaCoreDbContext    dbContext,
		IReadOnlySet<string> tablesToPreserve,
		CancellationToken    cancellationToken,
		ILogger?             logger = null)
	{
		logger?.LogInformation("Starting schema cleanup for provider {ProviderName}...", ProviderName);

		// Use the raw connection for all operations to ensure session-level settings
		// (FOREIGN_KEY_CHECKS) persist across all commands.
		DbConnection connection = dbContext.Database.GetDbConnection();
		if (connection.State != ConnectionState.Open)
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// MySQL table name case sensitivity depends on the file system and lower_case_table_names
		// setting. Normalize the preserve set to case-insensitive to avoid mismatches.
		var preserve = new HashSet<string>(tablesToPreserve, StringComparer.OrdinalIgnoreCase);

		try
		{
			// Disable FK checks for the session to avoid constraint errors when dropping tables.
			// Inside the try block so that the finally always restores FK checks — even if
			// cancellation fires between this await and the drops.
			await ExecuteNonQueryAsync(
					connection,
					"SET FOREIGN_KEY_CHECKS = 0",
					cancellationToken)
				.ConfigureAwait(false);

			// 1. Drop views (before tables, in case of CHECK constraint dependencies).
			//    MySQL supports multi-object DROP VIEW: DROP VIEW IF EXISTS v1, v2, v3
			List<string> views = await CollectQualifiedNamesAsync(
					                     connection,
					                     """
					                     SELECT TABLE_SCHEMA, TABLE_NAME
					                     FROM INFORMATION_SCHEMA.VIEWS
					                     WHERE TABLE_SCHEMA = DATABASE()
					                     """,
					                     preserveNames: null,
					                     cancellationToken)
				                     .ConfigureAwait(false);

			if (views.Count > 0)
			{
				await ExecuteNonQueryAsync(
						connection,
						"DROP VIEW IF EXISTS " + string.Join(", ", views),
						cancellationToken)
					.ConfigureAwait(false);
			}

			// 2. Drop tables (excluding preserved). FK checks are off, so no constraint errors.
			//    MySQL supports multi-object DROP TABLE: DROP TABLE IF EXISTS t1, t2, t3
			List<string> tables = await CollectQualifiedNamesAsync(
					                      connection,
					                      """
					                      SELECT TABLE_SCHEMA, TABLE_NAME
					                      FROM INFORMATION_SCHEMA.TABLES
					                      WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'
					                      """,
					                      preserve,
					                      cancellationToken)
				                      .ConfigureAwait(false);

			if (tables.Count > 0)
			{
				await ExecuteNonQueryAsync(
						connection,
						"DROP TABLE IF EXISTS " + string.Join(", ", tables),
						cancellationToken)
					.ConfigureAwait(false);
			}

			// 3. Drop stored procedures one by one.
			//    MySQL does not support multi-object DROP for routines.
			await DropRoutinesAsync(
					connection,
					"""
					SELECT ROUTINE_SCHEMA, ROUTINE_NAME
					FROM INFORMATION_SCHEMA.ROUTINES
					WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'PROCEDURE'
					""",
					"PROCEDURE",
					cancellationToken)
				.ConfigureAwait(false);

			// 4. Drop functions one by one.
			await DropRoutinesAsync(
					connection,
					"""
					SELECT ROUTINE_SCHEMA, ROUTINE_NAME
					FROM INFORMATION_SCHEMA.ROUTINES
					WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'FUNCTION'
					""",
					"FUNCTION",
					cancellationToken)
				.ConfigureAwait(false);

			// 5. Drop events.
			//    Events are separate objects (like scheduled jobs) not tied to tables locally.
			//    They persist after DROP TABLE and must be removed explicitly to ensure a strict reset.
			List<string> events = await CollectQualifiedNamesAsync(
					                      connection,
					                      """
					                      SELECT EVENT_SCHEMA, EVENT_NAME
					                      FROM INFORMATION_SCHEMA.EVENTS
					                      WHERE EVENT_SCHEMA = DATABASE()
					                      """,
					                      preserveNames: null,
					                      cancellationToken)
				                      .ConfigureAwait(false);

			foreach (string evt in events)
			{
				await ExecuteNonQueryAsync(
						connection,
						$"DROP EVENT IF EXISTS {evt}",
						cancellationToken)
					.ConfigureAwait(false);
			}
		}
		finally
		{
			// Always restore FK checks, even if drops failed, to prevent session pollution
			// if the connection is returned to the pool. CancellationToken.None is intentional:
			// if the original token is already cancelled, we must still send this command to
			// avoid returning a connection with FOREIGN_KEY_CHECKS = 0 to the pool.
			await ExecuteNonQueryAsync(
					connection,
					"SET FOREIGN_KEY_CHECKS = 1",
					CancellationToken.None)
				.ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
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
			// MySQL equates schema with database; we always query the current database context via DATABASE().
			cmd.CommandText = """
			                  SELECT 1
			                  FROM INFORMATION_SCHEMA.TABLES
			                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND TABLE_TYPE = 'BASE TABLE'
			                  """;

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
	public async Task<RestoreCheckpointData?> ReadCheckpointAsync(
		DbConnection      connection,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// MySQL equates schema with database; schema parameter is ignored.
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
	/// MySQL equates schema with database, so the <paramref name="schema"/> parameter is ignored.
	/// The table is created with <c>TEXT</c> columns via <c>CREATE TABLE IF NOT EXISTS</c>.
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
		// Step 1: Create table (idempotent). MySQL ignores schema (it uses the database).
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
		await dbContext.Database
			.ExecuteSqlRawAsync(insertSql, [shuttleId, baselineMigrationId, startedUtc, startedUtc], cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task UpdateCheckpointPhaseAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		string            phase,
		string            updatedUtc,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// MySQL ignores schema parameter.
		string sql =
			$$"""
			  UPDATE {{QuoteIdentifier(tableName)}}
			  SET {{QuoteIdentifier("phase")}} = {0}, {{QuoteIdentifier("updated_utc")}} = {1}
			  """;
		await dbContext.Database.ExecuteSqlRawAsync(sql, [phase, updatedUtc], cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DropCheckpointTableAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// MySQL ignores schema parameter.
		string sql = $"DROP TABLE IF EXISTS {QuoteIdentifier(tableName)}";
		await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <exception cref="NotSupportedException">
	/// MySQL DataPort is not yet available because <c>Pomelo.EntityFrameworkCore.MySql</c> has not released
	/// an EF Core 10 compatible version.
	/// </exception>
	public IDataExportReader CreateExportReader(DatabaseOptions options, ILogger logger) =>
		throw new NotSupportedException(DataPortNotSupportedMessage);

	/// <inheritdoc/>
	/// <exception cref="NotSupportedException">
	/// MySQL DataPort is not yet available because <c>Pomelo.EntityFrameworkCore.MySql</c> has not released
	/// an EF Core 10 compatible version.
	/// </exception>
	public IDataImportWriter CreateImportWriter(string connectionString, ILogger logger, TimeProvider timeProvider) =>
		throw new NotSupportedException(DataPortNotSupportedMessage);

	/// <inheritdoc/>
	public string MapToShuttleStorageType(string providerDbType)
	{
		string lower = providerDbType.ToLowerInvariant();

		// Text types
		if (lower.StartsWith("varchar", StringComparison.Ordinal) ||
		    lower.StartsWith("char", StringComparison.Ordinal) ||
		    lower == "text" ||
		    lower == "tinytext" ||
		    lower == "mediumtext" ||
		    lower == "longtext" ||
		    lower == "enum" ||
		    lower == "set")
			return "TEXT";

		// Date/time types → ISO 8601 text
		if (lower == "datetime" ||
		    lower == "timestamp" ||
		    lower == "date" ||
		    lower == "time" ||
		    lower == "year")
			return "TEXT";

		// Integer types
		if (lower is "int" or "integer" or "smallint" or "mediumint" or "bigint" or "tinyint")
			return "INTEGER";

		// Boolean → integer (MySQL BOOL is alias for TINYINT(1))
		if (lower is "boolean" or "bool")
			return "INTEGER";

		// Floating-point types
		if (lower is "float" or "double" or "real")
			return "REAL";

		// Exact numeric types
		if (lower.StartsWith("decimal", StringComparison.Ordinal) ||
		    lower.StartsWith("numeric", StringComparison.Ordinal))
			return "NUMERIC";

		// Binary types
		if (lower == "blob" ||
		    lower == "tinyblob" ||
		    lower == "mediumblob" ||
		    lower == "longblob" ||
		    lower.StartsWith("varbinary", StringComparison.Ordinal) ||
		    lower.StartsWith("binary", StringComparison.Ordinal))
			return "BLOB";

		// Default fallback — preserves value as text
		return "TEXT";
	}

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     MySQL uses numeric error codes. Server-side errors are in the 1000-1999 and 3000+ ranges,
	///     while client-side errors (CR_*) are in the 2000-2999 range. This method checks for errors
	///     that indicate service unavailability:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description><b>Connection Errors:</b> Cannot connect, server gone, connection lost</description>
	///         </item>
	///         <item>
	///             <description><b>Resource Exhaustion:</b> Table/disk full, out of memory, too many connections</description>
	///         </item>
	///         <item>
	///             <description><b>Server State:</b> Server shutdown, server not available</description>
	///         </item>
	///     </list>
	///     <para>
	///     Since the Pomelo MySQL provider is not yet available for EF Core 10, the <c>MySqlException</c> type
	///     is detected by name and its <c>Number</c> property is read via reflection.
	///     </para>
	///     <para>
	///     Generic checks include <see cref="TimeoutException"/>, <see cref="SocketException"/>, and
	///     <see cref="EndOfStreamException"/>.
	///     </para>
	///     <para>
	///     References:
	///     <see href="https://dev.mysql.com/doc/mysql-errors/8.0/en/server-error-reference.html"/> (server errors),
	///     <see href="https://dev.mysql.com/doc/mysql-errors/8.0/en/client-error-reference.html"/> (client errors)
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

			// MySQL: Check MySqlException.Number for connection-related error codes.
			// TODO: Uncomment when Pomelo releases EF Core 10 compatible version.
			// if (current is MySqlException mysqlEx && IsMySqlConnectionError(mysqlEx.Number))
			//     return true;

			// MySQL fallback: Check by type name when provider is not directly referenced.
			if (current.GetType().Name == "MySqlException" &&
			    TryGetPropertyValue(current, "Number", out int mysqlNumber) &&
			    IsMySqlConnectionError(mysqlNumber))
				return true;

			// Generic: TimeoutException indicates connection timeout.
			if (current is TimeoutException)
				return true;

			// Generic: SocketException indicates network-level failures (connection refused,
			// host unreachable, network down, etc.). This is the only System.Net exception
			// that realistically appears in database exception chains.
			if (current is SocketException)
				return true;

			// Generic: EndOfStreamException during a database operation indicates the server closed
			// the connection unexpectedly (graceful TCP close without sending an error response).
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
	/// Attempts to get a property value from an object using reflection.
	/// Used for MySQL when the provider package is not directly referenced.
	/// </summary>
	/// <typeparam name="T">The expected type of the property value.</typeparam>
	/// <param name="obj">The object to read the property from.</param>
	/// <param name="propertyName">The name of the property to read.</param>
	/// <param name="value">
	/// When successful, contains the property value; otherwise, the default value of <typeparamref name="T"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the property was found and read successfully; otherwise, <see langword="false"/>.
	/// </returns>
	private static bool TryGetPropertyValue<T>(object obj, string propertyName, out T? value)
	{
		PropertyInfo? property = obj.GetType().GetProperty(propertyName);
		if (property != null && property.PropertyType == typeof(T))
		{
			value = (T?)property.GetValue(obj);
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Determines whether the MySQL error number indicates a connection-related failure.
	/// </summary>
	/// <param name="errorNumber">The <c>MySqlException.Number</c> value.</param>
	/// <returns><see langword="true"/> if the error indicates the database service is unavailable.</returns>
	private static bool IsMySqlConnectionError(int errorNumber)
	{
		// @formatter:off
		return errorNumber switch
		{
			// ============================================================
			// Connection / Network Errors (Server-side: 1000-1999)
			// ============================================================
			// NOTE: Authentication errors (1044, 1045) are intentionally excluded here.
			// If the connection itself fails with Access Denied, HandleConnectionFailure() catches it.
			// If Access Denied occurs during a command (e.g., admin revoked privileges mid-session),
			// that's a configuration problem, not a transient infrastructure failure.
			1042 => true, // ER_BAD_HOST_ERROR: Can't get hostname for your address
			1043 => true, // ER_HANDSHAKE_ERROR: Bad handshake
			1080 => true, // ER_FORCING_CLOSE: Server is closing the connection
			1081 => true, // ER_IPSOCK_ERROR: Can't create TCP/IP socket
			1129 => true, // ER_HOST_IS_BLOCKED: Host is blocked because of many connection errors
			1130 => true, // ER_HOST_NOT_PRIVILEGED: Host is not allowed to connect to this MySQL server
			1152 => true, // ER_ABORTING_CONNECTION: Aborted connection (reading/writing/auth)
			1153 => true, // ER_NET_PACKET_TOO_LARGE: Got a packet bigger than max_allowed_packet
			1154 => true, // ER_NET_READ_ERROR_FROM_PIPE: Got a read error from the connection pipe
			1155 => true, // ER_NET_FCNTL_ERROR: Got an error from fcntl()
			1156 => true, // ER_NET_PACKETS_OUT_OF_ORDER: Got packets out of order
			1157 => true, // ER_NET_UNCOMPRESS_ERROR: Couldn't uncompress communication packet
			1158 => true, // ER_NET_READ_ERROR: Got an error reading communication packets
			1159 => true, // ER_NET_READ_INTERRUPTED: Got timeout reading communication packets
			1160 => true, // ER_NET_ERROR_ON_WRITE: Got an error writing communication packets
			1161 => true, // ER_NET_WRITE_INTERRUPTED: Got timeout writing communication packets

			// ============================================================
			// Resource Exhaustion (Server-side)
			// ============================================================
			1016 => true, // ER_CANT_OPEN_FILE: Can't open file (often disk-related)
			1021 => true, // ER_DISK_FULL: Disk full; waiting for someone to free some space
			1037 => true, // ER_OUTOFMEMORY: Out of memory; restart server and try again
			1038 => true, // ER_OUT_OF_SORTMEMORY: Out of sort memory; increase server sort buffer
			1040 => true, // ER_CON_COUNT_ERROR: Too many connections
			1041 => true, // ER_OUT_OF_RESOURCES: Out of memory; check if mysqld or other process uses all memory
			1114 => true, // ER_RECORD_FILE_FULL: The table is full
			1135 => true, // ER_CANT_CREATE_THREAD: Can't create a new thread (out of memory)
			1203 => true, // ER_TOO_MANY_USER_CONNECTIONS: User already has more than max_user_connections connections
			1226 => true, // ER_USER_LIMIT_REACHED: User has exceeded the resource limit (connections, queries, etc.)

			// ============================================================
			// Server State (shutdown / not available)
			// ============================================================
			1053 => true, // ER_SERVER_SHUTDOWN: Server shutdown in progress
			1077 => true, // ER_NORMAL_SHUTDOWN: Normal shutdown
			1079 => true, // ER_SHUTDOWN_COMPLETE: Shutdown complete
			1290 => true, // ER_OPTION_PREVENTS_STATEMENT: Server running with --read-only (e.g., replica failover)
			1836 => true, // ER_READ_ONLY_MODE: Running in read-only mode
			3032 => true, // ER_INNODB_READ_ONLY: InnoDB is in read only mode
			3058 => true, // ER_INNODB_FORCED_RECOVERY: InnoDB is in force recovery mode (read-only, no modifications)
			3098 => true, // ER_DISK_FULL_NOWAIT: Disk is full writing (with NOWAIT option)
			3168 => true, // ER_SERVER_OFFLINE_MODE: Server is currently in offline mode

			// ============================================================
			// Client-side Connection Errors (2000-2999 range)
			// ============================================================
			2000 => true, // CR_UNKNOWN_ERROR: Unknown MySQL error
			2001 => true, // CR_SOCKET_CREATE_ERROR: Can't create UNIX socket
			2002 => true, // CR_CONNECTION_ERROR: Can't connect to local MySQL server through socket
			2003 => true, // CR_CONN_HOST_ERROR: Can't connect to MySQL server on host
			2004 => true, // CR_IPSOCK_ERROR: Can't create TCP/IP socket
			2005 => true, // CR_UNKNOWN_HOST: Unknown MySQL server host
			2006 => true, // CR_SERVER_GONE_ERROR: MySQL server has gone away
			2007 => true, // CR_VERSION_ERROR: Protocol mismatch
			2008 => true, // CR_OUT_OF_MEMORY: MySQL client ran out of memory
			2009 => true, // CR_WRONG_HOST_INFO: Wrong host info
			2010 => true, // CR_LOCALHOST_CONNECTION: Localhost via UNIX socket
			2012 => true, // CR_SERVER_HANDSHAKE_ERR: Error in server handshake
			2013 => true, // CR_SERVER_LOST: Lost connection to MySQL server during query
			2024 => true, // CR_PROBE_SLAVE_STATUS: Error on SLAVE status
			2025 => true, // CR_PROBE_SLAVE_HOSTS: Error on SLAVE hosts
			2026 => true, // CR_PROBE_SLAVE_CONNECT: Error on SLAVE connect
			2027 => true, // CR_PROBE_MASTER_CONNECT: Error on MASTER connect
			2028 => true, // CR_SSL_CONNECTION_ERROR: SSL connection error (TLS handshake failure)
			2047 => true, // CR_CONN_UNKNOW_PROTOCOL: Wrong or unknown protocol
			2048 => true, // CR_INVALID_CONN_HANDLE: Invalid connection handle
			2055 => true, // CR_SERVER_LOST_EXTENDED: Lost connection to MySQL server, system error

			var _ => false
		};
		// @formatter:on
	}

	/// <summary>
	/// Queries <c>INFORMATION_SCHEMA</c> for schema-qualified object names and returns them as
	/// backtick-quoted <c>`schema`.`name`</c> strings ready for use in DROP statements.
	/// </summary>
	/// <param name="connection">The open database connection.</param>
	/// <param name="sql">
	/// A query returning two columns: <c>schema</c> (index 0) and <c>name</c> (index 1).
	/// </param>
	/// <param name="preserveNames">
	/// Optional set of object names (unqualified) to exclude. Compared against column index 1.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A list of backtick-quoted, schema-qualified identifiers.</returns>
	private static async Task<List<string>> CollectQualifiedNamesAsync(
		DbConnection      connection,
		string            sql,
		HashSet<string>?  preserveNames,
		CancellationToken cancellationToken)
	{
		var names = new List<string>();

		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText = sql;
			DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					string schema = reader.GetString(0);
					string name = reader.GetString(1);

					if (preserveNames is null || !preserveNames.Contains(name))
						names.Add(SqlIdentifierHelper.QuoteMySql(schema) + "." + SqlIdentifierHelper.QuoteMySql(name));
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

		return names;
	}

	/// <summary>
	/// Drops routines (procedures or functions) one by one, since MySQL does not support
	/// multi-object <c>DROP PROCEDURE</c> or <c>DROP FUNCTION</c>.
	/// </summary>
	/// <param name="connection">The open database connection.</param>
	/// <param name="querySql">
	/// A query returning two columns (schema at index 0, name at index 1) from <c>INFORMATION_SCHEMA.ROUTINES</c>.
	/// </param>
	/// <param name="routineType">The SQL keyword for the routine kind (<c>PROCEDURE</c> or <c>FUNCTION</c>).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	private static async Task DropRoutinesAsync(
		DbConnection      connection,
		string            querySql,
		string            routineType,
		CancellationToken cancellationToken)
	{
		List<string> routines = await CollectQualifiedNamesAsync(
				                        connection,
				                        querySql,
				                        preserveNames: null,
				                        cancellationToken)
			                        .ConfigureAwait(false);

		foreach (string routine in routines)
		{
			await ExecuteNonQueryAsync(
					connection,
					$"DROP {routineType} IF EXISTS {routine}",
					cancellationToken)
				.ConfigureAwait(false);
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
