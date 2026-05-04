// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data;

using LumaCore.Data.DataPort.Models;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

using static LumaCore.Data.Providers.SqlIdentifierHelper;

namespace LumaCore.Data.DataPort.Import.Implementations;

/// <summary>
/// Implements the <see cref="IDataImportWriter"/> contract for SQLite.
/// </summary>
/// <remarks>
///     <para>
///     This importer uses prepared INSERT statements for high-speed data import.
///     It disables foreign key checks during import and re-enables them during cleanup.
///     </para>
///     <para>
///     Data is imported in chunks of <see cref="DataPortTuning.ImportChunkSizeRows"/> rows. Each chunk
///     is committed in its own transaction alongside a checkpoint update to the
///     <c>_shuttle_import_checkpoint</c> table, enabling crash-safe resume.
///     </para>
/// </remarks>
public sealed class SqliteImportWriter : IDataImportWriter
{
	/// <summary>
	/// Name of the temporary checkpoint table created in the target database during import.
	/// </summary>
	private const string CheckpointTableName = "_shuttle_import_checkpoint";

	private readonly string            mConnectionString;
	private readonly TimeProvider      mTimeProvider;
	private readonly ILogger?          mLogger;
	private          SqliteConnection? mConnection;
	private          string?           mShuttleId;
	private          bool              mDisposed;

	/// <summary>
	/// Checkpoint data loaded during <see cref="PrepareForImportAsync"/>. Maps table name to the number of
	/// chunks already committed and the total number of rows imported for that table.
	/// </summary>
	private Dictionary<string, (int ChunksCompleted, long TotalRowsImported)>? mCheckpoints;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteImportWriter"/> class.
	/// </summary>
	/// <param name="connectionString">The SQLite connection string for the target database.</param>
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
	public SqliteImportWriter(string connectionString, TimeProvider timeProvider, ILogger? logger = null)
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

		SqliteConnection? connection = mConnection;
		mConnection = null;

