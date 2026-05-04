// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;

using LumaCore.Core;
using LumaCore.Data.DataPort.Models;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

using static LumaCore.Data.Providers.SqlIdentifierHelper;

namespace LumaCore.Data.DataPort.Shuttle;

/// <summary>
/// Writes data exports to a LumaCore Shuttle file (SQLite-based) with optimized performance.
/// </summary>
/// <remarks>
///     <para>
///     This writer creates SQLite database files that are optimized for one-way bulk exports. During
///     the export phase it configures the underlying SQLite connection for maximum throughput (for
///     example by disabling journaling and synchronous writes).
///     </para>
///     <para>
///     <b>Warning:</b> The resulting shuttle file is left in a "high-performance" state
///     (e.g., <c>journal_mode = OFF</c>). This is optimal for the intended "write-once, read-many"
///     lifecycle, as the <see cref="SqliteShuttleReader"/> opens the file in ReadOnly mode.
///     However, if this file is later used by another application in a read-write context,
///     it will inherit these dangerous settings, increasing the risk of data corruption
///     on application crash.
///     </para>
///     <para>
///     After all tables and metadata have been written, callers must invoke
///     <see cref="FinalizeAsync(CancellationToken)"/> exactly once to establish the durability boundary
///     for the export. The implementation switches <c>synchronous</c> back to <c>FULL</c>, forces all
///     buffered data to be flushed to disk, runs a full integrity check, and writes a completion marker
///     that can be validated by the corresponding reader.
///     </para>
///     <para>
///     Instances of this writer are not thread-safe. Callers must ensure that methods are not called
///     concurrently from multiple threads.
///     </para>
/// </remarks>
public sealed class SqliteShuttleWriter : IShuttleWriter
{
	private readonly ILogger           mLogger;
	private readonly TimeProvider      mTimeProvider;
	private readonly string            mConnectionString;
	private          SqliteConnection? mConnection;
	private          bool              mIsFinalized;
	private          bool              mDisposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteShuttleWriter"/> class.
	/// </summary>
	/// <param name="filePath">The file path where the shuttle file will be created.</param>
	/// <param name="logger">The logger for progress reporting.</param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="filePath"/>, <paramref name="logger"/>, or <paramref name="timeProvider"/> is
	/// <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="filePath"/> is empty, consists only of white-space characters, contains characters that are
	/// invalid on the current operating system, or contains a path segment that exceeds 255 characters.
	/// </exception>
	public SqliteShuttleWriter(string filePath, ILogger logger, TimeProvider timeProvider)
	{
		// Validate parameters and build connection string.
		mConnectionString = BuildConnectionString(filePath);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(timeProvider);

		// Store dependencies.
		mLogger = logger;
		mTimeProvider = timeProvider;
	}

