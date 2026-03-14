// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Providers;
using LumaCore.Data.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Initialization;

partial class DatabaseInitializer
{
	/// <summary>
	/// Creates a portable backup of the current database state before applying migrations.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The absolute path to the created backup file.</returns>
	/// <exception cref="InvalidOperationException">
	/// <see cref="IDatabaseMaintenanceService"/> cannot be resolved from the service provider.
	/// </exception>
	/// <exception cref="IOException">
	/// The backup directory cannot be created (from <see cref="Directory.CreateDirectory(string)"/>).
	/// </exception>
	/// <exception cref="UnauthorizedAccessException">
	/// The process lacks permissions to create the backup directory.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This backup serves as a safety net for automatic migration rollback. The backup uses the
	///     <b>LumaCore Shuttle format</b> — a provider-agnostic SQLite-based container that includes:
	///     </para>
	///     <list type="bullet">
	///         <item><b>Data:</b> All table contents</item>
	///         <item><b>Migration history:</b> List of applied migration IDs (from <c>__EFMigrationsHistory</c>)</item>
	///     </list>
	///     <para>
	///     The Shuttle backup does <b>not</b> contain migration code itself. During restore, the schema is
	///     recreated by reapplying migrations from the project code.
	///     </para>
	///     <para>
	///     The backup is created via <see cref="IDatabaseMaintenanceService.CreateShuttleBackupAsync"/>,
	///     which handles export from the current provider (SQLite, PostgreSQL, SQL Server, MySQL) to the
	///     portable LumaCore Shuttle format.
	///     </para>
	/// </remarks>
	private async Task<string> CreateBackupAndReturnPathAsync(CancellationToken cancellationToken)
	{
		// Create new scope to resolve scoped services (DbContext, DataPortService)
		AsyncServiceScope scope = mServiceProvider.CreateAsyncScope();
		try
		{
			ExecutionStageMonitor.ReportStage("CreateBackup.BeforeCreate");

			// Resolve the maintenance service which handles backup creation.
			var maintenanceService = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceService>();

			// Create portable backup (export current DB → LumaCore Shuttle format: provider-agnostic SQLite container)
			// The returned path is the absolute path to the created backup file.
			return await maintenanceService
				       .CreateShuttleBackupAsync(cancellationToken)
				       .ConfigureAwait(false);
		}
		finally
		{
			await scope.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Removes backup files older than the retention period.
	/// </summary>
	/// <param name="options">The database options containing retention settings.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	///     <para>
	///     This method scans the backup directory for LumaCore Shuttle files (matching
	///     <see cref="SqliteShuttleSchema.FileSearchPattern"/>) and deletes any files older than
	///     <c>Database:AutoMigration:BackupRetentionDays</c>.
	///     </para>
	///     <para>
	///     File age is determined by the <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata stored
	///     inside each Shuttle file, which is immune to file system timestamp changes caused by copy or
	///     move operations. If the metadata cannot be read (e.g., corrupt file), the method falls back to
	///     <c>FileInfo.LastWriteTimeUtc</c>.
	///     </para>
	///     <para>
	///     Individual file deletion failures are logged as warnings and skipped so that one problematic
	///     file does not prevent cleanup of subsequent files.
	///     </para>
	/// </remarks>
	internal async Task CleanupOldBackupsAsync(DatabaseOptions options, CancellationToken cancellationToken)
	{
		try
		{
			ExecutionStageMonitor.ReportStage("CleanupOldBackups.BeforeScan");
			cancellationToken.ThrowIfCancellationRequested();

			// Determine backup directory, exit if it doesn't exist.
			string backupDirectory = DatabaseMaintenanceService.GetBackupDirectory(options);
			if (!Directory.Exists(backupDirectory))
				return;

			// Determine cutoff date.
			DateTimeOffset cutoffDate = mTimeProvider
				.GetUtcNow()
				.AddDays(-options.AutoMigration.BackupRetentionDays);

			// Find and delete old backup files.
			int deletedCount = 0;
			foreach (string file in Directory.EnumerateFiles(backupDirectory, SqliteShuttleSchema.FileSearchPattern))
			{
				try
				{
					DateTimeOffset createdUtc =
						await ReadShuttleCreatedUtcAsync(file, cancellationToken).ConfigureAwait(false)
						?? new DateTimeOffset(new FileInfo(file).LastWriteTimeUtc, TimeSpan.Zero);

					if (createdUtc < cutoffDate)
					{
						mLogger.LogInformation(
							"Deleting old backup file (file: {BackupFilePath}, created: {BackupCreatedUtc})",
							file,
							createdUtc);

						ExecutionStageMonitor.ReportStage("CleanupOldBackups.BeforeDelete");
						new FileInfo(file).Delete();
						deletedCount++;
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					mLogger.LogWarning(
						ex,
						"Failed to clean up backup file: {BackupFilePath}",
						file);
				}
			}

			// Log summary.
			if (deletedCount > 0)
			{
				mLogger.LogInformation(
					"Cleaned up {DeletedBackupCount} backup(s) older than {BackupRetentionDays} days",
					deletedCount,
					options.AutoMigration.BackupRetentionDays);
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// Don't fail startup because of backup cleanup issues.
			mLogger.LogWarning(ex, "Failed to clean up old backups");
		}
	}

	/// <summary>
	/// Attempts to read the <see cref="SqliteShuttleSchema.CreatedUtcKey"/> metadata from a Shuttle file.
	/// </summary>
	/// <param name="filePath">The path to the Shuttle file.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The parsed <see cref="DateTimeOffset"/>, or <see langword="null"/> if the file cannot be opened,
	/// the metadata is missing, or the value cannot be parsed.
	/// </returns>
	private async Task<DateTimeOffset?> ReadShuttleCreatedUtcAsync(string filePath, CancellationToken cancellationToken)
	{
		try
		{
			IShuttleReader reader = mShuttleReaderFactory.Create(filePath);
			try
			{
				await reader.InitializeAsync(cancellationToken).ConfigureAwait(false);
				return await reader.GetCreatedUtcAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// File may be corrupt, incomplete, or locked — fall back to filesystem timestamp.
			mLogger.LogDebug(
				ex,
				"Could not read shuttle metadata from {BackupFilePath}, falling back to file last-write time",
				filePath);
			return null;
		}
	}

	/// <summary>
	/// Restores the database from a portable LumaCore Shuttle backup file after a failed migration.
	/// </summary>
	/// <param name="options">The database configuration options.</param>
	/// <param name="backupPath">The absolute path to the LumaCore Shuttle backup file.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="InvalidOperationException">The Shuttle backup lacks migration history.</exception>
	/// <remarks>
	///     <para>
	///     This method performs a complete database restore in six phases. Phases 3–5 each use their own
	///     <see cref="IServiceScope"/> to ensure clean database connections after destructive operations:
	///     </para>
	///     <list type="number">
	///         <item>
	///         <b>Read and validate Shuttle backup:</b> Opens the SQLite-based LumaCore Shuttle file with a
	///         dedicated reader, extracts migration history (IDs only), runs <c>PRAGMA integrity_check</c> to
	///         detect file-level corruption, and disposes the reader before proceeding.
	///         </item>
	///         <item>
	///         <b>Write restore checkpoint:</b> Persists a checkpoint row to the target database so that an
	///         interrupted restore can be resumed on the next startup.
	///         </item>
	///         <item>
	///         <b>Clean database:</b> Drops all schema objects (tables, views, FKs) via provider-specific cleanup.
	///         Uses its own scope to ensure subsequent phases get a fresh <see cref="LumaCoreDbContext"/>
	///         after schema destruction.
	///         </item>
	///         <item>
	///         <b>Recreate schema:</b> Reapplies EF Core migrations from the <b>project code</b> up to the backup's
	///         last migration ID. Requires a fresh scope to obtain a new connection to the recreated database.
	///         </item>
	///         <item>
	///         <b>Import data:</b> Creates a second shuttle reader and passes it to
	///         <see cref="DataPortService.RunImportAsync"/> which initializes the reader and runs the import.
	///         The caller retains ownership and disposes both reader and scope.
	///         </item>
	///         <item>
	///         <b>Drop checkpoint:</b> Removes the checkpoint table after a successful restore.
	///         </item>
	///     </list>
	///     <para>
	///     <b>Important:</b> This operation is destructive! All current database contents are replaced with the backup state.
	///     </para>
	///     <para>
	///     <b>Note:</b> Phase 4 (schema recreation via <c>MigrateAsync()</c>) is not protected by a transaction.
	///     If the migration itself fails mid-way, the database may be left in a partially migrated state — especially
	///     on MySQL/MariaDB where DDL causes implicit commits. The pre-migration Shuttle backup (created before any
	///     of these phases) is the actual safety net for that scenario. Phase 5 uses per-chunk transactions with
	///     checkpoint-based resume, so a failed import can be resumed from the last committed chunk.
	///     </para>
	/// </remarks>
	private async Task RestoreFromShuttleBackupAsync(
		DatabaseOptions   options,
		string            backupPath,
		CancellationToken cancellationToken)
	{
		// Phase 1: Read LumaCore Shuttle backup, extract migration history, and validate integrity.
		// A dedicated reader is used here and disposed before Phase 3 to release the file handle.
		// Phase 5 creates a separate reader instance that RunImportAsync() will initialize itself.
		ExecutionStageMonitor.ReportStage("RestoreFromBackup.BeforePhase1");
		string? baselineMigration;
		string? shuttleId;
		IShuttleReader historyReader = mShuttleReaderFactory.Create(backupPath);
		try
		{
			await historyReader.InitializeAsync(cancellationToken).ConfigureAwait(false);
			List<MigrationInfo> migrationHistory = await historyReader
				                                       .GetMigrationHistoryAsync(cancellationToken)
				                                       .ConfigureAwait(false);

			// Determine baseline migration (last applied migration in backup).
			baselineMigration = migrationHistory.Count > 0 ? migrationHistory[^1].MigrationId : null;

			// Read the shuttle identity for crash-safe checkpoint matching.
			Dictionary<string, string> metadata = await historyReader
				                                      .GetMetadataAsync(cancellationToken)
				                                      .ConfigureAwait(false);
			metadata.TryGetValue(SqliteShuttleSchema.ShuttleIdKey, out shuttleId);

			// Validate file-level integrity (B-tree structure, page consistency) before any destructive
			// operations. This catches corruption that the completion marker check in InitializeAsync()
			// would miss (e.g., bit rot, truncated files, disk errors in data pages).
			await historyReader.ValidateIntegrityAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await historyReader.DisposeAsync().ConfigureAwait(false);
		}

		if (string.IsNullOrWhiteSpace(baselineMigration))
		{
			throw new InvalidOperationException(
				"Portable backup (LumaCore Shuttle format) does not contain migration history. " +
				"Automatic restore requires EF Core migration history.");
		}

		if (string.IsNullOrWhiteSpace(shuttleId))
		{
			throw new InvalidOperationException(
				"Portable backup (LumaCore Shuttle format) does not contain a ShuttleId. " +
				"Only finalized shuttle files can be used for automatic restore.");
		}

		// Phase 2: Write restore checkpoint before any destructive operations.
		// The checkpoint stores the ShuttleId (not the file path) so the backup can be located
		// by scanning the backup directory — resilient to directory moves and file renames.
		AsyncServiceScope checkpointScope = mServiceProvider.CreateAsyncScope();
		try
		{
			var dbContext = checkpointScope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();
			await WriteRestoreCheckpointAsync(dbContext, shuttleId, baselineMigration, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			await checkpointScope.DisposeAsync().ConfigureAwait(false);
		}

		// Delegate to the shared resume logic which handles Phases 3–6.
		// This is the same code path used when resuming an interrupted restore at startup.
		var checkpoint = new RestoreCheckpointData(
			ShuttleId: shuttleId,
			BaselineMigrationId: baselineMigration,
			Phase: RestoreCheckpointData.PhaseSchemaCleanup,
			StartedUtc: mTimeProvider.GetUtcNow().ToString("O"));

		await ResumeRestoreFromCheckpointAsync(options, checkpoint, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Creates a provider-specific data import writer for restoring portable backups.
	/// </summary>
	/// <param name="options">The database configuration options.</param>
	/// <returns>An <see cref="IDataImportWriter"/> implementation matching the configured provider.</returns>
	private IDataImportWriter CreateImportWriter(DatabaseOptions options) =>
		mProviderOperations.CreateImportWriter(options.ConnectionString, mLogger, mTimeProvider);

	/// <summary>
	/// Removes all database schema objects (tables, views, foreign keys, etc.) in preparation for restore,
	/// while preserving the <c>_lumacore_restore_checkpoint</c> table.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	///     <para>
	///     Delegates to <see cref="IDatabaseProviderOperations.DropSchemaObjectsAsync"/> which implements
	///     the provider-specific drop strategy. The <see cref="RestoreCheckpointTableName"/> table is
	///     passed in the preserve set so it survives the schema cleanup.
	///     </para>
	///     <para>
	///     <b>Warning:</b> This operation is destructive and irreversible! Use only during restore operations.
	///     </para>
	/// </remarks>
	private Task CleanupDatabaseSchemaAsync(
		LumaCoreDbContext dbContext,
		CancellationToken cancellationToken)
	{
		var tablesToPreserve = new HashSet<string>(StringComparer.Ordinal) { RestoreCheckpointTableName };
		return mProviderOperations.DropSchemaObjectsAsync(dbContext, tablesToPreserve, cancellationToken, mLogger);
	}
}
