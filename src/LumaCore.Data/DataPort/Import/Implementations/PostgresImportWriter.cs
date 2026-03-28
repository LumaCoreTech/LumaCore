// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;
using System.Text;

using LumaCore.Data.DataPort.Models;

using Microsoft.Extensions.Logging;

using Npgsql;

using static LumaCore.Data.DataPort.SqlIdentifierHelper;

namespace LumaCore.Data.DataPort.Import.Implementations;

/// <summary>
/// Implements the <see cref="IDataImportWriter"/> for PostgreSQL.
/// </summary>
/// <remarks>
///     <para>
///     This importer uses the high-performance PostgreSQL COPY command (via <see cref="NpgsqlBinaryImporter"/>)
///     for data insertion. It manages constraints and sequence resetting for a complete, safe, and fast import.
///     </para>
///     <para>
///     All operations are scoped to a single PostgreSQL schema (default: <c>public</c>). The schema is
///     specified at construction time and affects table truncation, data import, sequence resets,
///     migration history lookups, and the internal checkpoint table.
///     </para>
///     <para>
///     Data is imported in chunks of <see cref="DataPortTuning.ImportChunkSizeRows"/> rows. Each chunk
///     is committed in its own transaction alongside a checkpoint update to the
///     <c>_shuttle_import_checkpoint</c> table, enabling crash-safe resume.
///     </para>
///     <para>
///     <b>Note on COPY chunking:</b> Each chunk requires its own
///     <see cref="NpgsqlBinaryImporter.CompleteAsync"/> / transaction cycle, which adds overhead compared
///     to a single COPY for the entire table. This is an intentional trade-off: the restore path
///     prioritizes correctness and resumability over maximum throughput.
///     </para>
/// </remarks>
public sealed class PostgresImportWriter : IDataImportWriter
{
	/// <summary>
	/// Name of the temporary checkpoint table created in the target database during import.
	/// </summary>
	private const string CheckpointTableName = "_shuttle_import_checkpoint";

	private readonly string            mConnectionString;
	private readonly string            mSchema;
	private readonly TimeProvider      mTimeProvider;
	private readonly ILogger?          mLogger;
	private          NpgsqlConnection? mConnection;
	private          string?           mShuttleId;
	private          bool              mDisposed;

	/// <summary>
	/// Checkpoint data loaded during <see cref="PrepareForImportAsync"/>. Maps table name to the number of
	/// chunks already committed and the total number of rows imported for that table.
	/// </summary>
	private Dictionary<string, (int ChunksCompleted, long TotalRowsImported)>? mCheckpoints;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresImportWriter"/> class.
	/// </summary>
	/// <param name="connectionString">The connection string for the target Postgres database.</param>
	/// <param name="schema">
	/// The PostgreSQL schema to import tables into. Defaults to <c>public</c>, which is the default schema
	/// used by EF Core's Npgsql provider. Since PostgreSQL 15, unprivileged users no longer have
	/// <c>CREATE</c> rights on the <c>public</c> schema by default — specify the application schema
	/// explicitly when the database uses a non-default schema.
	/// </param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <param name="logger">
	/// An optional logger for diagnostic messages (e.g., checkpoint mismatch warnings).
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="connectionString"/>, <paramref name="schema"/>, or <paramref name="timeProvider"/>
	/// is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="connectionString"/> or <paramref name="schema"/> is empty or consists only of
	/// white-space characters.
	/// </exception>
	public PostgresImportWriter(
		string        connectionString,
		string        schema       = "public",
		TimeProvider? timeProvider = null,
		ILogger?      logger       = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		mConnectionString = connectionString;
		mSchema = schema;
		mTimeProvider = timeProvider ?? TimeProvider.System;
		mLogger = logger;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		mDisposed = true;

		if (mConnection != null)
		{
			await mConnection.DisposeAsync().ConfigureAwait(false);
			mConnection = null;
		}
	}

	/// <inheritdoc/>
	public Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();

		// Prevent re-initialization.
		if (mConnection != null)
			throw new InvalidOperationException("Importer has already been initialized");

		mConnection = new NpgsqlConnection(mConnectionString);
		return mConnection.OpenAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		var migrations = new List<MigrationInfo>();

