// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Providers;
using LumaCore.Data.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Initialization;

partial class DatabaseInitializer
{
	/// <summary>
	/// Name of the restore checkpoint table created in the target database during restore operations.
	/// </summary>
	/// <remarks>
	/// This table acts as a "restore in progress" marker. If it exists at startup, the initializer knows a
	/// previous restore was interrupted and resumes it instead of starting a new migration/backup cycle.
	/// The table is dropped after a successful restore.
	/// </remarks>
	internal const string RestoreCheckpointTableName = "_lumacore_restore_checkpoint";

	/// <summary>
	/// Checks whether the restore checkpoint table exists and reads its contents if present.
	/// </summary>
	/// <param name="dbContext">The database context for accessing the database connection.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A <see cref="RestoreCheckpointData"/> instance if a checkpoint exists; otherwise, <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// Delegates to <see cref="IDatabaseProviderOperations.ReadCheckpointAsync"/> which handles the
	/// provider-specific table existence check and row reading.
	/// </remarks>
	internal Task<RestoreCheckpointData?> TryReadRestoreCheckpointAsync(
		LumaCoreDbContext dbContext,
		CancellationToken cancellationToken)
	{
		DbConnection connection = dbContext.Database.GetDbConnection();
		return mProviderOperations.ReadCheckpointAsync(
			connection,
			RestoreCheckpointTableName,
			cancellationToken);
	}

	/// <summary>
	/// Creates the restore checkpoint table and writes the initial checkpoint row.
	/// </summary>
	/// <param name="dbContext">The database context for executing provider-specific statements.</param>
	/// <param name="shuttleId">The unique identity of the Shuttle backup file being restored.</param>
	/// <param name="baselineMigrationId">The target migration ID from the backup's migration history.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// Delegates to <see cref="IDatabaseProviderOperations.WriteCheckpointAsync"/> which handles
	/// table creation, stale-row cleanup, and insertion with provider-specific DDL and quoting.
	/// </remarks>
	internal Task WriteRestoreCheckpointAsync(
		LumaCoreDbContext dbContext,
		string            shuttleId,
		string            baselineMigrationId,
		CancellationToken cancellationToken)
	{
		string now = mTimeProvider.GetUtcNow().ToString("O");
		return mProviderOperations.WriteCheckpointAsync(
			dbContext,
			RestoreCheckpointTableName,
			shuttleId,
			baselineMigrationId,
			now,
			cancellationToken);
	}

	/// <summary>
	/// Updates the phase column of the restore checkpoint to track progress across restarts.
	/// </summary>
	/// <param name="dbContext">The database context for executing provider-specific statements.</param>
	/// <param name="phase">
	/// The new phase value (e.g., <see cref="RestoreCheckpointData.PhaseMigration"/> or
	/// <see cref="RestoreCheckpointData.PhaseImport"/>).
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	internal Task UpdateRestoreCheckpointPhaseAsync(
		LumaCoreDbContext dbContext,
		string            phase,
		CancellationToken cancellationToken)
	{
		string now = mTimeProvider.GetUtcNow().ToString("O");
		return mProviderOperations.UpdateCheckpointPhaseAsync(
			dbContext,
			RestoreCheckpointTableName,
			phase,
			now,
			cancellationToken);
	}