	/// <summary>
	/// Gets a value indicating whether <see cref="InitializeAsync"/> has been called successfully and the
	/// writer has not yet been disposed.
	/// </summary>
	/// <remarks>
	/// This property returns <see langword="false"/> both before initialization and after disposal. It reflects
	/// the writer's current operational readiness, not whether initialization was ever attempted.
	/// </remarks>
	public bool IsInitialized => mConnection is not null;

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     Disposes the underlying SQLite connection and releases all unmanaged resources. This method
	///     does not implicitly finalize the export and does not write any completion markers.
	///     </para>
	///     <para>
	///     Callers are responsible for invoking <see cref="FinalizeAsync(CancellationToken)"/> exactly once
	///     after all data has been written and before disposing the writer. Disposing a writer that has never
	///     been finalized leaves the shuttle file in an incomplete state; the corresponding reader implementation
	///     is expected to reject such files.
	///     </para>
	/// </remarks>
	public ValueTask DisposeAsync()
	{
		mDisposed = true;

		SqliteConnection? connection = mConnection;
		mConnection = null;

		if (connection != null)
			return connection.DisposeAsync();

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		// Prevent use after disposal.
		ThrowIfDisposed();

		// Ensure not finalized.
		if (mIsFinalized)
		{
			throw new InvalidOperationException(
				"Cannot re-initialize a writer that has already been finalized. Create a new instance for each export.");
		}

		// Validate not already initialized.
		if (mConnection != null)
			throw new InvalidOperationException("Writer has already been initialized");

		SqliteConnection? connection = null;

		try
		{
			// Create and open SQLite connection.
			connection = new SqliteConnection(mConnectionString);
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			// Prepare command for PRAGMAs.
			SqliteCommand cmd = connection.CreateCommand();
			try
			{
				// Apply PRAGMAs.
				// We split them into "Critical" (must succeed) and "Optional" (log warning on fail).
				try
				{
					// --- Critical PRAGMAs ---
					// If these fail, the export's assumptions about performance
					// and safety are broken, so we must stop.

					// We use EXCLUSIVE locking to prevent any other process
					// from interfering with our write-once operation.
					cmd.CommandText = "PRAGMA locking_mode = EXCLUSIVE;";
					await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

					// We need journal_mode=OFF and synchronous=OFF for the "Reckless Mode" speed.
					cmd.CommandText = "PRAGMA journal_mode = OFF;";
					await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

					cmd.CommandText = "PRAGMA synchronous = OFF;";
					await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					mLogger.LogError(
						ex,
						"A critical PRAGMA ({CommandText}) failed to set, aborting export",
						cmd.CommandText);
					throw; // Re-throw to stop the service
				}

				try
				{
					// --- Optional Performance PRAGMAs ---
					// If these fail, it's not ideal, but the export can still proceed.

					// Set page size (32KB)
					cmd.CommandText = "PRAGMA page_size = 32768;";
					await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

					// Set cache size (approx 128MB)
					cmd.CommandText = "PRAGMA cache_size = -128000;";
					await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

					// Use in-memory temporary storage
					cmd.CommandText = "PRAGMA temp_store = MEMORY;";
					await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					// Log the failed optimization but DO NOT stop the export.
					mLogger.LogWarning(
						ex,
						"An optional PRAGMA ({CommandText}) failed to set, but the export will continue",
						cmd.CommandText);
				}

				// Success: publish the connection to the instance.
				mConnection = connection;
				connection = null;
			}
			finally
			{
				await cmd.DisposeAsync().ConfigureAwait(false);
			}

			// Log initialization.
			mLogger.LogDebug("SQLite shuttle writer initialized in high-performance mode");
		}
		finally
		{
			// If anything failed before we assigned mConnection, clean up the local connection.
			if (connection != null)
			{
				await connection.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	/// <exception cref="ArgumentNullException"><paramref name="table"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <para>
	///     This implementation streams rows from <see cref="TableSnapshot.Rows"/> and writes them to the
	///     underlying SQLite database in batches using a sequence of short-lived transactions. The
	///     connection is configured for <c>journal_mode = OFF</c> and <c>synchronous = OFF</c> while the
	///     export is running to maximize throughput.
	///     </para>
	///     <para>
	///     If an exception or cancellation occurs during this method, the current table may be only
	///     partially populated and the overall shuttle file is considered incomplete. Because the
	///     completion marker is only written by <see cref="FinalizeAsync(CancellationToken)"/>, such a
	///     file will be rejected by the corresponding reader implementation and should be discarded or
	///     re-generated.
	///     </para>
	/// </remarks>
	public async Task WriteTableAsync(
		TableSnapshot                      table,
		ILogger?                           logger,
		IProgress<DataPortProgressReport>? progress,
		int                                currentTable,
		int                                totalTables,
		CancellationToken                  cancellationToken = default)
	{
		// Validate arguments.
		ArgumentNullException.ThrowIfNull(table);

		ThrowIfNotInitialized();

		// Ensure not finalized.
		if (mIsFinalized)
			throw new InvalidOperationException("Cannot write new tables after FinalizeAsync() has been called.");

		// Create table without indexes (indexes not needed for backup storage)
		await CreateTableAsync(table, cancellationToken).ConfigureAwait(false);

		// Stream and insert rows in batches
		await InsertRowsAsync(table, logger, progress, currentTable, totalTables, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	///     <para>
	///     This implementation stores Entity Framework Core migration information in the
	///     <see cref="SqliteShuttleSchema.MigrationsTableName"/> table inside the shuttle file. The table
	///     is created on demand if it does not already exist, and all migration records are written within
	///     a single transaction.
	///     </para>
	///     <para>
	///     The LumaCore Shuttle format itself does not require migration history to be present for correctness.
	///     The matching reader ignores the <see cref="SqliteShuttleSchema.MigrationsTableName"/> table unless higher-level
	///     code explicitly chooses to consume it.
	///     </para>
	/// </remarks>
	public async Task WriteMigrationHistoryAsync(
		List<MigrationInfo> migrations,
		CancellationToken   cancellationToken = default)
	{
		// Validate arguments.
		ArgumentNullException.ThrowIfNull(migrations);

		ThrowIfNotInitialized();

		// Ensure not finalized.
		if (mIsFinalized)
		{
			throw new InvalidOperationException(
				"Cannot write migration history after FinalizeAsync() has been called.");
		}

		// Use a transaction for performance and atomicity.
		var transaction =
			(SqliteTransaction)await mConnection!
				                   .BeginTransactionAsync(cancellationToken)
				                   .ConfigureAwait(false);

		try
		{
			// Create migration history table if it does not exist yet.
			SqliteCommand createCmd = mConnection.CreateCommand();
			try
			{
				createCmd.Transaction = transaction;
				createCmd.CommandText =
					$"""
					 CREATE TABLE IF NOT EXISTS {QuoteSqlite(SqliteShuttleSchema.MigrationsTableName)} (
					 {QuoteSqlite("MigrationId")}   TEXT PRIMARY KEY,
					 {QuoteSqlite("ProductVersion")} TEXT NOT NULL
					 );
					 """;
				await createCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				await createCmd.DisposeAsync().ConfigureAwait(false);
			}

			// Prepare insert command once and reuse it for all migration rows.
			SqliteCommand insertCmd = mConnection.CreateCommand();
			try
			{
				insertCmd.Transaction = transaction;
				insertCmd.CommandText =
					$"""
					 INSERT OR IGNORE INTO {QuoteSqlite(SqliteShuttleSchema.MigrationsTableName)}
					 	({QuoteSqlite("MigrationId")}, {QuoteSqlite("ProductVersion")})
					 VALUES (@id, @version)
					 """;

				SqliteParameter idParam = insertCmd.Parameters.Add("@id", SqliteType.Text);
				SqliteParameter versionParam = insertCmd.Parameters.Add("@version", SqliteType.Text);

				foreach (MigrationInfo migration in migrations)
				{
					idParam.Value = migration.MigrationId;
					versionParam.Value = migration.ProductVersion;
					await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
			}
			finally
			{
				await insertCmd.DisposeAsync().ConfigureAwait(false);
			}

			// Commit all inserts at once.
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}

		// Log completion.
		mLogger.LogDebug("Wrote {WrittenMigrationCount} migration entries to shuttle file", migrations.Count);
	}

	/// <inheritdoc/>
	/// <exception cref="ArgumentNullException"><paramref name="metadata"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <para>
	///     This implementation persists metadata as key/value pairs in the backup info table defined by
	///     <see cref="SqliteShuttleSchema.BackupInfoTableName"/> within the shuttle database. The table
	///     is created on demand if it does not already exist, and the <c>key</c> column is defined as
	///     <c>PRIMARY KEY</c>.
	///     </para>
	///     <para>
	///     Callers should treat metadata keys as unique within a single export run. When the same key
	///     is supplied more than once, the last value wins because this implementation uses
	///     <c>INSERT OR REPLACE</c> semantics on the underlying table.
	///     </para>
	///     <para>
	///     Certain keys (such as <see cref="SqliteShuttleSchema.ExportStatusKey"/> and
	///     <see cref="SqliteShuttleSchema.ShuttleFormatVersionKey"/>) are reserved
	///     by the LumaCore Shuttle format and must not be supplied by callers. All reserved keys are listed in
	///     <see cref="SqliteShuttleSchema.ReservedMetadataKeys"/> and are enforced by this implementation.
	///     </para>
	///     <para>
	///     <see cref="WriteMetadataAsync"/> is logically optional for the LumaCore Shuttle format: if it is never
	///     called, <see cref="FinalizeAsync(CancellationToken)"/> will still ensure that the table
	///     specified by <see cref="SqliteShuttleSchema.BackupInfoTableName"/> exists and will at least
	///     write the reserved completion and format-version markers.
	///     </para>
	/// </remarks>
	public async Task WriteMetadataAsync(
		Dictionary<string, string> metadata,
		CancellationToken          cancellationToken = default)
	{
		// Validate arguments.
		ArgumentNullException.ThrowIfNull(metadata);

		ThrowIfNotInitialized();

		// Ensure not finalized.
		if (mIsFinalized)
			throw new InvalidOperationException("Cannot write metadata after FinalizeAsync() has been called.");

		// Prevent the caller from using our reserved keys.
		if (metadata.Keys.Any(key => SqliteShuttleSchema.ReservedMetadataKeys.Contains(key)))
		{
			throw new ArgumentException(
				"Metadata contains one or more keys that are reserved by the shuttle writer.",
				nameof(metadata));
		}

		// Use a transaction for performance.
		var transaction =
			(SqliteTransaction)await mConnection!
				                   .BeginTransactionAsync(cancellationToken)
				                   .ConfigureAwait(false);

		try
		{
			// Create table idempotently.
			SqliteCommand createCmd = mConnection.CreateCommand();
			try
			{
				createCmd.Transaction = transaction;
				createCmd.CommandText = $"""
				                         CREATE TABLE IF NOT EXISTS {QuoteSqlite(SqliteShuttleSchema.BackupInfoTableName)} (
				                         {QuoteSqlite("key")} TEXT PRIMARY KEY,
				                         {QuoteSqlite("value")} TEXT NOT NULL
				                         );
				                         """;
				await createCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				await createCmd.DisposeAsync().ConfigureAwait(false);
			}

			// Insert metadata entries using a prepared command for efficiency.
			SqliteCommand insertCmd = mConnection.CreateCommand();
			try
			{
				insertCmd.Transaction = transaction;
				insertCmd.CommandText =
					$"""
					 INSERT OR REPLACE INTO {QuoteSqlite(SqliteShuttleSchema.BackupInfoTableName)}
					 	({QuoteSqlite("key")}, {QuoteSqlite("value")})
					 VALUES (@key, @value)
					 """;

				SqliteParameter keyParam = insertCmd.Parameters.Add("@key", SqliteType.Text);
				SqliteParameter valueParam = insertCmd.Parameters.Add("@value", SqliteType.Text);

				foreach ((string key, string value) in metadata)
				{
					keyParam.Value = key;
					valueParam.Value = value;
					await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
			}
			finally
			{
				await insertCmd.DisposeAsync().ConfigureAwait(false);
			}

			// Commit all inserts at once.
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}

		// Log completion.
		mLogger.LogDebug("Wrote {WrittenMetadataCount} metadata entries to shuttle file", metadata.Count);
	}

	/// <inheritdoc/>
	/// <exception cref="InvalidOperationException">
	///     <para>Writer is not initialized.</para>
	///     <para>- or -</para>
	///     <para><see cref="FinalizeAsync(CancellationToken)"/> has already been called.</para>
	/// </exception>
	/// <exception cref="InvalidDataException">
	/// The shuttle file integrity check failed (the file may be corrupted).
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method establishes the durability and completeness boundary for the export. It must be
	///     called once after all data and metadata have been written and before the resulting shuttle file
	///     is exposed to consumers.
	///     </para>
	///     <para>
	///     The implementation switches <c>synchronous</c> back to <c>FULL</c>, triggers a commit that
	///     flushes all pending changes to disk, runs an integrity check against the flushed database file,
	///     and records a completion marker that is later validated by the corresponding reader.
	///     </para>
	///     <para>
	///     If this method throws at any point, the shuttle file must be treated as invalid and should be
	///     discarded. Disposing the writer does <b>not</b> call this method automatically.
	///     </para>
	/// </remarks>
	public async Task FinalizeAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfNotInitialized();

		// Ensure not already finalized.
		if (mIsFinalized)
		{
			throw new InvalidOperationException(
				"FinalizeAsync() has already been called. This method must be called exactly once.");
		}

		// Switch synchronous mode back to FULL to ensure the final flush is durable.
		// Note: When journal_mode=OFF, the synchronous pragma has limited effect since there is no
		// journal to sync. However, setting it to FULL ensures that any subsequent operations
		// (like the empty transaction below) will trigger a full fsync to disk.
		SqliteCommand syncCmd = mConnection!.CreateCommand();
		try
		{
			mLogger.LogDebug("Setting synchronous mode to FULL for final flush");
			syncCmd.CommandText = "PRAGMA synchronous = FULL;";
			await syncCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await syncCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Force a Flush via an Empty Transaction.
		mLogger.LogDebug("Running empty transaction to force flush data to disk...");
		var transaction = (SqliteTransaction)await mConnection
			                                     .BeginTransactionAsync(cancellationToken)
			                                     .ConfigureAwait(false);

		try
		{
			// "BEGIN IMMEDIATE" is the standard behavior of BeginTransactionAsync()
			// + "COMMIT" is a clean semantic flush point.
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}
		mLogger.LogDebug("All data is now flushed to disk");

		// Integrity check (now runs on the *actual* flushed file).
		SqliteCommand integrityCmd = mConnection.CreateCommand();
		try
		{
			mLogger.LogDebug("Verifying shuttle file integrity...");
			integrityCmd.CommandText = "PRAGMA integrity_check;";
			string? result = (string?)await integrityCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

			// Throw if integrity check failed.
			// "ok" means the database is valid.
			// Any other result indicates corruption.
			if (result != "ok")
			{
				throw new InvalidDataException(
					$"Shuttle file integrity check failed: {result}. " +
					"The file may be corrupted. Please retry the export.");
			}
		}
		finally
		{
			await integrityCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Log success.
		mLogger.LogInformation("Shuttle file integrity verified successfully");

		// Ensure the backup info table defined by SqliteShuttleSchema.BackupInfoTableName exists,
		// in case WriteMetadataAsync was never called.
		SqliteCommand createCmd = mConnection.CreateCommand();
		try
		{
			createCmd.CommandText = $"""
			                         CREATE TABLE IF NOT EXISTS {QuoteSqlite(SqliteShuttleSchema.BackupInfoTableName)} (
			                         {QuoteSqlite("key")} TEXT PRIMARY KEY,
			                         {QuoteSqlite("value")} TEXT NOT NULL
			                         );
			                         """;
			await createCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await createCmd.DisposeAsync().ConfigureAwait(false);
		}

		// Write completion marker to the backup info table.
		// This indicates the export completed successfully.
		try
		{
			SqliteCommand markerCmd = mConnection.CreateCommand();
			try
			{
				// Use "INSERT OR REPLACE" to avoid a UNIQUE constraint failure
				markerCmd.CommandText =
					$"""
					 INSERT OR REPLACE INTO {QuoteSqlite(SqliteShuttleSchema.BackupInfoTableName)}
					 	({QuoteSqlite("key")}, {QuoteSqlite("value")})
					 VALUES (@key, @value)
					 """;

				// Write the completion marker.
				markerCmd.Parameters.AddWithValue("@key", SqliteShuttleSchema.ExportStatusKey);
				markerCmd.Parameters.AddWithValue("@value", SqliteShuttleSchema.CompletedValue);
				await markerCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

				// Write the version marker.
				markerCmd.Parameters.Clear();
				markerCmd.Parameters.AddWithValue("@key", SqliteShuttleSchema.ShuttleFormatVersionKey);
				markerCmd.Parameters.AddWithValue(
					"@value",
					SqliteShuttleSchema.CurrentShuttleFormatVersion.ToString(CultureInfo.InvariantCulture));
				await markerCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

				// Write the shuttle identity for checkpoint-based resume during import.
				// This must be written during FinalizeAsync() so that only successfully completed exports
				// receive an identity.
				markerCmd.Parameters.Clear();
				markerCmd.Parameters.AddWithValue("@key", SqliteShuttleSchema.ShuttleIdKey);
				markerCmd.Parameters.AddWithValue("@value", Guid.NewGuid().ToString("D"));
				await markerCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

				// Write the creation timestamp so readers can determine shuttle age without
				// relying on file system metadata (which can be altered by copy/move operations).
				markerCmd.Parameters.Clear();
				markerCmd.Parameters.AddWithValue("@key", SqliteShuttleSchema.CreatedUtcKey);
				markerCmd.Parameters.AddWithValue("@value", mTimeProvider.GetUtcNow().ToString("o"));
				await markerCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				await markerCmd.DisposeAsync().ConfigureAwait(false);
			}

			mLogger.LogDebug("Export completion marker written and flushed");
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// Most likely an I/O error. Log and re-throw.
			mLogger.LogCritical(ex, "Failed to write final completion marker");
			throw new InvalidOperationException("Failed to write final completion marker", ex);
		}

		// Mark as finalized to prevent re-entry.
		mIsFinalized = true;
	}

	/// <summary>
	/// Validates the file path and builds a SQLite connection string for write access without pooling.
	/// </summary>
	/// <param name="filePath">The file path where the shuttle file will be created.</param>
	/// <returns>A SQLite connection string targeting <paramref name="filePath"/>.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="filePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="filePath"/> is empty, consists only of white-space characters, contains characters that are
	/// invalid on the current operating system, or contains a path segment that exceeds 255 characters.
	/// </exception>
	private static string BuildConnectionString(string filePath)
	{
		FilePathValidator.Validate(filePath);

		return new SqliteConnectionStringBuilder
		{
			DataSource = filePath,
			Pooling = false // Ensure file handle is released immediately on dispose
		}.ConnectionString;
	}

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if the writer has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(mDisposed, this);

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if the writer has been disposed, or
	/// <see cref="InvalidOperationException"/> if it has not been initialized.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="InvalidOperationException">The writer is not initialized.</exception>
	private void ThrowIfNotInitialized()
	{
		ThrowIfDisposed();

		if (mConnection is null)
			throw new InvalidOperationException("Writer is not initialized. Call InitializeAsync() first.");
	}

	/// <summary>
	/// Creates the table schema in the SQLite database.
	/// </summary>
	/// <param name="table">The table snapshot.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// The LumaCore Shuttle format is intentionally data-centric. It mirrors column names and basic
	/// SQLite-compatible types but does not recreate any schema constraints (nullability, primary keys,
	/// foreign keys, indexes). Those aspects are owned by the EF Core model and its migrations, not by
	/// the shuttle container.
	/// </remarks>
	private async Task CreateTableAsync(TableSnapshot table, CancellationToken cancellationToken)
	{
		// Create the table with the specified name and columns.
		// No schema constraints (NOT NULL, PK, FK, indexes) are created for shuttle storage.
		string columns = string.Join(
			", ",
			table.Columns.Select(c => $"{QuoteSqlite(c.Name)} {c.ShuttleStorageType ?? c.DbType}"));

		string sql = $"CREATE TABLE IF NOT EXISTS {QuoteSqlite(table.Name)} ({columns})";

		SqliteCommand cmd = mConnection!.CreateCommand();
		try
		{
			cmd.CommandText = sql;
			await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}

		// Log table creation.
		mLogger.LogDebug("Created table {TableName} in shuttle file", table.Name);
	}

	/// <summary>
	/// Inserts all rows from the snapshot into the table, reporting progress.
	/// </summary>
	/// <param name="table">The table snapshot.</param>
	/// <param name="logger">The logger for progress reporting (<see langword="null"/> to disable logging progress).</param>
	/// <param name="progress">Progress reporter for data export progress.</param>
	/// <param name="currentTable">The current table index being processed (1-based).</param>
	/// <param name="totalTables">The total number of tables to be processed.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// Uses a prepared statement and batches commits for performance.
	/// </remarks>
	private async Task InsertRowsAsync(
		TableSnapshot                      table,
		ILogger?                           logger,
		IProgress<DataPortProgressReport>? progress,
		int                                currentTable,
		int                                totalTables,
		CancellationToken                  cancellationToken)
	{
		// Prepare statement for performance.
		string columnNames = string.Join(", ", table.Columns.Select(c => QuoteSqlite(c.Name)));
		string parameters = string.Join(", ", table.Columns.Select((_, i) => $"@p{i}"));
		string sql = $"INSERT INTO {QuoteSqlite(table.Name)} ({columnNames}) VALUES ({parameters})";

		// Prepare command outside the loop.
		SqliteCommand cmd = mConnection!.CreateCommand();
		try
		{
			cmd.CommandText = sql;

			SqliteTransaction? transaction = null;
			try
			{
				// Begin transaction.
				transaction = (SqliteTransaction)await mConnection!
					                                 .BeginTransactionAsync(cancellationToken)
					                                 .ConfigureAwait(false);
				cmd.Transaction = transaction;

				// Create parameters once, reuse for all rows.
				for (int i = 0; i < table.Columns.Count; i++)
				{
					cmd.Parameters.Add(new SqliteParameter { ParameterName = $"@p{i}" });
				}

				// Track row count for batching.
				long rowCount = 0;
				long rowsInCurrentTransaction = 0;
				const int commitBatchSize = DataPortTuning.ShuttleCommitBatchSizeRows;
				const int reportInterval = DataPortTuning.ExportProgressReportIntervalRows;

				// Get estimated count for progress reporting.
				long estimatedRows = table.EstimatedRowCount;
				string overallMsg = $"Exporting '{table.Name}' ({currentTable}/{totalTables})...";

				// Report initial count for this table.
				progress?.Report(
					new DataPortProgressReport
					{
						OverallMessage = overallMsg,
						OverallCurrentStep = currentTable,
						OverallTotalSteps = totalTables,
						DetailedMessage = $"{rowCount:N0} rows exported",
						DetailedCurrentStep = rowCount,
						DetailedTotalSteps = estimatedRows > 0 ? estimatedRows : null
					});

				// Stream rows - only 1 row in RAM at a time!
				await foreach (object?[] row in table.Rows.WithCancellation(cancellationToken).ConfigureAwait(false))
				{
					// Set parameter values and execute insert.
					for (int i = 0; i < row.Length; i++) cmd.Parameters[i].Value = row[i] ?? DBNull.Value;
					await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
					rowCount++;
					rowsInCurrentTransaction++;

					// Block: Report/Cancel (executed frequently)
					if (rowCount % reportInterval == 0)
					{
						// Check for cancellation.
						cancellationToken.ThrowIfCancellationRequested();

						// Report detailed row progress.
						progress?.Report(
							new DataPortProgressReport
							{
								OverallMessage = overallMsg,
								OverallCurrentStep = currentTable,
								OverallTotalSteps = totalTables,
								DetailedMessage = $"{rowCount:N0} rows exported",
								DetailedCurrentStep = rowCount,
								DetailedTotalSteps = estimatedRows > 0 ? estimatedRows : null
							});
					}

					// Block: Commit (executed rarely)
					if (rowsInCurrentTransaction >= commitBatchSize)
					{
						await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
						await transaction.DisposeAsync().ConfigureAwait(false);
						transaction = null;
						rowsInCurrentTransaction = 0;

						// Only start a new transaction if there are more rows to write.
						transaction = (SqliteTransaction)await mConnection
							                                 .BeginTransactionAsync(cancellationToken)
							                                 .ConfigureAwait(false);
						cmd.Transaction = transaction;
					}
				}

				// Commit the final batch (only if we actually wrote rows in the current transaction).
				if (rowsInCurrentTransaction > 0)
					await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

				// Report final count for this table.
				progress?.Report(
					new DataPortProgressReport
					{
						OverallMessage = overallMsg,
						OverallCurrentStep = currentTable,
						OverallTotalSteps = totalTables,
						DetailedMessage = $"{rowCount:N0} rows exported",
						DetailedCurrentStep = rowCount,
						DetailedTotalSteps = estimatedRows > 0 ? estimatedRows : null
					});

				// Log completion.
				logger?.LogInformation("Exported {ExportedRowCount} rows from {TableName}", rowCount, table.Name);
			}
			finally
			{
				// Dispose transaction at the end, even on error.
				if (transaction != null)
				{
					await transaction.DisposeAsync().ConfigureAwait(false);
				}
			}
		}
		finally
		{
			await cmd.DisposeAsync().ConfigureAwait(false);
		}
	}
}
