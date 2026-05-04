// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Data.Common;
using System.Net.Sockets;

using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Export.Implementations;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.DataPort.Import.Implementations;
using LumaCore.Data.Initialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace LumaCore.Data.Providers;

/// <summary>
/// PostgreSQL implementation of <see cref="IDatabaseProviderOperations"/>.
/// </summary>
public sealed class PostgreSqlProviderOperations : IDatabaseProviderOperations
{
	/// <inheritdoc/>
	public string ProviderName => DatabaseProviders.PostgreSql;

	/// <inheritdoc/>
	/// <remarks>
	/// PostgreSQL uses double-quotes for identifier quoting. Any embedded double-quotes are escaped by
	/// doubling them.
	/// </remarks>
	public string QuoteIdentifier(string identifier) => SqlIdentifierHelper.QuotePostgres(identifier);

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     Schema cleanup drops all objects individually within a single <c>DO $body$</c> anonymous block,
	///     which is atomic — if any DROP fails, the entire block is rolled back. This replaces the previous
	///     <c>DROP SCHEMA CASCADE</c> approach, which could not preserve individual tables.
	///     </para>
	///     <para>
	///     <b>Note:</b> By keeping the <c>public</c> schema intact (instead of dropping and recreating it),
	///     this avoids the <c>GRANT ALL ON SCHEMA public TO public</c> requirement that can fail on managed
	///     PostgreSQL services where the application user lacks schema-level GRANT privileges.
	///     </para>
	/// </remarks>
	public async Task DropSchemaObjectsAsync(
		LumaCoreDbContext    dbContext,
		IReadOnlySet<string> tablesToPreserve,
		CancellationToken    cancellationToken,
		ILogger?             logger = null)
	{
		logger?.LogInformation("Starting schema cleanup for provider {ProviderName}...", ProviderName);

		string schemaName = dbContext.Model.GetDefaultSchema() ?? "public";
		string escapedSchemaLiteral = EscapePgStringLiteral(schemaName);

		// Build the exclusion list for the PL/pgSQL block.
		// format('%I', ...) is PostgreSQL's built-in identifier quoting (handles reserved words,
		// special characters, and mixed case). oid::regprocedure gives the full function signature
		// including parameter types, which is required for correct DROP of overloaded functions.
		string excludeClause = "";
		string sequenceExcludeClause = "";

		if (tablesToPreserve.Count > 0)
		{
			string preserveList = string.Join(", ", tablesToPreserve.Select(EscapePgStringLiteral));

			excludeClause = $"AND c.relname NOT IN ({preserveList})";

			// Exclude sequences owned by preserved tables from the sequence cleanup step.
			// After non-preserved tables are dropped with CASCADE, only sequences owned by preserved
			// tables and standalone (unowned) sequences remain. We must keep the former intact.
			// deptype 'a' = auto (SERIAL), 'i' = internal (IDENTITY).
			sequenceExcludeClause =
				$"""
				 AND NOT EXISTS (
				   SELECT 1 FROM pg_depend d
				   JOIN pg_class owner ON d.refobjid = owner.oid
				   WHERE d.objid = c.oid AND d.deptype IN ('a','i')
				     AND owner.relnamespace = n.oid
				     AND owner.relname IN ({preserveList}))
				 """;
		}

		// Tagged dollar quoting ($body$...$body$) is used instead of plain $$ to prevent
		// premature block termination if interpolated values contain the sequence "$$".
		// PostgreSQL's dollar-quoting lexer is context-free: it scans for the closing tag
		// regardless of nesting inside single-quoted strings or comments.
		string sql = $"""
		              DO $body$
		              DECLARE
		                  rec RECORD;
		                  _schema TEXT := {escapedSchemaLiteral};
		              BEGIN
		                  -- 1. Drop all tables except preserved ones.
		                  --    CASCADE automatically drops dependent FKs, views, materialized views, triggers,
		                  --    and table-owned sequences.
		                  --    We exclude tables that are part of an extension (deptype = 'e').
		                  FOR rec IN
		                      SELECT c.relname AS tablename
		                      FROM pg_class c
		                      JOIN pg_namespace n ON c.relnamespace = n.oid
		                      WHERE n.nspname = _schema
		                        AND c.relkind IN ('r', 'p')
		                        {excludeClause}
		                        AND NOT EXISTS (SELECT 1 FROM pg_depend WHERE objid = c.oid AND deptype = 'e')
		                  LOOP
		                      EXECUTE format('DROP TABLE IF EXISTS %I.%I CASCADE', _schema, rec.tablename);
		                  END LOOP;

		                  -- 1b. Drop foreign tables.
		                  --     Foreign tables require DROP FOREIGN TABLE and are identified by relkind = 'f'.
		                  FOR rec IN
		                      SELECT c.relname AS tablename
		                      FROM pg_class c
		                      JOIN pg_namespace n ON c.relnamespace = n.oid
		                      WHERE n.nspname = _schema
		                        AND c.relkind = 'f'
		                        {excludeClause}
		                        AND NOT EXISTS (SELECT 1 FROM pg_depend WHERE objid = c.oid AND deptype = 'e')
		                  LOOP
		                      EXECUTE format('DROP FOREIGN TABLE IF EXISTS %I.%I CASCADE', _schema, rec.tablename);
		                  END LOOP;

		                  -- 2. Drop remaining views (standalone views not caught by table CASCADE).
		                  --    We exclude views that are part of an extension.
		                  FOR rec IN
		                      SELECT c.relname AS viewname
		                      FROM pg_class c
		                      JOIN pg_namespace n ON c.relnamespace = n.oid
		                      WHERE n.nspname = _schema
		                        AND c.relkind = 'v'
		                        AND NOT EXISTS (SELECT 1 FROM pg_depend WHERE objid = c.oid AND deptype = 'e')
		                  LOOP
		                      EXECUTE format('DROP VIEW IF EXISTS %I.%I CASCADE', _schema, rec.viewname);
		                  END LOOP;

		                  -- 3. Drop remaining materialized views (not caught by table CASCADE).
		                  --    pg_matviews is separate from pg_views; materialized views require DROP MATERIALIZED VIEW.
		                  FOR rec IN
		                      SELECT c.relname AS matviewname
		                      FROM pg_class c
		                      JOIN pg_namespace n ON c.relnamespace = n.oid
		                      WHERE n.nspname = _schema
		                        AND c.relkind = 'm'
		                        AND NOT EXISTS (SELECT 1 FROM pg_depend WHERE objid = c.oid AND deptype = 'e')
		                  LOOP
		                      EXECUTE format('DROP MATERIALIZED VIEW IF EXISTS %I.%I CASCADE', _schema, rec.matviewname);
		                  END LOOP;

		                  -- 4. Drop remaining sequences not owned by any preserved table.
		                  --    We exclude sequences that are part of an extension.
		                  FOR rec IN
		                      SELECT c.relname
		                      FROM pg_class c
		                      JOIN pg_namespace n ON c.relnamespace = n.oid
		                      WHERE n.nspname = _schema
		                        AND c.relkind = 'S'
		                        {sequenceExcludeClause}
		                        AND NOT EXISTS (SELECT 1 FROM pg_depend WHERE objid = c.oid AND deptype = 'e')
		                  LOOP
		                      EXECUTE format('DROP SEQUENCE IF EXISTS %I.%I CASCADE', _schema, rec.relname);
		                  END LOOP;

		                  -- 5. Drop functions, procedures, and aggregates.
		                  --    prokind: 'f' = function, 'p' = procedure, 'a' = aggregate, 'w' = window (PG 11+).
		                  --    oid::regprocedure includes the full signature with parameter types.
		                  --    Aggregates MUST use DROP AGGREGATE; DROP FUNCTION errors on aggregates even with IF EXISTS.
		                  --    We exclude routines that are part of an extension.
		                  FOR rec IN
		                      SELECT p.oid::regprocedure::text AS func_sig,
		                             CASE p.prokind
		                                 WHEN 'p' THEN 'PROCEDURE'
		                                 WHEN 'a' THEN 'AGGREGATE'
		                                 ELSE 'FUNCTION'
		                             END AS kind
		                      FROM pg_proc p
		                      JOIN pg_namespace n ON p.pronamespace = n.oid
		                      WHERE n.nspname = _schema
		                        AND NOT EXISTS (SELECT 1 FROM pg_depend WHERE objid = p.oid AND deptype = 'e')
		                  LOOP
		                      EXECUTE format('DROP %s IF EXISTS %s CASCADE', rec.kind, rec.func_sig);
		                  END LOOP;

		                  -- 6. Drop custom enum, composite types, and domains.
		                  --    After step 1, the only remaining pg_class entries with reltype → type OID are:
		                  --      (a) preserved tables (relkind = 'r'/'p'/'f') — their row types must NOT be dropped
		                  --      (b) standalone composite types (relkind = 'c') — these SHOULD be dropped
		                  --    The filter "c.relkind != 'c'" excludes only (a), allowing (b) through.
		                  --    We use RESTRICT and trap exception to avoid dropping types used by preserved tables.
		                  --    We order by OID DESC to drop dependent types before their dependencies.
		                  FOR rec IN
		                      SELECT t.typname
		                      FROM pg_type t
		                      JOIN pg_namespace n ON t.typnamespace = n.oid
		                      WHERE n.nspname = _schema
		                        -- 'e'=enum, 'c'=composite, 'd'=domain
		                        AND t.typtype IN ('e', 'c', 'd')
		                        AND NOT EXISTS (SELECT 1 FROM pg_depend WHERE objid = t.oid AND deptype = 'e')
		                        AND NOT EXISTS (
		                            SELECT 1 FROM pg_class c
		                            WHERE c.reltype = t.oid
		                              AND c.relkind != 'c'
		                        )
		                      ORDER BY t.oid DESC
		                  LOOP
		                      BEGIN
		                          EXECUTE format('DROP TYPE IF EXISTS %I.%I RESTRICT', _schema, rec.typname);
		                      EXCEPTION WHEN dependent_objects_still_exist THEN
		                          -- Type is used by a preserved table or another remaining object; skip it.
		                          NULL;
		                      END;
		                  END LOOP;
		              END $body$;
		              """;

		await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// PostgreSQL uses the <c>pg_tables</c> system view for table existence checks. Unlike
	/// <c>INFORMATION_SCHEMA.TABLES</c>, <c>pg_tables</c> is PostgreSQL-native and filters by
	/// <c>schemaname</c> and <c>tablename</c> directly. The schema defaults to <c>"public"</c> when
	/// not explicitly provided. Both filter values are sent as query parameters to prevent SQL injection.
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

		// PostgreSQL supports real schemas; default to 'public' if none is provided.
		string targetSchema = schema ?? "public";

		DbCommand cmd = connection.CreateCommand();
		try
		{
			// pg_tables joins pg_class and pg_namespace internally. Both schemaname and tablename
			// are compared case-sensitively (PostgreSQL folds unquoted identifiers to lowercase).
			cmd.CommandText = "SELECT 1 FROM pg_tables WHERE schemaname = @schema AND tablename = @tableName";

			DbParameter schemaParam = cmd.CreateParameter();
			schemaParam.ParameterName = "@schema";
			schemaParam.Value = targetSchema;
			cmd.Parameters.Add(schemaParam);

			DbParameter tableParam = cmd.CreateParameter();
			tableParam.ParameterName = "@tableName";
			tableParam.Value = tableName;
			cmd.Parameters.Add(tableParam);

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
	/// PostgreSQL supports real schemas (unlike MySQL where schema and database are equivalent). The
	/// checkpoint table is accessed via a fully schema-qualified identifier (e.g.,
	/// <c>"public"."_restore_checkpoint"</c>). The schema defaults to <c>"public"</c> when not
	/// explicitly provided.
	/// </remarks>
	public async Task<RestoreCheckpointData?> ReadCheckpointAsync(
		DbConnection      connection,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// Prevent crash if called with a closed connection (e.g., from a pool or stale context).
		if (connection.State != ConnectionState.Open)
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Default to the 'public' schema, which is the conventional default schema in PostgreSQL.
		string targetSchema = schema ?? "public";
		bool exists = await TableExistsAsync(connection, tableName, cancellationToken, targetSchema)
			              .ConfigureAwait(false);
		if (!exists)
			return null;

		// Build a fully schema-qualified, double-quote-escaped table reference for the SELECT.
		string qualifiedTable = $"{QuoteIdentifier(targetSchema)}.{QuoteIdentifier(tableName)}";

		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText =
				$"""
				 SELECT {QuoteIdentifier("shuttle_id")},
				   {QuoteIdentifier("baseline_migration_id")},
				   {QuoteIdentifier("phase")},
				   {QuoteIdentifier("started_utc")}
				 FROM {qualifiedTable}
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
	/// PostgreSQL uses schema-qualified table names. The table is created with <c>TEXT</c> columns
	/// via <c>CREATE TABLE IF NOT EXISTS</c>.
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
		// Default to 'public' schema; build schema-qualified table reference.
		string targetSchema = schema ?? "public";
		string qt = $"{QuoteIdentifier(targetSchema)}.{QuoteIdentifier(tableName)}";

		// Step 1: Create table (idempotent).
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
	/// PostgreSQL checkpoint updates use schema-qualified table references. The schema defaults to
	/// <c>"public"</c> when not explicitly provided.
	/// </remarks>
	public async Task UpdateCheckpointPhaseAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		string            phase,
		string            updatedUtc,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// Default to 'public' schema; build schema-qualified table reference.
		string targetSchema = schema ?? "public";
		string qt = $"{QuoteIdentifier(targetSchema)}.{QuoteIdentifier(tableName)}";
		string sql =
			$$"""
			  UPDATE {{qt}}
			  SET {{QuoteIdentifier("phase")}} = {0}, {{QuoteIdentifier("updated_utc")}} = {1}
			  """;
		await dbContext
			.Database
			.ExecuteSqlRawAsync(sql, [phase, updatedUtc], cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// PostgreSQL uses schema-qualified <c>DROP TABLE IF EXISTS</c> to target the correct schema. The
	/// schema defaults to <c>"public"</c> when not explicitly provided.
	/// </remarks>
	public async Task DropCheckpointTableAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// Default to 'public' schema; build schema-qualified table reference for the DROP.
		string targetSchema = schema ?? "public";
		string qt = $"{QuoteIdentifier(targetSchema)}.{QuoteIdentifier(tableName)}";
		string sql = $"DROP TABLE IF EXISTS {qt}";
		await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public IDataExportReader CreateExportReader(DatabaseOptions options, ILogger logger) => new PostgresExportReader(
		options.ConnectionString,
		shuttleTypeMapper: MapToShuttleStorageType);

	/// <inheritdoc/>
	public IDataImportWriter CreateImportWriter(string connectionString, ILogger logger, TimeProvider timeProvider) =>
		new PostgresImportWriter(connectionString, timeProvider: timeProvider, logger: logger);

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     PostgreSQL's type system is OID-based and includes aliases and pseudo-types that don't exist
	///     in other databases. Notable PostgreSQL-specific mappings:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <c>serial</c> / <c>bigserial</c> — auto-increment pseudo-types that resolve to
	///             <c>integer</c> / <c>bigint</c> with an owned sequence. Mapped to <c>INTEGER</c>.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>timestamptz</c> — shorthand alias for <c>timestamp with time zone</c>. Both forms
	///             are mapped to <c>TEXT</c> (ISO 8601).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>bytea</c> — PostgreSQL's sole binary type (unlike MySQL/SQL Server which offer
	///             multiple binary types). Mapped to <c>BLOB</c>.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>uuid</c> — native 128-bit type stored as <c>TEXT</c> in the Shuttle format.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>money</c> — fixed-precision currency type. Mapped to <c>NUMERIC</c> to preserve
	///             exact decimal precision.
	///             </description>
	///         </item>
	///     </list>
	/// </remarks>
	public string MapToShuttleStorageType(string providerDbType)
	{
		string lower = providerDbType.ToLowerInvariant();

		// Text types — PostgreSQL's "text" is the preferred unbounded string type (no length limit).
		// "character varying" (varchar) and "character" (char) have optional length constraints.
		if (lower == "text" ||
		    lower.StartsWith("varchar", StringComparison.Ordinal) ||
		    lower.StartsWith("char", StringComparison.Ordinal)) // Covers "character varying", "character"
			return "TEXT";

		// Timestamp / date / time types → ISO 8601 text.
		// "timestamptz" is a PostgreSQL shorthand for "timestamp with time zone".
		if (lower.StartsWith("timestamp", StringComparison.Ordinal) || // covers "with" and "without time zone"
		    lower == "timestamptz" ||
		    lower == "date" ||
		    lower == "time")
			return "TEXT";

		// UUID → text. PostgreSQL has a native uuid type (128-bit); stored as text in Shuttle format.
		if (lower == "uuid")
			return "TEXT";

		// Integer types (including auto-increment pseudo-types).
		// "serial"/"bigserial" are PostgreSQL shorthand for integer/bigint with an owned sequence.
		// "int4"/"int8" are PostgreSQL internal aliases for integer and bigint respectively.
		if (lower is "integer" or "int" or "int4" or "smallint" or "bigint" or "int8" or "serial" or "bigserial")
			return "INTEGER";

		// Boolean → integer (0/1). PostgreSQL has a native boolean type (unlike MySQL's TINYINT(1) alias).
		if (lower == "boolean")
			return "INTEGER";

		// Floating-point types. "double precision" is PostgreSQL's name for 8-byte IEEE 754.
		if (lower is "real" or "double precision" or "float")
			return "REAL";

		// Exact numeric types. "money" is a PostgreSQL fixed-precision currency type (8 bytes,
		// locale-dependent formatting) — mapped to NUMERIC to preserve exact decimal precision.
		if (lower.StartsWith("decimal", StringComparison.Ordinal) ||
		    lower.StartsWith("numeric", StringComparison.Ordinal) ||
		    lower == "money")
			return "NUMERIC";

		// Binary — "bytea" is PostgreSQL's only binary large object type (byte array).
		// Unlike MySQL (BLOB/TINYBLOB/etc.) or SQL Server (VARBINARY/IMAGE), PostgreSQL has just one.
		if (lower == "bytea")
			return "BLOB";

		// Default fallback — preserves value as text
		return "TEXT";
	}

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     PostgreSQL uses standardized 5-character SQLSTATE codes where the first two characters indicate
	///     the error class. This method checks for classes that indicate service unavailability:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description><b>Class 08:</b> Connection Exception (connection failure, broken, etc.)</description>
	///         </item>
	///         <item>
	///             <description><b>Class 53:</b> Insufficient Resources (disk full, out of memory, too many connections)</description>
	///         </item>
	///         <item>
	///             <description>
	///             <b>Class 57:</b> Operator Intervention (admin shutdown, crash shutdown, cannot connect now).
	///             <b>Exception:</b> 57014 (query_canceled) and 57P05 (idle_session_timeout) are excluded to avoid
	///             false positives from user cancellations and session timeouts.
	///             </description>
	///         </item>
	///         <item>
	///             <description><b>Class 58:</b> System Error (I/O error, undefined file, duplicate file)</description>
	///         </item>
	///     </list>
	///     <para>
	///     Generic checks include <see cref="TimeoutException"/>, <see cref="SocketException"/>, and
	///     <see cref="EndOfStreamException"/>.
	///     </para>
	///     <para>
	///     Reference: <see href="https://www.postgresql.org/docs/current/errcodes-appendix.html"/>
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

			// PostgreSQL: Check NpgsqlException.SqlState for connection-related SQLSTATE codes.
			if (current is NpgsqlException npgsqlEx && IsPostgreSqlConnectionError(npgsqlEx.SqlState))
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
			// This covers edge cases where Npgsql wraps the exception with SqlState = null.
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
	/// Determines whether the PostgreSQL SQLSTATE code indicates a connection-related failure.
	/// </summary>
	/// <param name="sqlState">The <c>NpgsqlException.SqlState</c> value (5-character SQLSTATE code).</param>
	/// <returns><see langword="true"/> if the SQLSTATE indicates the database service is unavailable.</returns>
	private static bool IsPostgreSqlConnectionError(string? sqlState)
	{
		if (string.IsNullOrEmpty(sqlState) || sqlState.Length < 2)
			return false;

		// IMPORTANT: 57014 (query_canceled) and 57P05 (idle_session_timeout) must be excluded to avoid
		// false positives. 57014 is triggered when users cancel requests (e.g., navigate away in Blazor)
		// or when statement_timeout is reached. 57P05 is a session timeout, not a server outage.
		if (sqlState is "57014" or "57P05")
			return false;

		// Get the error class (first 2 characters)
		string errorClass = sqlState[..2];

		return errorClass switch
		{
			// Class 08 — Connection Exception
			// 08000: connection_exception
			// 08003: connection_does_not_exist
			// 08006: connection_failure
			// 08001: sqlclient_unable_to_establish_sqlconnection
			// 08004: sqlserver_rejected_establishment_of_sqlconnection
			// 08007: transaction_resolution_unknown
			// 08P01: protocol_violation
			"08" => true,

			// Class 53 — Insufficient Resources
			// 53000: insufficient_resources
			// 53100: disk_full
			// 53200: out_of_memory
			// 53300: too_many_connections
			// 53400: configuration_limit_exceeded
			"53" => true,

			// Class 57 — Operator Intervention
			// 57000: operator_intervention
			// 57014: query_canceled (user-initiated, but also used for statement_timeout)
			// 57P01: admin_shutdown
			// 57P02: crash_shutdown
			// 57P03: cannot_connect_now
			// 57P04: database_dropped
			// 57P05: idle_session_timeout (excluded above)
			"57" => true,

			// Class 58 — System Error (external causes)
			// 58000: system_error
			// 58030: io_error
			// 58P01: undefined_file
			// 58P02: duplicate_file
			"58" => true,

			var _ => false
		};
	}

	/// <summary>
	/// Wraps a value in single quotes with proper escaping for use in PL/pgSQL string literals.
	/// </summary>
	/// <param name="value">The raw string value.</param>
	/// <returns>A safely quoted string literal, e.g. <c>'foo''bar'</c> for input <c>foo'bar</c>.</returns>
	/// <remarks>
	/// Used exclusively inside <c>DO $body$...$body$</c> PL/pgSQL anonymous blocks
	/// (see <see cref="DropSchemaObjectsAsync"/>), where standard ADO.NET query parameterization
	/// (<c>@param</c>) is not available. PL/pgSQL string literals use single-quote doubling
	/// as the escape mechanism (<c>'</c> → <c>''</c>).
	/// </remarks>
	private static string EscapePgStringLiteral(string value) => $"'{value.Replace("'", "''")}'";
}