	/// <summary>
	/// Drops the restore checkpoint table after a successful restore.
	/// </summary>
	/// <param name="dbContext">The database context for executing provider-specific statements.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	internal Task DropRestoreCheckpointTableAsync(
		LumaCoreDbContext dbContext,
		CancellationToken cancellationToken) => mProviderOperations.DropCheckpointTableAsync(
		dbContext,
		RestoreCheckpointTableName,
		cancellationToken);

	/// <summary>
	/// Resumes a restore operation that was interrupted during a previous startup.
	/// </summary>
	/// <param name="options">The database configuration options.</param>
	/// <param name="checkpoint">The checkpoint data read from the database.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="FileNotFoundException">
	/// No shuttle file matching the checkpoint's <see cref="RestoreCheckpointData.ShuttleId"/> can be found
	/// in the configured backup directory.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The checkpoint phase is not recognized by this application version.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method resumes the restore from the phase recorded in the checkpoint. Phases that have already
	///     completed are skipped. The phase progression is:
	///     </para>
	///     <list type="number">
	///         <item>
	///         <see cref="RestoreCheckpointData.PhaseSchemaCleanup"/> — schema drop not yet completed → start
	///         from Phase 3
	///         </item>
	///         <item>
	///         <see cref="RestoreCheckpointData.PhaseMigration"/> — schema was dropped, migration not yet
	///         completed → start from Phase 4
	///         </item>
	///         <item>
	///         <see cref="RestoreCheckpointData.PhaseImport"/> — migration completed, import not yet completed →
	///         start from Phase 5
	///         </item>
	///     </list>
	///     <para>
	///     The backup file is located by scanning the backup directory for a shuttle file whose
	///     <see cref="SqliteShuttleSchema.ShuttleIdKey"/> metadata matches the checkpoint's
	///     <see cref="RestoreCheckpointData.ShuttleId"/>. This makes the checkpoint resilient to
	///     directory moves and file renames — only the file content matters.
	///     </para>
	///     <para>
	///     The import phase (Phase 5) benefits from the chunk-level checkpoint system in
	///     <see cref="IDataImportWriter"/>, which tracks per-table progress. This means a partially completed
	///     import can resume from the last committed chunk rather than re-importing all data.
	///     </para>
	///     <para>
	///     After all applicable phases complete, the restore checkpoint table is dropped (Phase 6) to indicate a
	///     successful restore. This prevents the next startup from re-entering the restore flow.
	///     </para>
	/// </remarks>
	internal async Task ResumeRestoreFromCheckpointAsync(
		DatabaseOptions       options,
		RestoreCheckpointData checkpoint,
		CancellationToken     cancellationToken)
	{
		// Locate the shuttle file by scanning the backup directory for a matching ShuttleId.
		// This is resilient to directory moves and file renames — only the file identity matters.
		string resolvedPath = await FindShuttleFileByIdAsync(options, checkpoint.ShuttleId, cancellationToken)
			                      .ConfigureAwait(false);

		mLogger.LogWarning(
			"Resuming interrupted restore from phase '{RestorePhase}' " +
			"(shuttleId: '{ShuttleId}', file: '{ResolvedPath}', baseline: '{BaselineMigrationId}', " +
			"started: {RestoreStartedUtc})",
			checkpoint.Phase,
			checkpoint.ShuttleId,
			resolvedPath,
			checkpoint.BaselineMigrationId,
			checkpoint.StartedUtc);

		// Validate backup integrity before proceeding. Even when resuming from a later phase
		// (where the database may already be wiped), an early integrity check produces a clear
		// error message instead of a cryptic import failure deep in Phase 5.
		IShuttleReader integrityReader = mShuttleReaderFactory.Create(resolvedPath);
		try
		{
			await integrityReader.InitializeAsync(cancellationToken).ConfigureAwait(false);
			await integrityReader.ValidateIntegrityAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await integrityReader.DisposeAsync().ConfigureAwait(false);
		}

		// Determine which phases to execute based on the checkpoint.
		bool executeSchemaCleanup = string.Equals(
			checkpoint.Phase,
			RestoreCheckpointData.PhaseSchemaCleanup,
			StringComparison.Ordinal);
		bool executeMigration = executeSchemaCleanup ||
		                        string.Equals(
			                        checkpoint.Phase,
			                        RestoreCheckpointData.PhaseMigration,
			                        StringComparison.Ordinal);
		bool executeImport = executeMigration ||
		                     string.Equals(
			                     checkpoint.Phase,
			                     RestoreCheckpointData.PhaseImport,
			                     StringComparison.Ordinal);

		if (!executeImport)
		{
			throw new InvalidOperationException(
				$"Unrecognized restore checkpoint phase: '{checkpoint.Phase}'. " +
				$"Expected '{RestoreCheckpointData.PhaseSchemaCleanup}', " +
				$"'{RestoreCheckpointData.PhaseMigration}', or '{RestoreCheckpointData.PhaseImport}'.");
		}

		// Phase 3: Drop schema (excluding checkpoint table).
		if (executeSchemaCleanup)
		{
			AsyncServiceScope cleanupScope = mServiceProvider.CreateAsyncScope();
			try
			{
				var dbContext = cleanupScope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();
				await CleanupDatabaseSchemaAsync(dbContext, cancellationToken).ConfigureAwait(false);

				// Update checkpoint to indicate schema cleanup is complete.
				await UpdateRestoreCheckpointPhaseAsync(
						dbContext,
						RestoreCheckpointData.PhaseMigration,
						cancellationToken)
					.ConfigureAwait(false);
			}
			finally
			{
				await cleanupScope.DisposeAsync().ConfigureAwait(false);
			}
		}

		// Phase 4: Recreate schema via migrations.
		if (executeMigration)
		{
			mLogger.LogInformation(
				"Rebuilding database schema to baseline migration '{BaselineMigrationId}'...",
				checkpoint.BaselineMigrationId);

			AsyncServiceScope migrateScope = mServiceProvider.CreateAsyncScope();
			try
			{
				var dbContext = migrateScope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();
				var migrator = dbContext.GetService<IMigrator>();
				await migrator
					.MigrateAsync(checkpoint.BaselineMigrationId, cancellationToken)
					.ConfigureAwait(false);

				// Update checkpoint to indicate migration is complete.
				// Use a fresh scope because MigrateAsync() may have invalidated the connection.
				AsyncServiceScope updateScope = mServiceProvider.CreateAsyncScope();
				try
				{
					var updateContext = updateScope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();
					await UpdateRestoreCheckpointPhaseAsync(
							updateContext,
							RestoreCheckpointData.PhaseImport,
							cancellationToken)
						.ConfigureAwait(false);
				}
				finally
				{
					await updateScope.DisposeAsync().ConfigureAwait(false);
				}
			}
			finally
			{
				await migrateScope.DisposeAsync().ConfigureAwait(false);
			}
		}

		// Phase 5: Import data.
		if (executeImport)
		{
			AsyncServiceScope importScope = mServiceProvider.CreateAsyncScope();
			try
			{
				var dataPortService = importScope.ServiceProvider.GetRequiredService<DataPortService>();
				IShuttleReader importReader = mShuttleReaderFactory.Create(resolvedPath);
				try
				{
					IDataImportWriter targetImporter = CreateImportWriter(options);
					try
					{
						await dataPortService
							.RunImportAsync(importReader, targetImporter, progress: null, cancellationToken)
							.ConfigureAwait(false);
					}
					finally
					{
						await targetImporter.DisposeAsync().ConfigureAwait(false);
					}
				}
				finally
				{
					await importReader.DisposeAsync().ConfigureAwait(false);
				}
			}
			finally
			{
				await importScope.DisposeAsync().ConfigureAwait(false);
			}
		}

		// Phase 6: Drop checkpoint table — restore completed successfully.
		AsyncServiceScope finalScope = mServiceProvider.CreateAsyncScope();
		try
		{
			var dbContext = finalScope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();
			await DropRestoreCheckpointTableAsync(dbContext, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await finalScope.DisposeAsync().ConfigureAwait(false);
		}

		mLogger.LogInformation("Interrupted restore completed successfully");
	}

	/// <summary>
	/// Scans the configured backup directory for a shuttle file whose
	/// <see cref="SqliteShuttleSchema.ShuttleIdKey"/> metadata matches <paramref name="shuttleId"/>.
	/// </summary>
	/// <param name="options">The database configuration options (provides the backup directory).</param>
	/// <param name="shuttleId">The shuttle identity to search for.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The absolute path to the matching shuttle file.</returns>
	/// <exception cref="FileNotFoundException">
	/// The backup directory does not exist or contains no shuttle file with the given
	/// <paramref name="shuttleId"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     Each shuttle file is opened briefly to read its metadata table (~8–12 KB I/O per file regardless
	///     of file size). Files that fail to initialize (corrupt, incomplete) are skipped with a debug log.
	///     </para>
	///     <para>
	///     Files are scanned in reverse filename order (newest first) because the backup that matches the
	///     checkpoint was typically created moments before the crash. With timestamp-prefixed filenames
	///     (<c>lumacore-{yyyyMMdd-HHmmss-fff}-{suffix}</c>), this means the match is usually found on the
	///     first attempt, avoiding unnecessary I/O on older backups.
	///     </para>
	/// </remarks>
	private async Task<string> FindShuttleFileByIdAsync(
		DatabaseOptions   options,
		string            shuttleId,
		CancellationToken cancellationToken)
	{
		string backupDirectory = DatabaseMaintenanceService.GetBackupDirectory(options);

		if (!Directory.Exists(backupDirectory))
		{
			throw new FileNotFoundException(
				$"Cannot resume interrupted restore: backup directory '{backupDirectory}' does not exist. " +
				$"No shuttle file with ID '{shuttleId}' can be located.",
				shuttleId);
		}

		// Scan newest files first — the matching backup was typically created moments before the crash.
		foreach (string filePath in Directory
			         .EnumerateFiles(backupDirectory, SqliteShuttleSchema.FileSearchPattern)
			         .OrderDescending())
		{
			IShuttleReader reader = mShuttleReaderFactory.Create(filePath);
			try
			{
				await reader.InitializeAsync(cancellationToken).ConfigureAwait(false);
				Dictionary<string, string> metadata = await reader
					                                      .GetMetadataAsync(cancellationToken)
					                                      .ConfigureAwait(false);

				if (metadata.TryGetValue(SqliteShuttleSchema.ShuttleIdKey, out string? fileShuttleId)
				    && string.Equals(fileShuttleId, shuttleId, StringComparison.Ordinal))
				{
					return filePath;
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				// File might be corrupt or not a valid shuttle — skip and continue scanning.
				mLogger.LogDebug(
					ex,
					"Skipping '{FilePath}' during shuttle scan: not a valid shuttle file or read failed",
					filePath);
			}
			finally
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}

		throw new FileNotFoundException(
			$"Cannot resume interrupted restore: no shuttle file with ID '{shuttleId}' found " +
			$"in backup directory '{backupDirectory}'. The backup file may have been moved or deleted.",
			shuttleId);
	}
}
