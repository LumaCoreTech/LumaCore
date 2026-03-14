// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Data.Providers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Initialization;

partial class DatabaseInitializer
{
	/// <summary>
	/// Handles database migrations based on configuration and current state.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="options">The database options.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="DatabaseInitializationException">
	/// An interrupted restore completes (triggers automatic retry via recovery service), an interrupted restore cannot be
	/// resumed (backup file missing, unrecognized phase, integrity failure), when auto-create or auto-migration is
	/// disabled but required, when backup creation fails, or when migration fails.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method is the main entry point for migration handling. Before inspecting pending migrations, it
	///     checks for an interrupted restore checkpoint via <see cref="TryReadRestoreCheckpointAsync"/>. If a
	///     checkpoint exists, the method resumes the restore via <see cref="ResumeRestoreFromCheckpointAsync"/> and
	///     throws <see cref="DatabaseInitializationException"/> with <see cref="DatabaseFailureCategory.Transient"/>
	///     so the current initialization attempt exits cleanly. The <see cref="DatabaseConnectionMonitorService"/>
	///     will retry automatically, and on the next attempt no checkpoint exists — the normal migration flow
	///     proceeds.
	///     </para>
	///     <para>
	///     If the resume fails (e.g., backup file missing, unrecognized phase, integrity check failure), the
	///     exception is wrapped as <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> because
	///     these conditions cannot be resolved by a simple retry — an operator must intervene.
	///     </para>
	///     <para>
	///     When no checkpoint is found, it determines whether the database is new (no migrations applied) or
	///     existing, and delegates to the appropriate handler:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///         <b>New database:</b> <see cref="HandleInitialCreationAsync"/> — applies all migrations if
	///         <see cref="DatabaseOptions.AutoCreate"/> is enabled
	///         </item>
	///         <item>
	///         <b>Existing database:</b> <see cref="HandleUpdateMigrationsAsync"/> — applies pending migrations
	///         with optional backup/restore support
	///         </item>
	///     </list>
	/// </remarks>
	private async Task HandleMigrationsAsync(
		LumaCoreDbContext dbContext,
		DatabaseOptions   options,
		CancellationToken cancellationToken)
	{
		// Before any migration logic, check if a previous restore was interrupted.
		// The checkpoint table acts as a "restore in progress" marker that survives process crashes.
		// If found, resume the restore instead of starting a new migration/backup cycle
		// (which would create a backup of the broken/partially-restored state).
		//
		// On external providers (PostgreSQL, SQL Server) a brand-new database does not exist yet —
		// MigrateAsync() creates it later. Our custom checkpoint query uses raw ADO.NET which would
		// fail with a connection error (e.g., PostgreSQL SQLSTATE 3D000 "invalid_catalog_name").
		// EF Core's own GetAppliedMigrationsAsync() handles non-existent databases internally, but
		// raw ADO.NET does not. If the database doesn't exist, there can't be a checkpoint — skip.
		var databaseCreator = dbContext.GetService<IRelationalDatabaseCreator>();
		bool databaseExists = await databaseCreator.ExistsAsync(cancellationToken).ConfigureAwait(false);

		RestoreCheckpointData? checkpoint = databaseExists
			                                    ? await TryReadRestoreCheckpointAsync(dbContext, cancellationToken)
				                                      .ConfigureAwait(false)
			                                    : null;

		if (checkpoint is not null)
		{
			mLogger.LogWarning(
				"Detected interrupted restore checkpoint (phase: {RestorePhase}, shuttleId: '{ShuttleId}') — " +
				"resuming restore instead of applying migrations",
				checkpoint.Phase,
				checkpoint.ShuttleId);

			try
			{
				await ResumeRestoreFromCheckpointAsync(options, checkpoint, cancellationToken)
					.ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// Resume failed — the backup file may be missing, the checkpoint phase may be unrecognized,
				// or the integrity check may have failed. None of these can be resolved by a simple retry;
				// an operator must either restore the backup file or delete the checkpoint table.
				mLogger.LogCritical(
					ex,
					"Failed to resume interrupted restore (shuttleId: '{ShuttleId}', phase: '{Phase}') — " +
					"manual intervention is required",
					checkpoint.ShuttleId,
					checkpoint.Phase);

				throw new DatabaseInitializationException(
					"Failed to resume interrupted restore. Either restore the backup file to the backup " +
					$"directory or delete the '{RestoreCheckpointTableName}' table from the database to " +
					"skip the restore and proceed with normal migration.",
					DatabaseFailureCategory.ManualInterventionRequired,
					ex);
			}

			// Restore completed — the database is back to pre-migration state.
			// Throw Transient so the app doesn't immediately re-enter the migration/backup cycle
			// in the same call. The DatabaseConnectionMonitorService will pick up the Transient
			// failure and retry initialization automatically — at that point, no checkpoint exists
			// and the normal migration flow proceeds.
			throw new DatabaseInitializationException(
				"Interrupted restore completed successfully. The database has been restored to the pre-migration " +
				"state. Pending migrations will be applied automatically on the next recovery cycle.",
				DatabaseFailureCategory.Transient);
		}

		// Get migrations that have already been applied to the database.
		List<string> appliedMigrations = (await dbContext.Database
			                                  .GetAppliedMigrationsAsync(cancellationToken)
			                                  .ConfigureAwait(false))
			.ToList();

		// Get migrations that are defined in the application but have not yet been applied to the database.
		List<string> pendingMigrations = (await dbContext.Database
			                                  .GetPendingMigrationsAsync(cancellationToken)
			                                  .ConfigureAwait(false))
			.ToList();

		// Determine if this is a new database (no migrations applied yet).
		bool isNewDatabase = appliedMigrations.Count == 0;

		// No pending migrations - nothing to do.
		if (pendingMigrations.Count == 0)
		{
			mLogger.LogDebug("No pending migrations — database is up to date");
			return;
		}

		// Log pending migrations.
		mLogger.LogInformation(
			"Found {PendingMigrationCount} pending migration(s): {PendingMigrations}",
			pendingMigrations.Count,
			string.Join(", ", pendingMigrations));

		// Handle new database vs. update scenarios.
		// New database: apply all migrations directly (no backup needed).
		// Existing database: handle according to auto-migration settings.
		if (isNewDatabase)
		{
			// First-time setup scenario.
			await HandleInitialCreationAsync(dbContext, options, pendingMigrations, cancellationToken)
				.ConfigureAwait(false);
		}
		else
		{
			// Update scenario - database exists with some migrations applied.
			await HandleUpdateMigrationsAsync(dbContext, options, pendingMigrations, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Handles initial database creation for new installations.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="options">The database options.</param>
	/// <param name="pendingMigrations">The list of pending migrations to apply.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="DatabaseInitializationException">
	/// <see cref="DatabaseOptions.AutoCreate"/> is <see langword="false"/> and migrations are pending.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method handles the first-time setup scenario where no migrations have been applied yet.
	///     No backup is created because there is no existing data to protect.
	///     </para>
	///     <para>
	///     If <see cref="DatabaseOptions.AutoCreate"/> is disabled, an exception is thrown requiring
	///     manual migration via <c>dotnet ef database update</c>.
	///     </para>
	/// </remarks>
	private async Task HandleInitialCreationAsync(
		LumaCoreDbContext dbContext,
		DatabaseOptions   options,
		List<string>      pendingMigrations,
		CancellationToken cancellationToken)
	{
		if (!options.AutoCreate)
		{
			mLogger.LogError(
				"Database is empty and AutoCreate is disabled. " +
				"Please run migrations manually or enable AutoCreate. " +
				"Pending migrations: {PendingMigrations}",
				string.Join(", ", pendingMigrations));

			throw new DatabaseInitializationException(
				"Database is empty and AutoCreate is disabled. Run 'dotnet ef database update' manually or set Database:AutoCreate=true.",
				DatabaseFailureCategory.ConfigurationRequired);
		}

		mLogger.LogInformation("New database detected — creating initial schema...");

		// No backup needed for new database (nothing to lose)
		await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

		mLogger.LogInformation("Initial database schema created successfully");
	}

	/// <summary>
	/// Handles migration updates for existing databases.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="options">The database options.</param>
	/// <param name="pendingMigrations">The list of pending migrations to apply.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <exception cref="DatabaseInitializationException">
	/// <c>Database:AutoMigration:Enabled</c> is <see langword="false"/> and migrations are pending,
	/// when backup creation fails with backup enabled, or when migration fails (with or without restore attempt).
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method handles the update scenario where the database already has some migrations applied.
	///     It implements the following flow:
	///     </para>
	///     <list type="number">
	///         <item>Check if auto-migration is enabled; throw if disabled</item>
	///         <item>
	///         Create a LumaCore Shuttle backup if <c>Database:AutoMigration:CreateBackupBeforeMigration</c> is enabled
	///         </item>
	///         <item>Apply pending migrations</item>
	///         <item>
	///         On failure: attempt automatic restore if <c>Database:AutoMigration:RestoreOnFailure</c> is enabled
	///         and a backup exists. After successful restore, the failure category is determined by
	///         <see cref="IDatabaseProviderOperations.IsServiceUnavailable"/>: transient infrastructure errors
	///         get <see cref="DatabaseFailureCategory.Transient"/> (automatic retry); persistent errors get
	///         <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>
	///         </item>
	///         <item>Clean up old backups based on <c>Database:AutoMigration:BackupRetentionDays</c></item>
	///     </list>
	///     <para>
	///     If migration fails, the original migration exception is always preserved in the thrown
	///     <see cref="DatabaseInitializationException"/>. When both migration and restore fail, both exceptions are
	///     wrapped in an <see cref="AggregateException"/> as the inner exception.
	///     </para>
	/// </remarks>
	private async Task HandleUpdateMigrationsAsync(
		LumaCoreDbContext dbContext,
		DatabaseOptions   options,
		List<string>      pendingMigrations,
		CancellationToken cancellationToken)
	{
		// Handle disabled auto-migration with pending migrations.
		// In this case, we cannot proceed.
		if (!options.AutoMigration.Enabled)
		{
			mLogger.LogError(
				"AutoMigrate is disabled but {PendingMigrationCount} pending migration(s) found: {PendingMigrations} — " +
				"run 'dotnet ef database update' manually or set Database:AutoMigrate=true",
				pendingMigrations.Count,
				string.Join(", ", pendingMigrations));

			throw new DatabaseInitializationException(
				$"Database has {pendingMigrations.Count} pending migration(s) and AutoMigrate is disabled. " +
				$"Run 'dotnet ef database update' manually or set Database:AutoMigrate=true.",
				DatabaseFailureCategory.ConfigurationRequired);
		}

		// Create backup, if configured.
		// If migration fails later, we can restore from this backup.
		// On retries after a failed migration, reuse the existing backup instead of re-exporting
		// the unchanged database state (see mLastBackupPath remarks).
		string? backupPath = null;
		if (options.AutoMigration.CreateBackupBeforeMigration)
		{
			// Check if a backup from a previous attempt in this retry cycle is still available.
			if (mLastBackupPath is not null && File.Exists(mLastBackupPath))
			{
				backupPath = mLastBackupPath;
				mLogger.LogInformation(
					"Reusing existing pre-migration backup from previous attempt: {BackupPath}",
					backupPath);
			}
			else
			{
				// No reusable backup — clear stale reference and create a fresh one.
				mLastBackupPath = null;

				try
				{
					backupPath = await CreateBackupAndReturnPathAsync(cancellationToken).ConfigureAwait(false);
					mLastBackupPath = backupPath;
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					// Backup failed — we can't safely proceed with migrations without a safety net.
					// The database has NOT been modified at this point, so the failure category depends
					// on whether the error is a transient infrastructure issue (connection drop, timeout)
					// or a persistent local problem (disk full, permissions, invalid path).
					DatabaseFailureCategory category = mProviderOperations.IsServiceUnavailable(ex)
						                                   ? DatabaseFailureCategory.Transient
						                                   : DatabaseFailureCategory.ManualInterventionRequired;

					mLogger.LogCritical(
						ex,
						"Automatic migration backup is enabled, but creating the portable backup failed " +
						"(category: {FailureCategory}) — aborting startup without applying migrations",
						category);

					throw new DatabaseInitializationException(
						"Failed to create backup before migration. Cannot safely proceed without a backup. " +
						"Check disk space, permissions, and backup directory configuration, then restart the application.",
						category,
						ex);
				}
			}
		}

		// Apply migrations within try-catch to handle failures.
		// On failure, attempt automatic restore from LumaCore Shuttle backup if configured.
		// If restore also fails, log critical error for manual intervention.
		try
		{
			ExecutionStageMonitor.ReportStage("HandleUpdateMigrations.BeforeMigrate");
			mLogger.LogInformation("Applying {MigrationCount} migration(s)...", pendingMigrations.Count);
			await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
			mLogger.LogInformation("Migrations applied successfully");

			// Migration succeeded — the pre-migration backup is no longer needed for rollback.
			// Clear the cached path so the next migration cycle (if any) creates a fresh backup.
			mLastBackupPath = null;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			if (options.AutoMigration.RestoreOnFailure && !string.IsNullOrWhiteSpace(backupPath))
			{
				mLogger.LogCritical(
					ex,
					"Database migration failed — " +
					"starting automatic restore from portable backup (LumaCore Shuttle format) '{BackupPath}'",
					backupPath);

				try
				{
					await RestoreFromShuttleBackupAsync(options, backupPath, cancellationToken).ConfigureAwait(false);

					// Restore succeeded — database is back to pre-migration state.
					// Use provider-specific error classification to distinguish transient infrastructure
					// failures (connection drop, lock timeout, disk I/O) from persistent code failures.
					// Transient failures get automatic retry; persistent failures require a code fix.
					if (mProviderOperations.IsServiceUnavailable(ex))
					{
						mLogger.LogInformation(
							"Automatic restore from portable backup completed — migration failure " +
							"classified as transient, automatic retry will be attempted");

						throw new DatabaseInitializationException(
							"Database migration failed due to a transient error but was automatically " +
							"restored from backup. Automatic retry will be attempted.",
							DatabaseFailureCategory.Transient,
							ex);
					}

					mLogger.LogWarning(
						"Automatic restore from portable backup completed — migration failure classified " +
						"as persistent (not a transient infrastructure issue), " +
						"resolve the underlying issue and restart the application");

					throw new DatabaseInitializationException(
						"Database migration failed but was automatically restored from backup. " +
						"Review the migration error, resolve the underlying issue, and restart the application.",
						DatabaseFailureCategory.ManualInterventionRequired,
						ex);
				}
				catch (DatabaseInitializationException)
				{
					// Re-throw our own exception (created above).
					throw;
				}
				catch (OperationCanceledException)
				{
					// Cancellation during restore (e.g., application shutdown) should propagate cleanly
					// rather than being wrapped as ManualInterventionRequired. The database is in an
					// unknown state, but the next startup will retry via the recovery service.
					throw;
				}
				catch (Exception restoreEx)
				{
					mLogger.LogCritical(
						restoreEx,
						"Automatic restore from portable backup failed — " +
						"manual intervention required, backup: '{BackupPath}'",
						backupPath);

					// Both migration AND restore failed — database is in unknown state.
					// Wrap both exceptions so the full diagnostic chain is available:
					// AggregateException.InnerExceptions[0] = migration failure (root cause)
					// AggregateException.InnerExceptions[1] = restore failure (why recovery failed)
					throw new DatabaseInitializationException(
						$"Database migration failed and automatic restore also failed. " +
						$"Manual intervention is required. Backup file: {backupPath}",
						DatabaseFailureCategory.ManualInterventionRequired,
						new AggregateException(ex, restoreEx));
				}
			}
			else
			{
				mLogger.LogCritical(
					ex,
					"Database migration failed — automatic restore is disabled or no portable backup was created");

				// No backup available or restore disabled — database may be in inconsistent state.
				throw new DatabaseInitializationException(
					"Database migration failed. No backup was available for automatic restore. " +
					"Manual intervention may be required to fix or restore the database.",
					DatabaseFailureCategory.ManualInterventionRequired,
					ex);
			}
		}

		// Cleanup old backups based on retention policy.
		// This only runs on successful migration (all failure paths throw above).
		if (options.AutoMigration is { CreateBackupBeforeMigration: true, BackupRetentionDays: > 0 })
		{
			await CleanupOldBackupsAsync(options, cancellationToken).ConfigureAwait(false);
		}
	}
}