		if (connection != null)
		{
			// Re-enable foreign keys to ensure the connection is clean when returned to the pool.
			try
			{
				SqliteCommand cmd = connection.CreateCommand();
				try
				{
					cmd.CommandText = "PRAGMA foreign_keys = ON;";
					await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
				}
				finally
				{
					await cmd.DisposeAsync().ConfigureAwait(false);
				}
			}
			catch
			{
				// Best-effort cleanup. If the connection is broken, we can't do much.
			}

			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();

		// Prevent re-initialization.
		if (mConnection != null)
			throw new InvalidOperationException("Importer has already been initialized");

		mConnection = new SqliteConnection(mConnectionString);
		await mConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Disable Foreign Keys for the connection.
		SqliteCommand cmd = mConnection.CreateCommand();
		try
		{
			cmd.CommandText = "PRAGMA foreign_keys = OFF;";
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task<List<MigrationInfo>> GetMigrationHistoryAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		var migrations = new List<MigrationInfo>();

		// If the migrations table doesn't exist, return empty list.
		SqliteCommand checkCmd = mConnection!.CreateCommand();
		try
		{
			checkCmd.CommandText =
				"SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'";
			object? exists = await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			if (exists == null || exists == DBNull.Value)
				return migrations;
		}
		finally
		{
			await checkCmd.DisposeAsync().ConfigureAwait(false);
		}

		SqliteCommand cmd = mConnection.CreateCommand();
		try
		{
			cmd.CommandText =
				"SELECT \"MigrationId\", \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"";

			SqliteDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		return migrations;
	}

	/// <inheritdoc/>
	public async Task PrepareForImportAsync(string shuttleId, CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		ArgumentException.ThrowIfNullOrWhiteSpace(shuttleId);
		mShuttleId = shuttleId;

		// NOTE: Foreign keys are already disabled in InitializeAsync().

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
			// Fresh import for this table: clear existing data.
			try
			{
				SqliteCommand deleteCmd = mConnection!.CreateCommand();
				try
				{
					deleteCmd.CommandText = $"DELETE FROM {QuoteSqlite(table.Name)}";
					await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					await deleteCmd.DisposeAsync().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, "Failed to clear table {TableName}", table.Name);
				throw;
			}
		}

		// Prepare INSERT SQL.
		string columnList = string.Join(", ", table.Columns.Select(c => QuoteSqlite(c.Name)));
		string parameterList = string.Join(", ", table.Columns.Select((_, i) => $"@p{i}"));
		string insertSql = $"INSERT INTO {QuoteSqlite(table.Name)} ({columnList}) VALUES ({parameterList})";

		long totalRowCount = rowsAlreadyImported;
		long rowsToSkip = rowsAlreadyImported;
		int currentChunk = chunksCompleted;
		int rowsInCurrentChunk = 0;

		// Per-chunk transaction and command — created lazily when the first row of a chunk arrives.
		SqliteTransaction? chunkTransaction = null;
		SqliteCommand? insertCmd = null;

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

				// Start a new chunk transaction if needed.
				if (chunkTransaction == null)
				{
					chunkTransaction = (SqliteTransaction)await mConnection!
						                                      .BeginTransactionAsync(cancellationToken)
						                                      .ConfigureAwait(false);

					insertCmd = mConnection.CreateCommand();
					insertCmd.Transaction = chunkTransaction;
					insertCmd.CommandText = insertSql;
					for (int i = 0; i < table.Columns.Count; i++)
					{
						insertCmd.Parameters.Add(new SqliteParameter($"@p{i}", DbType.Object));
					}
				}

				// Insert the row.
				for (int i = 0; i < row.Length; i++)
				{
					insertCmd!.Parameters[i].Value = row[i] ?? DBNull.Value;
				}

				await insertCmd!.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				rowsInCurrentChunk++;
				totalRowCount++;

				// Commit chunk when full.
				if (rowsInCurrentChunk >= chunkSize)
				{
					currentChunk++;
					await UpdateCheckpointAsync(
							chunkTransaction,
							table.Name,
							currentChunk,
							totalRowCount,
							cancellationToken)
						.ConfigureAwait(false);
					await chunkTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);

					// Dispose chunk resources.
					await insertCmd.DisposeAsync().ConfigureAwait(false);
					await chunkTransaction.DisposeAsync().ConfigureAwait(false);
					insertCmd = null;
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
			if (rowsInCurrentChunk > 0 && chunkTransaction != null)
			{
				currentChunk++;
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
			if (insertCmd != null) await insertCmd.DisposeAsync().ConfigureAwait(false);
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

		// Note: We cannot re-enable foreign keys here because PRAGMA foreign_keys requires
		// no active transaction. We will re-enable them in DisposeAsync() to ensure the
		// connection is clean when returned to the pool.
		//
		// CRITICAL: Because we imported data with FKs disabled, we MUST verify integrity.
		SqliteCommand integrityCmd = mConnection!.CreateCommand();
		try
		{
			integrityCmd.CommandText = "PRAGMA foreign_key_check;";
			SqliteDataReader reader = await integrityCmd
				                          .ExecuteReaderAsync(cancellationToken)
				                          .ConfigureAwait(false);
			try
			{
				if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					throw new InvalidOperationException(
						"Foreign key integrity check failed. The imported data violates database constraints.");
				}
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await integrityCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Reset SQLite autoincrement counters (sqlite_sequence table).
		SqliteCommand checkSeqCmd = mConnection.CreateCommand();
		bool seqTableExists;
		try
		{
			checkSeqCmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'sqlite_sequence'";
			object? seqExists = await checkSeqCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			seqTableExists = seqExists != null && seqExists != DBNull.Value;
		}
		finally
		{
			await checkSeqCmd.DisposeAsync().ConfigureAwait(false);
		}

		if (seqTableExists)
		{
			var tablesToReset = new List<string>();
			SqliteCommand getTablesCmd = mConnection.CreateCommand();
			try
			{
				getTablesCmd.CommandText = "SELECT name FROM sqlite_sequence";
				SqliteDataReader reader =
					await getTablesCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				try
				{
					while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
					{
						tablesToReset.Add(reader.GetString(0));
					}
				}
				finally
				{
					await reader.DisposeAsync().ConfigureAwait(false);
				}
			}
			finally
			{
				await getTablesCmd.DisposeAsync().ConfigureAwait(false);
			}

			foreach (string tableName in tablesToReset)
			{
				SqliteCommand resetCmd = mConnection.CreateCommand();
				try
				{
					resetCmd.CommandText =
						$"""
						 UPDATE sqlite_sequence
						 SET seq = (SELECT COALESCE(MAX(rowid), 0) FROM {QuoteSqlite(tableName)})
						 WHERE name = @tableName
						 """;
					resetCmd.Parameters.AddWithValue("@tableName", tableName);
					await resetCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					await resetCmd.DisposeAsync().ConfigureAwait(false);
				}
			}
		}

		// Drop the checkpoint table — import completed successfully, no resume needed.
		await DropCheckpointTableAsync(cancellationToken).ConfigureAwait(false);
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
		SqliteCommand cmd = mConnection!.CreateCommand();
		try
		{
			cmd.CommandText = $"""
			                   CREATE TABLE IF NOT EXISTS {QuoteSqlite(CheckpointTableName)} (
			                    "shuttle_id"          TEXT    NOT NULL,
			                    "table_name"          TEXT    NOT NULL,
			                    "chunks_completed"    INTEGER NOT NULL,
			                    "total_rows_imported" INTEGER NOT NULL,
			                    "started_utc"         TEXT    NOT NULL,
			                    "updated_utc"         TEXT    NOT NULL,
			                    PRIMARY KEY ("shuttle_id", "table_name")
			                   )
			                   """;
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
		SqliteCommand mismatchCmd = mConnection!.CreateCommand();
		try
		{
			mismatchCmd.CommandText =
				$"""
				 SELECT DISTINCT "shuttle_id"
				 FROM {QuoteSqlite(CheckpointTableName)}
				 WHERE "shuttle_id" != @shuttleId
				 LIMIT 1
				 """;
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
		SqliteCommand readCmd = mConnection.CreateCommand();
		try
		{
			readCmd.CommandText =
				$"""
				 SELECT "table_name", "chunks_completed", "total_rows_imported"
				 FROM {QuoteSqlite(CheckpointTableName)}
				 WHERE "shuttle_id" = @shuttleId
				 """;
			readCmd.Parameters.AddWithValue("@shuttleId", mShuttleId!);

			SqliteDataReader reader =
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
		SqliteTransaction transaction,
		string            tableName,
		int               chunksCompleted,
		long              totalRowsImported,
		CancellationToken cancellationToken)
	{
		SqliteCommand cmd = mConnection!.CreateCommand();
		try
		{
			cmd.Transaction = transaction;
			cmd.CommandText = $"""
			                   INSERT INTO {QuoteSqlite(CheckpointTableName)}
			                   	("shuttle_id", "table_name", "chunks_completed", "total_rows_imported", "started_utc", "updated_utc")
			                   VALUES (@shuttleId, @tableName, @chunks, @rows, @now, @now)
			                   ON CONFLICT ("shuttle_id", "table_name") DO UPDATE SET
			                   	"chunks_completed"    = @chunks,
			                   	"total_rows_imported" = @rows,
			                   	"updated_utc"         = @now
			                   """;
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
		SqliteCommand cmd = mConnection!.CreateCommand();
		try
		{
			cmd.CommandText = $"DROP TABLE IF EXISTS {QuoteSqlite(CheckpointTableName)}";
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}
}