		// Check if the migrations table exists before querying it.
		var checkCmd = new NpgsqlCommand(
			"""
			SELECT EXISTS (
				SELECT FROM information_schema.tables
				WHERE table_schema = @schema
				  AND table_name = '__EFMigrationsHistory'
			)
			""",
			mConnection);
		checkCmd.Parameters.AddWithValue("schema", mSchema);
		try
		{
			bool? exists = (bool?)await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (exists != true)
				return migrations;
		}
		finally
		{
			await checkCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Read migration history.
		var cmd = new NpgsqlCommand(
			$"""
			 SELECT "MigrationId", "ProductVersion"
			 FROM {QuotePostgres(mSchema)}."__EFMigrationsHistory"
			 ORDER BY "MigrationId"
			 """,
			mConnection);
		try
		{
			NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					migrations.Add(new MigrationInfo(reader.GetString(0), reader.GetString(1)));
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}

			return migrations;
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task PrepareForImportAsync(string shuttleId, CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		ArgumentException.ThrowIfNullOrWhiteSpace(shuttleId);
		mShuttleId = shuttleId;

		// Disable all foreign key checks and triggers for this session.
		// This is a session-level setting that persists across transactions.
		var cmd = new NpgsqlCommand("SET session_replication_role = 'replica';", mConnection);
		try
		{
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		// Create checkpoint table idempotently.
		await CreateCheckpointTableAsync(cancellationToken).ConfigureAwait(false);

		// Load and validate existing checkpoints.
		mCheckpoints = await ReadAndValidateCheckpointsAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task ImportTableAsync(
		TableSnapshot                      table,
		ILogger?                           logger,
		IProgress<DataPortProgressReport>? progress,
		int                                currentTable,
		int                                totalTables,
		CancellationToken                  cancellationToken = default)
	{
		ThrowIfNotPrepared();

		int chunkSize = DataPortTuning.ImportChunkSizeRows;
		string overallMsg = $"Importing '{table.Name}' ({currentTable}/{totalTables})...";
		long estimatedRows = table.EstimatedRowCount;

		// Check if we have a checkpoint for this table (resume scenario).
		int chunksCompleted = 0;
		long rowsAlreadyImported = 0;

		if (mCheckpoints != null && mCheckpoints.TryGetValue(
			    table.Name,
			    out (int ChunksCompleted, long TotalRowsImported) checkpoint))
		{
			chunksCompleted = checkpoint.ChunksCompleted;
			rowsAlreadyImported = checkpoint.TotalRowsImported;
			logger?.LogInformation(
				"Resuming import of {TableName} from chunk {ChunkNumber} ({RowCount} rows already imported)",
				table.Name,
				chunksCompleted + 1,
				rowsAlreadyImported);
		}
		else
		{
			// Fresh import for this table: delete existing data.
			// TRUNCATE ... CASCADE is not safe here because tables are imported in alphabetical
			// order — CASCADE propagates to tables that have FK references to the truncated table,
			// wiping data that was already imported in earlier iterations (e.g., TRUNCATE Users
			// CASCADE wipes UserRoles). DELETE FROM is safe because session_replication_role is set
			// to 'replica', which disables FK constraint triggers. Sequence resets are handled
			// separately in CleanupAfterImportAsync().
			try
			{
				string deleteSql = $"DELETE FROM {QuotePostgres(mSchema)}.{QuotePostgres(table.Name)}";
				var deleteCmd = new NpgsqlCommand(deleteSql, mConnection);
				try
				{
					await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					await deleteCmd.DisposeAsync().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, "Failed to delete data from table {TableName}", table.Name);
				throw;
			}
		}

		// Build the COPY command for this table.
		string columnList = string.Join(", ", table.Columns.Select(c => QuotePostgres(c.Name)));
		string copyCommand = $"""
		                      COPY {QuotePostgres(mSchema)}.{QuotePostgres(table.Name)} ({columnList})
		                      FROM STDIN (FORMAT BINARY)
		                      """;

		// Build per-column type mapping for shuttle → PostgreSQL type conversion.
		// The shuttle file stores all values as SQLite native types (string for TEXT, long for
		// INTEGER, double for REAL, byte[] for BLOB). Binary COPY requires CLR types that match
		// the target PG column types (e.g., DateTime for timestamptz, Guid for uuid).
		string?[] pgColumnTypes = await GetTargetColumnTypesAsync(table, cancellationToken).ConfigureAwait(false);

		long totalRowCount = rowsAlreadyImported;
		long rowsToSkip = rowsAlreadyImported;
		int currentChunk = chunksCompleted;
		int rowsInCurrentChunk = 0;

		// Per-chunk transaction and binary importer — created lazily when the first row of a chunk arrives.
		NpgsqlTransaction? chunkTransaction = null;
		NpgsqlBinaryImporter? importer = null;

		try
		{
			await foreach (object?[] row in table.Rows.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				// Skip already-imported rows (resume scenario).
				if (rowsToSkip > 0)
				{
					rowsToSkip--;
					continue;
				}

				// Start a new chunk transaction + COPY session if needed.
				if (chunkTransaction == null)
				{
					chunkTransaction =
						await mConnection!.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
					importer = await mConnection
						           .BeginBinaryImportAsync(copyCommand, cancellationToken)
						           .ConfigureAwait(false);
				}

				// Write the row via binary COPY, converting shuttle types to PostgreSQL types.
				await importer!.StartRowAsync(cancellationToken).ConfigureAwait(false);
				for (int col = 0; col < row.Length; col++)
				{
					object? converted = ConvertShuttleValue(row[col], pgColumnTypes[col]);
					await importer.WriteAsync(converted ?? DBNull.Value, cancellationToken).ConfigureAwait(false);
				}

				rowsInCurrentChunk++;
				totalRowCount++;

				// Commit chunk when full.
				if (rowsInCurrentChunk >= chunkSize)
				{
					currentChunk++;

					// Complete the COPY operation before the checkpoint update.
					await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
					await importer.DisposeAsync().ConfigureAwait(false);
					importer = null;

					await UpdateCheckpointAsync(
							chunkTransaction,
							table.Name,
							currentChunk,
							totalRowCount,
							cancellationToken)
						.ConfigureAwait(false);
					await chunkTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);

					await chunkTransaction.DisposeAsync().ConfigureAwait(false);
					chunkTransaction = null;
					rowsInCurrentChunk = 0;

					// Report progress.
					progress?.Report(
						new DataPortProgressReport
						{
							OverallMessage = overallMsg,
							OverallCurrentStep = currentTable,
							OverallTotalSteps = totalTables,
							DetailedMessage = $"{totalRowCount:N0} rows processed",
							DetailedCurrentStep = totalRowCount,
							DetailedTotalSteps = estimatedRows > 0 ? estimatedRows : null
						});
				}
			}

			// Commit remaining rows in the last (partial) chunk.
			if (rowsInCurrentChunk > 0 && chunkTransaction != null && importer != null)
			{
				currentChunk++;
				await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
				await importer.DisposeAsync().ConfigureAwait(false);
				importer = null;

				await UpdateCheckpointAsync(
						chunkTransaction,
						table.Name,
						currentChunk,
						totalRowCount,
						cancellationToken)
					.ConfigureAwait(false);
				await chunkTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			if (importer != null) await importer.DisposeAsync().ConfigureAwait(false);
			if (chunkTransaction != null) await chunkTransaction.DisposeAsync().ConfigureAwait(false);
		}

		// Final progress report.
		progress?.Report(
			new DataPortProgressReport
			{
				OverallMessage = overallMsg,
				OverallCurrentStep = currentTable,
				OverallTotalSteps = totalTables,
				DetailedMessage = $"{totalRowCount:N0} rows processed",
				DetailedCurrentStep = totalRowCount,
				DetailedTotalSteps = estimatedRows > 0 ? estimatedRows : null
			});

		logger?.LogDebug("Imported {ImportedRowCount} rows into {TableName}", totalRowCount, table.Name);
	}

	/// <inheritdoc/>
	public async Task CleanupAfterImportAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		// 1. Re-enable triggers and foreign key checks.
		var cmd = new NpgsqlCommand("SET session_replication_role = 'origin';", mConnection);
		try
		{
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		// 2. CRITICAL: Reset all auto-increment sequences.
		var sequences = new List<(string Sequence, string Table, string Column)>();
		var seqCmd = new NpgsqlCommand(
			"""
			SELECT
				seq.relname AS "SequenceName",
				tbl.relname AS "TableName",
				col.attname AS "ColumnName"
			FROM pg_class seq
			JOIN pg_namespace ns ON ns.oid = seq.relnamespace
			JOIN pg_depend dep ON dep.objid = seq.oid AND dep.classid = 'pg_class'::regclass
			JOIN pg_class tbl ON tbl.oid = dep.refobjid AND dep.refclassid = 'pg_class'::regclass
			JOIN pg_attribute col ON col.attrelid = tbl.oid AND col.attnum = dep.refobjsubid
			WHERE seq.relkind = 'S' AND dep.deptype = 'a'
			  AND ns.nspname = @schema
			""",
			mConnection);
		seqCmd.Parameters.AddWithValue("schema", mSchema);
		try
		{
			NpgsqlDataReader reader = await seqCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					sequences.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await seqCmd.DisposeAsync().ConfigureAwait(false);
		}

		var sb = new StringBuilder();
		foreach ((string _, string table, string col) in sequences)
		{
			string escapedSchema = mSchema.Replace("'", "''");
			string escapedTable = table.Replace("'", "''");
			string escapedCol = col.Replace("'", "''");
			string qualifiedTable = $"{QuotePostgres(mSchema)}.{QuotePostgres(table)}";
			string quotedCol = QuotePostgres(col);

			// pg_get_serial_sequence() expects a single-quoted string with double-quoted identifiers inside,
			// e.g., '"myschema"."mytable"' — this identifies the table that owns the sequence.
			string seqTableArg = $"'\"{escapedSchema}\".\"{escapedTable}\"'";

			// Use MAX(col) IS NOT NULL as the is_called parameter:
			// - Table has rows: setval(seq, max, true)  → next nextval() returns max + 1
			// - Empty table:    setval(seq, 1,   false) → next nextval() returns 1
			sb.AppendLine(
				$"SELECT setval(pg_get_serial_sequence({seqTableArg}, '{escapedCol}'), " +
				$"COALESCE(MAX({quotedCol}), 1), " +
				$"MAX({quotedCol}) IS NOT NULL) FROM {qualifiedTable};");
		}

		if (sb.Length > 0)
		{
			var resetCmd = new NpgsqlCommand(sb.ToString(), mConnection);
			try
			{
				await resetCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				await resetCmd.DisposeAsync().ConfigureAwait(false);
			}
		}

		// 3. Drop the checkpoint table — import completed successfully.
		await DropCheckpointTableAsync(cancellationToken).ConfigureAwait(false);
	}

	// --- Private Shuttle → PostgreSQL Type Conversion ---

	/// <summary>
	/// Queries <c>information_schema.columns</c> for the actual PostgreSQL data types of the target
	/// table and returns them as an array aligned with <see cref="TableSnapshot.Columns"/>.
	/// </summary>
	/// <param name="table">The table snapshot whose columns define the expected order.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// An array of PostgreSQL <c>data_type</c> strings (e.g., <c>"timestamp with time zone"</c>,
	/// <c>"uuid"</c>, <c>"boolean"</c>), one per column in <paramref name="table"/>.
	/// Elements are <see langword="null"/> when the column is not found in the target schema.
	/// </returns>
	private async Task<string?[]> GetTargetColumnTypesAsync(
		TableSnapshot     table,
		CancellationToken cancellationToken)
	{
		var pgTypes = new Dictionary<string, string>(StringComparer.Ordinal);

		var cmd = new NpgsqlCommand(
			"""
			SELECT column_name, data_type
			FROM information_schema.columns
			WHERE table_schema = @schema AND table_name = @table
			""",
			mConnection);
		cmd.Parameters.AddWithValue("schema", mSchema);
		cmd.Parameters.AddWithValue("table", table.Name);
		try
		{
			NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					pgTypes[reader.GetString(0)] = reader.GetString(1);
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

		// Build an array aligned with the table's column order.
		string?[] result = new string?[table.Columns.Count];
		for (int i = 0; i < table.Columns.Count; i++)
		{
			pgTypes.TryGetValue(table.Columns[i].Name, out result[i]);
		}

		return result;
	}

	/// <summary>
	/// Converts a raw value read from the SQLite shuttle file into the CLR type expected by
	/// PostgreSQL binary COPY for the given target column type.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The shuttle file stores all data using SQLite native types: <see cref="string"/> for
	///     <c>TEXT</c> columns (timestamps, UUIDs, intervals), <see cref="long"/> for <c>INTEGER</c>
	///     columns (booleans, ints, bigints), <see cref="double"/> for <c>REAL</c>, and
	///     <c>byte[]</c> for <c>BLOB</c>. PostgreSQL binary COPY requires the CLR type to
	///     match the target column type exactly.
	///     </para>
	///     <para>
	///     When the value is already the correct CLR type, or the PostgreSQL type is unknown, the
	///     value is returned unchanged (passthrough).
	///     </para>
	/// </remarks>
	/// <param name="value">The raw shuttle value, or <see langword="null"/> for SQL NULL.</param>
	/// <param name="pgDataType">
	/// The PostgreSQL <c>data_type</c> from <c>information_schema.columns</c>
	/// (e.g., <c>"timestamp with time zone"</c>, <c>"uuid"</c>, <c>"boolean"</c>), or
	/// <see langword="null"/> when the column type is unknown.
	/// </param>
	/// <returns>
	/// The converted value suitable for <see cref="NpgsqlBinaryImporter"/>.<c>WriteAsync</c>.
	/// </returns>
	private static object? ConvertShuttleValue(object? value, string? pgDataType)
	{
		if (value is null)
			return null;

		return pgDataType switch
		{
			"uuid" when value is byte[] b =>
				new Guid(b),
			"timestamp with time zone" when value is string s =>
				DateTime.SpecifyKind(DateTime.Parse(s, CultureInfo.InvariantCulture), DateTimeKind.Utc),
			"timestamp without time zone" when value is string s =>
				DateTime.Parse(s, CultureInfo.InvariantCulture),
			"date" when value is string s =>
				DateOnly.Parse(s, CultureInfo.InvariantCulture),
			"time without time zone" or "time with time zone" when value is string s =>
				TimeOnly.Parse(s, CultureInfo.InvariantCulture),
			"interval" when value is string s =>
				TimeSpan.Parse(s, CultureInfo.InvariantCulture),
			"uuid" when value is string s =>
				Guid.Parse(s),
			"boolean" when value is long l =>
				l != 0,
			"smallint" when value is long l =>
				(short)l,
			"integer" when value is long l =>
				(int)l,
			"real" when value is double d =>
				(float)d,
			var _ => value
		};
	}

	// --- Private Checkpoint Helpers ---

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if the importer has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The importer has been disposed.</exception>
	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(mDisposed, this);

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if the importer has been disposed, or
	/// <see cref="InvalidOperationException"/> if it has not been initialized.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The importer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The importer is not initialized.</exception>
	private void ThrowIfNotInitialized()
	{
		ThrowIfDisposed();

		if (mConnection is null)
			throw new InvalidOperationException("Importer is not initialized");
	}

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if the importer has been disposed, or
	/// <see cref="InvalidOperationException"/> if it has not been initialized and prepared.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The importer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The importer is not initialized or not prepared.</exception>
	private void ThrowIfNotPrepared()
	{
		ThrowIfDisposed();

		if (mConnection is null || mShuttleId is null)
			throw new InvalidOperationException("Importer is not initialized or not prepared.");
	}

	/// <summary>
	/// Creates the checkpoint table idempotently in the target database.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	private async Task CreateCheckpointTableAsync(CancellationToken cancellationToken)
	{
		var cmd = new NpgsqlCommand(
			$"""
			 CREATE TABLE IF NOT EXISTS {QuotePostgres(mSchema)}.{QuotePostgres(CheckpointTableName)} (
			  "shuttle_id"          TEXT    NOT NULL,
			  "table_name"          TEXT    NOT NULL,
			  "chunks_completed"    INTEGER NOT NULL,
			  "total_rows_imported" BIGINT  NOT NULL,
			  "started_utc"         TEXT    NOT NULL,
			  "updated_utc"         TEXT    NOT NULL,
			  PRIMARY KEY ("shuttle_id", "table_name")
			 )
			 """,
			mConnection);
		try
		{
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Reads existing checkpoint records and validates them against the current shuttle ID.
	/// If a shuttle-ID mismatch is detected, the checkpoint table is dropped and recreated.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A dictionary mapping table names to their checkpoint state, or an empty dictionary for a fresh import.
	/// </returns>
	private async Task<Dictionary<string, (int ChunksCompleted, long TotalRowsImported)>>
		ReadAndValidateCheckpointsAsync(CancellationToken cancellationToken)
	{
		var result = new Dictionary<string, (int, long)>();

		// Check for rows with a different shuttle ID (mismatch scenario).
		var mismatchCmd = new NpgsqlCommand(
			$"""
			 SELECT DISTINCT "shuttle_id"
			 FROM {QuotePostgres(mSchema)}.{QuotePostgres(CheckpointTableName)}
			 WHERE "shuttle_id" != @shuttleId
			 LIMIT 1
			 """,
			mConnection);
		try
		{
			mismatchCmd.Parameters.AddWithValue("@shuttleId", mShuttleId!);
			object? mismatch = await mismatchCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

			if (mismatch != null && mismatch != DBNull.Value)
			{
				mLogger?.LogWarning(
					"Import checkpoint references a different shuttle file (found: {FoundShuttleId}, expected: {ExpectedShuttleId}) — " +
					"discarding checkpoint and restarting from scratch",
					mismatch,
					mShuttleId);

				await DropCheckpointTableAsync(cancellationToken).ConfigureAwait(false);
				await CreateCheckpointTableAsync(cancellationToken).ConfigureAwait(false);
				return result;
			}
		}
		finally
		{
			await mismatchCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Read matching checkpoints.
		var readCmd = new NpgsqlCommand(
			$"""
			 SELECT "table_name", "chunks_completed", "total_rows_imported"
			 FROM {QuotePostgres(mSchema)}.{QuotePostgres(CheckpointTableName)}
			 WHERE "shuttle_id" = @shuttleId
			 """,
			mConnection);
		try
		{
			readCmd.Parameters.AddWithValue("@shuttleId", mShuttleId!);

			NpgsqlDataReader reader =
				await readCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					result[reader.GetString(0)] = (reader.GetInt32(1), reader.GetInt64(2));
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await readCmd.DisposeAsync().ConfigureAwait(false);
		}

		if (result.Count > 0)
		{
			mLogger?.LogInformation(
				"Found checkpoint data for {CheckpointCount} table(s). Resuming import",
				result.Count);
		}

		return result;
	}

	/// <summary>
	/// Upserts the checkpoint record for a table within the given chunk transaction.
	/// </summary>
	/// <param name="transaction">The active chunk transaction.</param>
	/// <param name="tableName">The table being imported.</param>
	/// <param name="chunksCompleted">Total number of chunks completed so far.</param>
	/// <param name="totalRowsImported">Total number of rows imported so far.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	private async Task UpdateCheckpointAsync(
		NpgsqlTransaction transaction,
		string            tableName,
		int               chunksCompleted,
		long              totalRowsImported,
		CancellationToken cancellationToken)
	{
		var cmd = new NpgsqlCommand(
			$"""
			 INSERT INTO {QuotePostgres(mSchema)}.{QuotePostgres(CheckpointTableName)}
			     ("shuttle_id", "table_name", "chunks_completed", "total_rows_imported", "started_utc", "updated_utc")
			 VALUES (@shuttleId, @tableName, @chunks, @rows, @now, @now)
			 ON CONFLICT ("shuttle_id", "table_name") DO UPDATE SET
			     "chunks_completed"    = @chunks,
			     "total_rows_imported" = @rows,
			     "updated_utc"         = @now
			 """,
			mConnection,
			transaction);
		try
		{
			cmd.Parameters.AddWithValue("@shuttleId", mShuttleId!);
			cmd.Parameters.AddWithValue("@tableName", tableName);
			cmd.Parameters.AddWithValue("@chunks", chunksCompleted);
			cmd.Parameters.AddWithValue("@rows", totalRowsImported);
			cmd.Parameters.AddWithValue("@now", mTimeProvider.GetUtcNow().UtcDateTime.ToString("O"));
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Drops the checkpoint table from the target database.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	private async Task DropCheckpointTableAsync(CancellationToken cancellationToken)
	{
		var cmd = new NpgsqlCommand(
			$"DROP TABLE IF EXISTS {QuotePostgres(mSchema)}.{QuotePostgres(CheckpointTableName)}",
			mConnection);
		try
		{
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}
}
