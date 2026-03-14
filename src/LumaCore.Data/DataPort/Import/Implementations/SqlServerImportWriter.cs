// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;
using System.Globalization;

using LumaCore.Data.DataPort.Models;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using static LumaCore.Data.DataPort.SqlIdentifierHelper;

namespace LumaCore.Data.DataPort.Import.Implementations;

/// <summary>
/// Implements the <see cref="IDataImportWriter"/> for SQL Server.
/// </summary>
/// <remarks>
///     <para>
///     This importer uses <see cref="SqlBulkCopy"/> for high-performance data insertion.
///     It is responsible for handling <c>IDENTITY_INSERT</c>, disabling/enabling
///     constraints, and resetting identity seed values after import.
///     </para>
///     <para>
///     Data is imported in chunks of <see cref="DataPortTuning.ImportChunkSizeRows"/> rows. Each chunk
///     is committed in its own transaction alongside a checkpoint update to the
///     <c>_shuttle_import_checkpoint</c> table, enabling crash-safe resume.
///     </para>
/// </remarks>
public sealed class SqlServerImportWriter : IDataImportWriter
{
	/// <summary>
	/// Name of the temporary checkpoint table created in the target database during import.
	/// </summary>
	private const string CheckpointTableName = "_shuttle_import_checkpoint";

	private readonly string         mConnectionString;
	private readonly TimeProvider   mTimeProvider;
	private readonly ILogger?       mLogger;
	private          SqlConnection? mConnection;
	private          string?        mShuttleId;
	private          bool           mDisposed;

	/// <summary>
	/// Stores the names of all tables that have an identity column.
	/// We query this once and cache it for use during <see cref="ImportTableAsync"/>.
	/// </summary>
	private HashSet<string> mIdentityTables = [];

