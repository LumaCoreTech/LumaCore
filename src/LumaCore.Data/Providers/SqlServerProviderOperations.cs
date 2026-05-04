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

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Providers;

/// <summary>
/// SQL Server implementation of <see cref="IDatabaseProviderOperations"/>.
/// </summary>
public sealed class SqlServerProviderOperations : IDatabaseProviderOperations
{
	/// <inheritdoc/>
	public string ProviderName => DatabaseProviders.SqlServer;

	/// <inheritdoc/>
	/// <remarks>
	/// SQL Server uses square brackets for identifier quoting. Any embedded closing brackets are escaped
	/// by doubling them.
	/// </remarks>
	public string QuoteIdentifier(string identifier) => SqlIdentifierHelper.QuoteSqlServer(identifier);

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     SQL Server does not support <c>DROP SCHEMA CASCADE</c>, so all objects must be dropped
	///     individually. This method builds and executes a T-SQL script that uses <c>sys.*</c> system
	///     catalog views to discover objects, generates <c>DROP</c> statements dynamically, and runs
	///     them via <c>sp_executesql</c> (SQL Server's built-in dynamic SQL executor).
	///     </para>
	///     <para>
	///         <b>Two-phase execution:</b>
	///     </para>
	///     <list type="number">
	///         <item>
	///             <description>
	///             <b>Batch phase (fail-fast):</b> FKs, views, tables, procedures, functions, sequences, and
	///             synonyms are accumulated into a single <c>@sql</c> variable and executed as one
	///             batch. The drop order (FKs → views → tables → …) resolves dependencies, so this
	///             phase should not fail under normal conditions. If an unexpected error does occur
	///             (e.g., an unresolvable dependency), the entire batch aborts — this is by design
	///             to surface schema inconsistencies rather than silently skipping objects.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <b>Cursor phase (UDTs only):</b> User-defined types are dropped one at a time via
	///             a <c>CURSOR</c> with individual <c>BEGIN TRY / END CATCH</c> error handling. UDTs
	///             can have unpredictable cross-dependencies (e.g., a preserved table referencing a
	///             UDT, or one UDT depending on another). A single failure must not abort the
	///             remaining drops. This mirrors the PostgreSQL provider's <c>BEGIN/EXCEPTION</c>
	///             pattern for custom types.
	///             </description>
	///         </item>
	///     </list>
	/// </remarks>
	public async Task DropSchemaObjectsAsync(
		LumaCoreDbContext    dbContext,
		IReadOnlySet<string> tablesToPreserve,
		CancellationToken    cancellationToken,
		ILogger?             logger = null)
	{
		logger?.LogInformation("Starting schema cleanup for provider {ProviderName}...", ProviderName);

		string schemaName = dbContext.Model.GetDefaultSchema() ?? "dbo";
		string escapedSchema = EscapeTSqlStringLiteral(schemaName);

		// Build the exclusion lists for the T-SQL script.
		string fkPreserveClause = "";
		string tablePreserveClause = "";
		string sequencePreserveClause = "";

		if (tablesToPreserve.Count > 0)
		{
			string preserveList = string.Join(", ", tablesToPreserve.Select(EscapeTSqlStringLiteral));

			// FK clause: drop the FK unless BOTH parent and referenced table are preserved. This handles
			// the case where a preserved table has FKs pointing to non-preserved tables that will be dropped.
			fkPreserveClause = $"AND NOT (pt.name IN ({preserveList}) AND rt.name IN ({preserveList}))";

			// Table clause: simple exclusion for table drops.
			tablePreserveClause = $"AND t.name NOT IN ({preserveList})";

			// Sequence clause: exclude sequences referenced by DEFAULT constraints on preserved tables.
			// This mirrors PostgreSQL's pg_depend exclusion (deptype 'a'/'i') for the SQL Server catalog.
			// Standalone sequences used only from application code (e.g., pure HiLo without DEFAULT)
			// are not linked in sys.sql_expression_dependencies and will still be dropped.
			sequencePreserveClause =
				$"""
				 AND NOT EXISTS (
				   SELECT 1 FROM sys.sql_expression_dependencies sed
				   JOIN sys.default_constraints dc ON sed.referencing_id = dc.object_id
				   JOIN sys.tables t ON dc.parent_object_id = t.object_id
				   WHERE sed.referenced_id = s.object_id AND t.name IN ({preserveList})
				 )
				 """;
		}

		// ──────────────────────────────────────────────────────────────────────────────
		// T-SQL schema cleanup script — see <remarks> for the two-phase architecture.
		//
		// T-SQL glossary (for non-SQL Server developers):
		//   SELECT @var += N'...' FROM  — Appends a string per matching row to @var.
		//   QUOTENAME(name)             — Wraps name in [...], escapes ] as ]].
		//   sp_executesql @sql          — Executes the dynamic SQL string @sql.
		//   SCHEMA_ID(N'name')          — Returns the internal numeric ID for a schema.
		//   SCHEMA_NAME(id)             — Returns the schema name for a numeric ID.
		//   is_ms_shipped = 0           — Excludes built-in SQL Server system objects.
		//   CURSOR LOCAL FAST_FORWARD   — Lightweight read-only cursor; LOCAL = scoped
		//                                 to this batch; FAST_FORWARD = forward-only.
		//   BEGIN TRY / END CATCH       — T-SQL equivalent of try/catch.
		//   @@FETCH_STATUS              — 0 while the cursor still has rows to return.
		//
		// NOTE: The "SELECT @sql += ..." pattern is technically undocumented (Microsoft
		// only guarantees the last row's value for SELECT variable assignment). In practice
		// it is universally reliable for serial plans, and these small system catalog
		// queries will never trigger parallelism. STRING_AGG() (SQL Server 2017+) is
		// unsuitable because each SELECT block generates a different statement type.
		// ──────────────────────────────────────────────────────────────────────────────
		string sql = $$"""
		               DECLARE @sql NVARCHAR(MAX) = N'';
		               DECLARE @schemaId INT = SCHEMA_ID({{escapedSchema}});

		               -- ============================================================
		               -- Phase 1: Accumulate DROP statements into @sql, execute as
		               -- one batch. Drop order resolves dependencies.
		               -- ============================================================

		               -- 1a. Foreign keys: drop unless BOTH parent and referenced
		               -- table are preserved. Prevents constraint violations when
		               -- dropping non-preserved tables.
		               SELECT @sql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(pt.schema_id)) + N'.' + QUOTENAME(pt.name) +
		                              N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
		               FROM sys.foreign_keys fk
		               JOIN sys.tables pt ON fk.parent_object_id = pt.object_id
		               JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
		               WHERE pt.schema_id = @schemaId
		                 AND pt.is_ms_shipped = 0
		                 AND rt.is_ms_shipped = 0
		                 {{fkPreserveClause}};

		               -- 1b. Views (must be dropped before the tables they reference).
		               SELECT @sql += N'DROP VIEW ' + QUOTENAME(SCHEMA_NAME(v.schema_id)) + N'.' + QUOTENAME(v.name) + N';'
		               FROM sys.views v
		               WHERE v.schema_id = @schemaId AND v.is_ms_shipped = 0;

		               -- 1c. Tables (safe now — all FKs removed above).
		               SELECT @sql += N'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N';'
		               FROM sys.tables t
		               WHERE t.schema_id = @schemaId AND t.is_ms_shipped = 0
		                 {{tablePreserveClause}};

		               -- 1d. Stored procedures.
		               SELECT @sql += N'DROP PROCEDURE ' + QUOTENAME(SCHEMA_NAME(p.schema_id)) + N'.' + QUOTENAME(p.name) + N';'
		               FROM sys.procedures p
		               WHERE p.schema_id = @schemaId AND p.is_ms_shipped = 0;

		               -- 1e. Functions (scalar 'FN', inline table-valued 'IF', multi-statement 'TF').
		               SELECT @sql += N'DROP FUNCTION ' + QUOTENAME(SCHEMA_NAME(o.schema_id)) + N'.' + QUOTENAME(o.name) + N';'
		               FROM sys.objects o
		               WHERE o.schema_id = @schemaId
		                 AND o.type IN ('FN', 'IF', 'TF')
		                 AND o.is_ms_shipped = 0;

		               -- 1f. Sequences (independent objects, not removed by DROP TABLE).
		               SELECT @sql += N'DROP SEQUENCE ' + QUOTENAME(SCHEMA_NAME(s.schema_id)) + N'.' + QUOTENAME(s.name) + N';'
		               FROM sys.sequences s
		               WHERE s.schema_id = @schemaId AND s.is_ms_shipped = 0
		                 {{sequencePreserveClause}};

		               -- 1g. Synonyms.
		               SELECT @sql += N'DROP SYNONYM ' + QUOTENAME(SCHEMA_NAME(s.schema_id)) + N'.' + QUOTENAME(s.name) + N';'
		               FROM sys.synonyms s
		               WHERE s.schema_id = @schemaId AND s.is_ms_shipped = 0;

		               -- Execute the accumulated Phase 1 batch.
		               IF LEN(@sql) > 0 EXEC sp_executesql @sql;

		               -- ============================================================
		               -- Phase 2: Drop UDTs individually with error handling. UDTs
		               -- can have unpredictable cross-dependencies (e.g., preserved
		               -- table referencing a UDT). Each DROP runs in its own
		               -- TRY/CATCH so one failure does not abort the rest. Mirrors
		               -- PostgreSQL's BEGIN/EXCEPTION pattern for custom types.
		               -- ============================================================

		               -- Cursor lifecycle: DECLARE → OPEN → FETCH in loop → CLOSE →
		               -- DEALLOCATE. LOCAL = scoped to this batch (auto-cleaned).
		               -- FAST_FORWARD = read-only, forward-only (minimal overhead).
		               DECLARE @udtSql NVARCHAR(MAX);
		               DECLARE udt_cursor CURSOR LOCAL FAST_FORWARD FOR
		                 SELECT N'DROP TYPE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
		                 FROM sys.types t
		                 WHERE t.schema_id = @schemaId AND t.is_user_defined = 1;

		               OPEN udt_cursor;
		               FETCH NEXT FROM udt_cursor INTO @udtSql;
		               WHILE @@FETCH_STATUS = 0
		               BEGIN
		                 BEGIN TRY
		                   EXEC sp_executesql @udtSql;
		                 END TRY
		                 BEGIN CATCH
		                   -- Type is still referenced by a preserved table or another remaining object; skip it.
		                 END CATCH
		                 FETCH NEXT FROM udt_cursor INTO @udtSql;
		               END
		               CLOSE udt_cursor;
		               DEALLOCATE udt_cursor;
		               """;

		await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// SQL Server uses <c>INFORMATION_SCHEMA.TABLES</c> (the SQL standard approach) for table existence
	/// checks. The schema defaults to <c>"dbo"</c> (SQL Server's default schema) when not explicitly
	/// provided. Both filter values are sent as query parameters to prevent SQL injection. The
	/// <c>TABLE_TYPE = 'BASE TABLE'</c> filter excludes views.
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

		// SQL Server supports real schemas; default to 'dbo' if none is provided.
		string targetSchema = schema ?? "dbo";

		DbCommand cmd = connection.CreateCommand();
		try
		{
			// INFORMATION_SCHEMA is the SQL standard approach. TABLE_TYPE = 'BASE TABLE' excludes
			// views. Both TABLE_SCHEMA and TABLE_NAME comparison is collation-dependent (typically
			// case-insensitive on default SQL Server installations).
			cmd.CommandText = """
			                  SELECT 1 FROM INFORMATION_SCHEMA.TABLES
			                  WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @tableName AND TABLE_TYPE = 'BASE TABLE'
			                  """;

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
	/// SQL Server supports real schemas (default <c>"dbo"</c>). The checkpoint table is accessed via
	/// a fully schema-qualified identifier (e.g., <c>[dbo].[_restore_checkpoint]</c>). The schema
	/// defaults to <c>"dbo"</c> when not explicitly provided.
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

		// Default to 'dbo', the conventional default schema in SQL Server.
		string targetSchema = schema ?? "dbo";
		bool exists = await TableExistsAsync(connection, tableName, cancellationToken, targetSchema)
			              .ConfigureAwait(false);
		if (!exists)
			return null;

		// Build a fully schema-qualified, bracket-escaped table reference for the SELECT.
		string qualifiedTable = $"{QuoteIdentifier(targetSchema)}.{QuoteIdentifier(tableName)}";

		DbCommand cmd = connection.CreateCommand();
		try
		{
			cmd.CommandText =
				$"""
				 SELECT
				   {QuoteIdentifier("shuttle_id")},
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
	/// SQL Server does not support <c>CREATE TABLE IF NOT EXISTS</c>, so the checkpoint table creation
	/// uses an <c>IF OBJECT_ID(...) IS NULL</c> guard instead. Columns use <c>NVARCHAR</c> types.
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
		// Default to 'dbo' schema; build schema-qualified table reference.
		string targetSchema = schema ?? "dbo";
		string quotedSchema = QuoteIdentifier(targetSchema);
		string quotedTable = QuoteIdentifier(tableName);
		string qt = $"{quotedSchema}.{quotedTable}";

		// Use quoted identifiers for OBJECT_ID check to handle special characters correctly.
		// Escape single quotes in the string literal for the T-SQL N'...' string.
		string objectIdArg = qt.Replace("'", "''");

		// Step 1: Create table (idempotent).
		string createSql =
			$"""
			 IF OBJECT_ID(N'{objectIdArg}', N'U') IS NULL
			 CREATE TABLE {qt} (
			   {QuoteIdentifier("shuttle_id")}            NVARCHAR(100) NOT NULL,
			   {QuoteIdentifier("baseline_migration_id")} NVARCHAR(400) NOT NULL,
			   {QuoteIdentifier("phase")}                 NVARCHAR(50) NOT NULL,
			   {QuoteIdentifier("started_utc")}           NVARCHAR(50) NOT NULL,
			   {QuoteIdentifier("updated_utc")}           NVARCHAR(50) NOT NULL
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
	/// SQL Server checkpoint updates use schema-qualified table references. The schema defaults to
	/// <c>"dbo"</c> when not explicitly provided.
	/// </remarks>
	public async Task UpdateCheckpointPhaseAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		string            phase,
		string            updatedUtc,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// Default to 'dbo' schema; build schema-qualified table reference.
		string targetSchema = schema ?? "dbo";
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
	/// SQL Server uses schema-qualified <c>DROP TABLE IF EXISTS</c> to target the correct schema. The
	/// schema defaults to <c>"dbo"</c> when not explicitly provided.
	/// </remarks>
	public async Task DropCheckpointTableAsync(
		LumaCoreDbContext dbContext,
		string            tableName,
		CancellationToken cancellationToken,
		string?           schema = null)
	{
		// Default to 'dbo' schema; build schema-qualified table reference for the DROP.
		string targetSchema = schema ?? "dbo";
		string qt = $"{QuoteIdentifier(targetSchema)}.{QuoteIdentifier(tableName)}";
		string sql = $"DROP TABLE IF EXISTS {qt}";
		await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public IDataExportReader CreateExportReader(DatabaseOptions options, ILogger logger) => new SqlServerExportReader(
		options.ConnectionString,
		logger,
		options.RequireSnapshotIsolationForExport,
		MapToShuttleStorageType);

	/// <inheritdoc/>
	public IDataImportWriter CreateImportWriter(string connectionString, ILogger logger, TimeProvider timeProvider) =>
		new SqlServerImportWriter(connectionString, timeProvider, logger);

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     SQL Server's type system distinguishes between Unicode (<c>nvarchar</c>, <c>nchar</c>,
	///     <c>ntext</c>) and non-Unicode (<c>varchar</c>, <c>char</c>, <c>text</c>) string types.
	///     All are mapped to <c>TEXT</c> in the Shuttle format. Notable SQL Server-specific mappings:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <c>uniqueidentifier</c> — SQL Server's native GUID type (16 bytes). Mapped to
	///             <c>TEXT</c>.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>bit</c> — SQL Server's boolean equivalent (0 or 1, not a true boolean type).
	///             Mapped to <c>INTEGER</c>.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>datetime2</c> / <c>datetimeoffset</c> — modern date/time types replacing the
	///             deprecated <c>datetime</c>. <c>datetimeoffset</c> includes a UTC offset. Both
	///             mapped to <c>TEXT</c> (ISO 8601).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>money</c> / <c>smallmoney</c> — fixed-precision currency types (8 and 4 bytes
	///             respectively). Mapped to <c>NUMERIC</c>.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <c>image</c> — deprecated binary type (replaced by <c>varbinary(max)</c>). Still
	///             mapped for backward compatibility. Mapped to <c>BLOB</c>.
	///             </description>
	///         </item>
	///     </list>
	/// </remarks>
	public string MapToShuttleStorageType(string providerDbType)
	{
		string lower = providerDbType.ToLowerInvariant();

		// Text types — SQL Server distinguishes Unicode (nvarchar, nchar, ntext) from non-Unicode
		// (varchar, char, text). All are mapped to TEXT. "xml" is a native XML type with built-in
		// validation; stored as TEXT in the Shuttle format. "text" and "ntext" are deprecated
		// (replaced by varchar(max)/nvarchar(max)) but still mapped for backward compatibility.
		if (lower.StartsWith("nvarchar", StringComparison.Ordinal) ||
		    lower.StartsWith("nchar", StringComparison.Ordinal) ||
		    lower.StartsWith("varchar", StringComparison.Ordinal) ||
		    lower.StartsWith("char", StringComparison.Ordinal) ||
		    lower == "text" ||
		    lower == "ntext" ||
		    lower == "xml")
			return "TEXT";

		// Date/time types → ISO 8601 text. "datetime2" replaces the deprecated "datetime" with
		// higher precision (100ns vs 3.33ms). "datetimeoffset" includes a UTC offset.
		// "smalldatetime" is deprecated (1-minute precision, range 1900–2079).
		if (lower.StartsWith("datetime", StringComparison.Ordinal) || // datetime, datetime2
		    lower == "date" ||
		    lower == "time" ||
		    lower == "datetimeoffset" ||
		    lower == "smalldatetime")
			return "TEXT";

		// GUID → text. "uniqueidentifier" is SQL Server's native 16-byte GUID type.
		if (lower == "uniqueidentifier")
			return "TEXT";

		// Integer types. SQL Server's "tinyint" is an unsigned 8-bit integer (0–255).
		if (lower is "int" or "smallint" or "bigint" or "tinyint")
			return "INTEGER";

		// Bit → integer (0/1). SQL Server's "bit" is a boolean-like type that only holds 0 or 1.
		if (lower == "bit")
			return "INTEGER";

		// Floating-point types. SQL Server's "float" defaults to float(53) (8-byte IEEE 754,
		// equivalent to "double precision" in PostgreSQL). "real" is float(24) (4-byte).
		if (lower is "real" or "float")
			return "REAL";

		// Exact numeric types. "money" (8 bytes) and "smallmoney" (4 bytes) are SQL Server
		// fixed-precision currency types — mapped to NUMERIC to preserve exact decimal precision.
		if (lower.StartsWith("decimal", StringComparison.Ordinal) ||
		    lower.StartsWith("numeric", StringComparison.Ordinal) ||
		    lower == "money" ||
		    lower == "smallmoney")
			return "NUMERIC";

		// Binary types. "image" is deprecated (replaced by varbinary(max)) but still mapped for
		// backward compatibility with older databases.
		if (lower.StartsWith("varbinary", StringComparison.Ordinal) ||
		    lower.StartsWith("binary", StringComparison.Ordinal) ||
		    lower == "image")
			return "BLOB";

		// Default fallback — preserves value as text
		return "TEXT";
	}

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     SQL Server error codes are checked via <see cref="SqlException.Number"/>. Errors are grouped
	///     into categories:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description><b>Connection/Network:</b> Server unreachable, connection refused, timeouts</description>
	///         </item>
	///         <item>
	///             <description><b>Resource Exhaustion:</b> Out of memory, disk full, transaction log full</description>
	///         </item>
	///         <item>
	///             <description><b>Database State:</b> Database recovering, suspect, in transition</description>
	///         </item>
	///         <item>
	///             <description><b>Server State:</b> SQL Server paused or shutting down</description>
	///         </item>
	///         <item>
	///             <description><b>Availability Groups:</b> Replica not accessible</description>
	///         </item>
	///         <item>
	///             <description><b>Azure SQL:</b> Service busy, throttling, resource limits</description>
	///         </item>
	///     </list>
	///     <para>
	///     Generic checks include <see cref="TimeoutException"/>, <see cref="SocketException"/>, and
	///     <see cref="EndOfStreamException"/>.
	///     </para>
	///     <para>
	///     Reference:
	///     <see
	///         href="https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors"/>
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

			// SQL Server: SqlException.Number only exposes the first error code. The Errors collection
			// can contain multiple errors from a single server response; the relevant code may not be
			// the first. Iterate all errors to avoid missing infrastructure failures.
			if (current is SqlException sqlEx)
			{
				foreach (SqlError error in sqlEx.Errors)
				{
					if (IsSqlServerConnectionError(error.Number))
						return true;
				}
			}

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
	/// Determines whether the SQL Server error number indicates a connection-related failure.
	/// </summary>
	/// <param name="errorNumber">The <see cref="SqlException.Number"/> value.</param>
	/// <returns><see langword="true"/> if the error indicates the database service is unavailable.</returns>
	private static bool IsSqlServerConnectionError(int errorNumber)
	{
		// @formatter:off
		return errorNumber switch
		{
			// ============================================================
			// Connection / Network Errors
			// ============================================================
			-2    => true, // Timeout expired (client-side)
			-1    => true, // Connection error (general)
			2     => true, // Timeout expired (alternate)
			53    => true, // Named pipe provider: Could not open a connection to SQL Server
			64    => true, // A connection was successfully established, but then an error occurred during the pre-login handshake
			233   => true, // Connection initialization error (no process on the other end of the pipe)
			10050 => true, // Network is down (WSAENETDOWN)
			10051 => true, // Network is unreachable (WSAENETUNREACH)
			10052 => true, // Network dropped connection on reset (WSAENETRESET)
			10053 => true, // Software caused connection abort (WSAECONNABORTED)
			10054 => true, // Connection reset by peer (WSAECONNRESET)
			10057 => true, // Socket is not connected (WSAENOTCONN)
			10060 => true, // Connection timed out (WSAETIMEDOUT)
			10061 => true, // Connection refused (WSAECONNREFUSED)
			10064 => true, // Host is down (WSAEHOSTDOWN)
			10065 => true, // No route to host (WSAEHOSTUNREACH)
			11001 => true, // Host not found (WSAHOST_NOT_FOUND)
			11004 => true, // Valid name, no data record of requested type (WSANO_DATA)

			// ============================================================
			// Resource Exhaustion (Memory / Disk)
			// ============================================================
			701  => true, // There is insufficient system memory in resource pool to run this query
			802  => true, // There is insufficient memory available in the buffer pool
			1105 => true, // Could not allocate space for object in database (filegroup full)
			1121 => true, // Disk full - unable to reserve log space
			9001 => true, // The log for database is not available (log file inaccessible or corrupted)
			9002 => true, // The transaction log for database is full

			// ============================================================
			// Database State (not accessible)
			// ============================================================
			942 => true, // Database cannot be opened because it is offline
			921 => true, // Database has not been recovered yet. Wait and try again
			922 => true, // Database is being recovered. Waiting until recovery is finished
			924 => true, // Database is marked SUSPECT (could not recover)
			926 => true, // Database cannot be opened. It has been marked SUSPECT by recovery
			927 => true, // Database cannot be opened. It is in the middle of a restore
			928 => true, // Database cannot be opened during upgrade
			930 => true, // Database cannot be opened because some files could not be recovered
			945 => true, // Database cannot be opened due to inaccessible files or insufficient memory/disk
			952 => true, // Database is in transition. Try the statement later

			// ============================================================
			// Server State (paused / shutting down)
			// ============================================================
			17142 => true, // SQL Server service has been paused. No new connections will be allowed
			17143 => true, // SQL Server is terminating because of a system shutdown

			// ============================================================
			// Availability Groups / Mirroring
			// ============================================================
			976   => true, // Target database is participating in availability group and not accessible for queries
			983   => true, // Unable to access availability database because replica is not PRIMARY or SECONDARY
			1404  => true, // Database mirroring partner is not reachable
			1418  => true, // Server network address cannot be reached or does not exist
			4060  => true, // Cannot open database requested by the login (dropped, renamed, or Azure geo-failover)
			4221  => true, // Login to read-secondary failed due to long wait on HADR synchronization
			35250 => true, // HADR_PRIMARYNOTACTIVE: Connection to the primary replica is not active

			// ============================================================
			// Azure SQL Database Specific
			// ============================================================
			10928 => true, // Resource limit reached (DTU/vCore limits for database)
			10929 => true, // Resource minimum guarantee exceeded (too many concurrent requests)
			10936 => true, // Elastic Pool request limit reached (DTU/eDTU limits for pool)
			40143 => true, // Connection could not be initialized
			40197 => true, // The service has encountered an error processing your request
			40501 => true, // The service is currently busy. Retry the request after 10 seconds
			40532 => true, // Cannot process request. Too many concurrent requests for the elastic pool
			40540 => true, // The database has reached its size quota (premium/hyperscale)
			40544 => true, // The database has reached its size quota
			40549 => true, // Session terminated: long-running transaction
			40550 => true, // Session terminated: too many locks acquired
			40551 => true, // Session terminated: excessive TEMPDB usage
			40552 => true, // Session terminated: excessive transaction log space usage
			40553 => true, // Session terminated: excessive memory usage
			40613 => true, // Database on server is not currently available
			40615 => true, // Cannot connect to server (Azure firewall)
			40627 => true, // Operation on server/database is in progress. Please wait a few minutes
			40642 => true, // The server is currently too busy
			40671 => true, // Unable to communicate between the gateway and the management service
			40675 => true, // The gateway is not responding
			40914 => true, // Cannot open server requested by the login
			45168 => true, // SQL Azure system under load, placing upper limits on concurrent operations
			45169 => true, // SQL Azure system is currently busy
			49918 => true, // Cannot process request. Not enough resources to process request
			49919 => true, // Cannot process create or update request. Too many create/update operations in progress
			49920 => true, // Cannot process request. Too many operations in progress for subscription

			var _ => false
		};
		// @formatter:on
	}

	/// <summary>
	/// Wraps a value as a T-SQL Unicode string literal with proper escaping.
	/// </summary>
	/// <param name="value">The raw string value.</param>
	/// <returns>A safely quoted literal, e.g. <c>N'foo''bar'</c> for input <c>foo'bar</c>.</returns>
	/// <remarks>
	/// Used exclusively inside the T-SQL schema cleanup script (see <see cref="DropSchemaObjectsAsync"/>),
	/// where values are interpolated into dynamic SQL built via <c>SELECT @sql += ...</c>. The
	/// <c>N</c> prefix ensures Unicode string handling. T-SQL string literals use single-quote doubling
	/// as the escape mechanism (<c>'</c> → <c>''</c>).
	/// </remarks>
	private static string EscapeTSqlStringLiteral(string value) => $"N'{value.Replace("'", "''")}'";
}