	/// <summary>
	/// Checkpoint data loaded during <see cref="PrepareForImportAsync"/>. Maps table name to the number of
	/// chunks already committed and the total number of rows imported for that table.
	/// </summary>
	private Dictionary<string, (int ChunksCompleted, long TotalRowsImported)>? mCheckpoints;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerImportWriter"/> class.
	/// </summary>
	/// <param name="connectionString">The connection string for the target SQL Server database.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <param name="logger">
	/// An optional logger for diagnostic messages (e.g., checkpoint mismatch warnings).
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="connectionString"/> or <paramref name="timeProvider"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="connectionString"/> is empty or consists only of white-space characters.
	/// </exception>
	public SqlServerImportWriter(string connectionString, TimeProvider timeProvider, ILogger? logger = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(timeProvider);
		mConnectionString = connectionString;
		mTimeProvider = timeProvider;
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
	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();

		// Prevent re-initialization.
		if (mConnection != null)
			throw new InvalidOperationException("Importer has already been initialized");

		mConnection = new SqlConnection(mConnectionString);
		await mConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Cache identity column information.
		await CacheIdentityTableNamesAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		var migrations = new List<MigrationInfo>();

		// Check if the migrations table exists before querying it.
		var checkCmd = new SqlCommand(
			"SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory'",
			mConnection);
		try
		{
			object? exists = await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (exists is null || exists == DBNull.Value)
				return migrations;
		}
		finally
		{
			await checkCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Read migration history.
		var cmd = new SqlCommand(
			"""
			SELECT [MigrationId], [ProductVersion]
			FROM [__EFMigrationsHistory]
			ORDER BY [MigrationId]
			""",
			mConnection);
		try
		{
			SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

		// Disable ALL foreign key constraints in the database.
		var cmd = new SqlCommand(
			"EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'",
			mConnection);
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

		bool hasIdentity = mIdentityTables.Contains(table.Name);
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
			// SQL Server does not allow TRUNCATE TABLE when any FK constraint references the table,
			// even if the constraint is disabled via NOCHECK. DELETE FROM works regardless of FK
			// existence. Identity seed reset is handled in CleanupAfterImportAsync().
			try
			{
				string deleteSql = $"DELETE FROM {QuoteSqlServer(table.Name)}";
				var deleteCmd = new SqlCommand(deleteSql, mConnection);
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

		// Enable IDENTITY_INSERT if necessary.
		if (hasIdentity)
			await SetIdentityInsertAsync(table.Name, true, cancellationToken).ConfigureAwait(false);

		// Build per-column type mapping for shuttle → SQL Server type conversion.
		// The shuttle file stores all values as SQLite native types (string for TEXT, long for
		// INTEGER, double for REAL, byte[] for BLOB). SqlBulkCopy cannot convert all of these
		// automatically (e.g., string → uniqueidentifier fails).
		string?[] sqlServerTypes = await GetTargetColumnTypesAsync(table, cancellationToken)
			                           .ConfigureAwait(false);

		long totalRowCount = rowsAlreadyImported;
		long rowsToSkip = rowsAlreadyImported;
		int currentChunk = chunksCompleted;

		// We need to chunk the row stream for SqlBulkCopy. The ChunkedTableSnapshotDataReader
		// wraps the IAsyncEnumerable and yields at most chunkSize rows per Read() cycle.
		IAsyncEnumerator<object?[]> enumerator = table.Rows.GetAsyncEnumerator(cancellationToken);
		try
		{
			// Skip already-imported rows (resume scenario).
			while (rowsToSkip > 0)
			{
				if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
					break;

				rowsToSkip--;
			}

			// Import in chunks.
			bool hasMore = true;
			while (hasMore)
			{
				// Begin a new chunk transaction.
				var chunkTransaction = (SqlTransaction)await mConnection!
					                                       .BeginTransactionAsync(cancellationToken)
					                                       .ConfigureAwait(false);

				try
				{
					// Create a chunked reader that reads at most chunkSize rows from the enumerator.
					using var chunkReader = new ChunkedTableSnapshotDataReader(
						table,
						enumerator,
						chunkSize,
						sqlServerTypes);

					using (var bulkCopy = new SqlBulkCopy(mConnection, SqlBulkCopyOptions.Default, chunkTransaction))
					{
						bulkCopy.DestinationTableName = table.Name;
						bulkCopy.BatchSize = chunkSize;

						foreach (ColumnDefinition col in table.Columns)
						{
							bulkCopy.ColumnMappings.Add(col.Name, col.Name);
						}

						await bulkCopy.WriteToServerAsync(chunkReader, cancellationToken).ConfigureAwait(false);
					}

					int rowsInChunk = chunkReader.RowsRead;
					hasMore = !chunkReader.SourceExhausted;

					// Only commit if we actually imported rows.
					if (rowsInChunk > 0)
					{
						totalRowCount += rowsInChunk;
						currentChunk++;

						await UpdateCheckpointAsync(
								chunkTransaction,
								table.Name,
								currentChunk,
								totalRowCount,
								cancellationToken)
							.ConfigureAwait(false);

						await chunkTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);

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
					else
					{
						// No rows — nothing to commit.
						await chunkTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
						hasMore = false;
					}
				}
				catch
				{
					await chunkTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
					throw;
				}
				finally
				{
					await chunkTransaction.DisposeAsync().ConfigureAwait(false);
				}
			}
		}
		finally
		{
			await enumerator.DisposeAsync().ConfigureAwait(false);
		}

		// Disable IDENTITY_INSERT.
		if (hasIdentity)
			await SetIdentityInsertAsync(table.Name, false, cancellationToken).ConfigureAwait(false);

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

		// 1. Re-enable all constraints and TRUST them.
		var cmd = new SqlCommand(
			"EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'",
			mConnection);
		try
		{
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		// 2. CRITICAL: Reset all identity seeds.
		foreach (string tableName in mIdentityTables)
		{
			string findIdentityColSql =
				"""
				SELECT name
				FROM sys.identity_columns
				WHERE object_id = OBJECT_ID(@tableName)
				""";

			string? idColName;
			var idCmd = new SqlCommand(findIdentityColSql, mConnection);
			try
			{
				idCmd.Parameters.AddWithValue("@tableName", tableName);
				idColName = (string?)await idCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				await idCmd.DisposeAsync().ConfigureAwait(false);
			}

			if (!string.IsNullOrEmpty(idColName))
			{
				string quotedTable = QuoteSqlServer(tableName);
				string quotedCol = QuoteSqlServer(idColName);
				string robustReseedQuery =
					$"""
					 DECLARE @maxId BIGINT;
					 EXEC sp_executesql
					 N'SELECT @maxId_out = COALESCE(MAX({quotedCol}), 0) FROM {quotedTable}',
					 N'@maxId_out BIGINT OUTPUT',
					 @maxId_out = @maxId OUTPUT;
					 DBCC CHECKIDENT ('{quotedTable}', RESEED, @maxId);
					 """;

				var reseedCmd = new SqlCommand(robustReseedQuery, mConnection);
				try
				{
					await reseedCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					await reseedCmd.DisposeAsync().ConfigureAwait(false);
				}
			}
		}

		// 3. Drop the checkpoint table — import completed successfully.
		await DropCheckpointTableAsync(cancellationToken).ConfigureAwait(false);
	}

	// --- Private Helper Methods ---

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
	/// Toggles <c>IDENTITY_INSERT</c> for a specific table.
	/// </summary>
	/// <param name="tableName">The name of the table.</param>
	/// <param name="enable"><see langword="true"/> to enable, <see langword="false"/> to disable.</param>
	/// <param name="ct">A cancellation token that can be signaled to abort the operation.</param>
	private async Task SetIdentityInsertAsync(string tableName, bool enable, CancellationToken ct)
	{
		string onOff = enable ? "ON" : "OFF";
		string sql = $"SET IDENTITY_INSERT {QuoteSqlServer(tableName)} {onOff}";
		var cmd = new SqlCommand(sql, mConnection);
		try
		{
			await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Queries the schema and caches the names of all tables that have an identity column.
	/// </summary>
	/// <param name="ct">A cancellation token that can be signaled to abort the operation.</param>
	private async Task CacheIdentityTableNamesAsync(CancellationToken ct)
	{
		const string sql = """
		                   SELECT t.name
		                   FROM sys.tables t
		                   JOIN sys.identity_columns ic ON t.object_id = ic.object_id
		                   """;

		mIdentityTables = [];
		var cmd = new SqlCommand(sql, mConnection);
		try
		{
			SqlDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(ct).ConfigureAwait(false))
				{
					mIdentityTables.Add(reader.GetString(0));
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

	// --- Private Checkpoint Helpers ---

	/// <summary>
	/// Creates the checkpoint table idempotently in the target database.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	private async Task CreateCheckpointTableAsync(CancellationToken cancellationToken)
	{
		var cmd = new SqlCommand(
			$"""
			 IF OBJECT_ID('{CheckpointTableName}', 'U') IS NULL
			 CREATE TABLE [{CheckpointTableName}] (
			     [shuttle_id]          NVARCHAR(450) NOT NULL,
			     [table_name]          NVARCHAR(450) NOT NULL,
			     [chunks_completed]    INT           NOT NULL,
			     [total_rows_imported] BIGINT        NOT NULL,
			     [started_utc]         NVARCHAR(50)  NOT NULL,
			     [updated_utc]         NVARCHAR(50)  NOT NULL,
			     CONSTRAINT [PK__shuttle_import_checkpoint] PRIMARY KEY ([shuttle_id], [table_name])
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
		var mismatchCmd = new SqlCommand(
			$"SELECT DISTINCT TOP 1 [shuttle_id] FROM [{CheckpointTableName}] WHERE [shuttle_id] != @shuttleId",
			mConnection);
		try
		{
			mismatchCmd.Parameters.AddWithValue("@shuttleId", mShuttleId!);
			object? mismatch = await mismatchCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

			if (mismatch != null && mismatch != DBNull.Value)
			{
				mLogger?.LogWarning(
					"Import checkpoint references a different shuttle file (found: {FoundShuttleId}, expected: {ExpectedShuttleId}) — discarding checkpoint and restarting from scratch",
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
		var readCmd = new SqlCommand(
			$"""
			 SELECT [table_name], [chunks_completed], [total_rows_imported]
			 FROM [{CheckpointTableName}]
			 WHERE [shuttle_id] = @shuttleId
			 """,
			mConnection);
		try
		{
			readCmd.Parameters.AddWithValue("@shuttleId", mShuttleId!);

			SqlDataReader reader =
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
		SqlTransaction    transaction,
		string            tableName,
		int               chunksCompleted,
		long              totalRowsImported,
		CancellationToken cancellationToken)
	{
		var cmd = new SqlCommand(
			$"""
			 MERGE [{CheckpointTableName}] AS target
			 USING (SELECT @shuttleId AS shuttle_id, @tableName AS table_name) AS source
			 ON target.[shuttle_id] = source.shuttle_id AND target.[table_name] = source.table_name
			 WHEN MATCHED THEN
			     UPDATE SET [chunks_completed] = @chunks, [total_rows_imported] = @rows, [updated_utc] = @now
			 WHEN NOT MATCHED THEN
			     INSERT ([shuttle_id], [table_name], [chunks_completed], [total_rows_imported], [started_utc], [updated_utc])
			     VALUES (@shuttleId, @tableName, @chunks, @rows, @now, @now);
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
		var cmd = new SqlCommand(
			$"DROP TABLE IF EXISTS [{CheckpointTableName}]",
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

	// --- Private Shuttle → SQL Server Type Conversion ---

	/// <summary>
	/// Queries <c>INFORMATION_SCHEMA.COLUMNS</c> for the actual SQL Server data types of the target
	/// table and returns them as an array aligned with <see cref="TableSnapshot.Columns"/>.
	/// </summary>
	/// <param name="table">The table snapshot whose columns define the expected order.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// An array of SQL Server <c>DATA_TYPE</c> strings (e.g., <c>"uniqueidentifier"</c>,
	/// <c>"datetime2"</c>, <c>"bit"</c>), one per column in <paramref name="table"/>.
	/// Elements are <see langword="null"/> when the column is not found in the target schema.
	/// </returns>
	private async Task<string?[]> GetTargetColumnTypesAsync(
		TableSnapshot     table,
		CancellationToken cancellationToken)
	{
		var sqlTypes = new Dictionary<string, string>(StringComparer.Ordinal);

		var cmd = new SqlCommand(
			"""
			SELECT COLUMN_NAME, DATA_TYPE
			FROM INFORMATION_SCHEMA.COLUMNS
			WHERE TABLE_NAME = @table
			""",
			mConnection);
		cmd.Parameters.AddWithValue("@table", table.Name);
		try
		{
			SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					sqlTypes[reader.GetString(0)] = reader.GetString(1);
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
			sqlTypes.TryGetValue(table.Columns[i].Name, out result[i]);
		}

		return result;
	}

	/// <summary>
	/// Converts a raw value read from the SQLite shuttle file into the CLR type expected by
	/// <see cref="SqlBulkCopy"/> for the given target column type.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The shuttle file stores all data using SQLite native types: <see cref="string"/> for
	///     <c>TEXT</c> columns (timestamps, UUIDs), <see cref="long"/> for <c>INTEGER</c>
	///     columns (booleans, ints, bigints), <see cref="double"/> for <c>REAL</c>, and
	///     <c>byte[]</c> for <c>BLOB</c>. <see cref="SqlBulkCopy"/> cannot convert all of
	///     these automatically (e.g., <see cref="string"/> → <c>uniqueidentifier</c> fails).
	///     </para>
	///     <para>
	///     When the value is already the correct CLR type, or the SQL Server type is unknown,
	///     the value is returned unchanged (passthrough).
	///     </para>
	/// </remarks>
	/// <param name="value">The raw shuttle value, or <see langword="null"/> for SQL NULL.</param>
	/// <param name="sqlServerDataType">
	/// The SQL Server <c>DATA_TYPE</c> from <c>INFORMATION_SCHEMA.COLUMNS</c>
	/// (e.g., <c>"uniqueidentifier"</c>, <c>"datetime2"</c>, <c>"bit"</c>), or
	/// <see langword="null"/> when the column type is unknown.
	/// </param>
	/// <returns>The converted value suitable for <see cref="SqlBulkCopy"/>.</returns>
	private static object? ConvertShuttleValue(object? value, string? sqlServerDataType)
	{
		if (value is null)
			return null;

		return sqlServerDataType switch
		{
			"uniqueidentifier" when value is byte[] b =>
				new Guid(b),
			"uniqueidentifier" when value is string s =>
				Guid.Parse(s),
			"datetime2" or "datetime" or "smalldatetime" when value is string s =>
				DateTime.Parse(s, CultureInfo.InvariantCulture),
			"datetimeoffset" when value is string s =>
				DateTimeOffset.Parse(s, CultureInfo.InvariantCulture),
			"date" when value is string s =>
				DateTime.Parse(s, CultureInfo.InvariantCulture),
			"time" when value is string s =>
				TimeSpan.Parse(s, CultureInfo.InvariantCulture),
			"bit" when value is long l =>
				l != 0,
			"int" when value is long l =>
				(int)l,
			"smallint" when value is long l =>
				(short)l,
			"tinyint" when value is long l =>
				(byte)l,
			"real" when value is double d =>
				(float)d,
			var _ => value
		};
	}

	// --- Nested Types ---

	/// <summary>
	/// A chunked <see cref="IDataReader"/> wrapper that reads at most a specified number of rows
	/// from a shared <see cref="IAsyncEnumerator{T}"/>, enabling per-chunk <see cref="SqlBulkCopy"/> operations.
	/// </summary>
	/// <param name="snapshot">The table snapshot providing column metadata.</param>
	/// <param name="enumerator">
	/// The shared row enumerator. Ownership is NOT transferred — the caller must dispose it.
	/// </param>
	/// <param name="maxRows">Maximum number of rows this reader will yield.</param>
	/// <remarks>
	///     <para>
	///     <see cref="SqlBulkCopy"/> requires a synchronous <see cref="IDataReader"/>, but
	///     <see cref="TableSnapshot.Rows"/> is an <see cref="IAsyncEnumerable{T}"/>. This class
	///     bridges the gap by synchronously blocking on the async enumerator in <see cref="Read"/>.
	///     </para>
	///     <para>
	///     Unlike the previous single-pass reader, this class limits the number of rows returned
	///     per instance to <paramref name="maxRows"/>. After reading that many rows (or exhausting
	///     the source), <see cref="Read"/> returns <see langword="false"/>. The caller can then
	///     commit the chunk transaction and create a new reader for the next chunk, using the
	///     same underlying enumerator.
	///     </para>
	/// </remarks>
	private sealed class ChunkedTableSnapshotDataReader(
		TableSnapshot               snapshot,
		IAsyncEnumerator<object?[]> enumerator,
		int                         maxRows,
		string?[]?                  sqlServerTypes = null) : IDataReader
	{
		private object?[]? mCurrentRow;

		/// <summary>
		/// Gets the total number of rows read by this reader instance so far.
		/// </summary>
		public int RowsRead { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the underlying source was exhausted during this chunk.
		/// When <see langword="true"/>, there are no more rows to import for this table.
		/// </summary>
		public bool SourceExhausted { get; private set; }

		/// <inheritdoc/>
		public int FieldCount => snapshot.Columns.Count;

		/// <inheritdoc/>
		public object this[int i] => GetValue(i);

		/// <inheritdoc/>
		public object this[string name] => GetValue(GetOrdinal(name));

		/// <inheritdoc/>
		public int Depth => 0;

		/// <inheritdoc/>
		public bool IsClosed => false;

		/// <inheritdoc/>
		public int RecordsAffected => -1;

		/// <inheritdoc/>
		public bool Read()
		{
			// Stop after maxRows.
			if (RowsRead >= maxRows)
				return false;

			bool hasNext = enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult();
			if (hasNext)
			{
				mCurrentRow = enumerator.Current;

				// Convert shuttle values (SQLite native types) to SQL Server–compatible CLR types.
				if (sqlServerTypes is not null)
				{
					for (int i = 0; i < mCurrentRow.Length; i++)
					{
						mCurrentRow[i] = ConvertShuttleValue(mCurrentRow[i], sqlServerTypes[i]);
					}
				}

				RowsRead++;
			}
			else
			{
				mCurrentRow = null;
				SourceExhausted = true;
			}

			return hasNext;
		}

		/// <inheritdoc/>
		public object GetValue(int i)
		{
			if (mCurrentRow == null)
				throw new InvalidOperationException("No current row. Call Read() first.");

			return mCurrentRow[i] ?? DBNull.Value;
		}

		/// <inheritdoc/>
		public int GetOrdinal(string name) =>
			snapshot.Columns.FindIndex(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

		/// <inheritdoc/>
		public string GetName(int i) => snapshot.Columns[i].Name;

		/// <inheritdoc/>
		public string GetDataTypeName(int i) => snapshot.Columns[i].DbType;

		/// <inheritdoc/>
		public bool IsDBNull(int i) => GetValue(i) == DBNull.Value;

		/// <inheritdoc/>
		public Type GetFieldType(int i)
		{
			object value = GetValue(i);
			return value == DBNull.Value ? typeof(object) : value.GetType();
		}

		/// <inheritdoc/>
		public void Close() { }

		/// <inheritdoc/>
		public void Dispose() { }

		/// <inheritdoc/>
		public bool NextResult() => false;

		/// <inheritdoc/>
		public DataTable GetSchemaTable() => throw new NotSupportedException();

		/// <inheritdoc/>
		public bool GetBoolean(int i) => (bool)GetValue(i);

		/// <inheritdoc/>
		public byte GetByte(int i) => (byte)GetValue(i);

		/// <inheritdoc/>
		public DateTime GetDateTime(int i) => (DateTime)GetValue(i);

		/// <inheritdoc/>
		public decimal GetDecimal(int i) => (decimal)GetValue(i);

		/// <inheritdoc/>
		public double GetDouble(int i) => (double)GetValue(i);

		/// <inheritdoc/>
		public float GetFloat(int i) => (float)GetValue(i);

		/// <inheritdoc/>
		public Guid GetGuid(int i) => (Guid)GetValue(i);

		/// <inheritdoc/>
		public short GetInt16(int i) => (short)GetValue(i);

		/// <inheritdoc/>
		public int GetInt32(int i) => (int)GetValue(i);

		/// <inheritdoc/>
		public long GetInt64(int i) => (long)GetValue(i);

		/// <inheritdoc/>
		public string GetString(int i) => (string)GetValue(i);

		/// <inheritdoc/>
		public int GetValues(object[] values)
		{
			if (mCurrentRow == null) return 0;
			Array.Copy(mCurrentRow, values, mCurrentRow.Length);

			for (int i = 0; i < values.Length; i++)
			{
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				values[i] ??= DBNull.Value;
			}

			return mCurrentRow.Length;
		}

		/// <inheritdoc/>
		public long GetBytes(
			int     i,
			long    fieldOffset,
			byte[]? buffer,
			int     bufferoffset,
			int     length)
		{
			byte[] val = (byte[])GetValue(i);
			int bytesToCopy = Math.Min(length, val.Length - (int)fieldOffset);
			if (bytesToCopy <= 0) return 0;

			Array.Copy(val, (int)fieldOffset, buffer!, bufferoffset, bytesToCopy);
			return bytesToCopy;
		}

		/// <inheritdoc/>
		public char GetChar(int i) => throw new NotSupportedException();

		/// <inheritdoc/>
		public long GetChars(
			int     i,
			long    fieldoffset,
			char[]? buffer,
			int     bufferoffset,
			int     length) => throw new NotSupportedException();

		/// <inheritdoc/>
		public IDataReader GetData(int i) => throw new NotSupportedException();
	}
}
