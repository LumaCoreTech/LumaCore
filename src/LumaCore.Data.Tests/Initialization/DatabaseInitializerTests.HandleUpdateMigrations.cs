// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;
using System.Data.Common;

using LumaCore.Core.Diagnostics;
using LumaCore.Core.IO;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Entities;
using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;
using LumaCore.Data.Services;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using Xunit;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable RedundantVerbatimStringPrefix

namespace LumaCore.Data.Tests.Initialization;

// Update-migration pipeline: from the simplest migration success to the worst-case double failure.
//
// When an existing database has pending migrations, the initializer walks through a series of
// increasingly defensive steps. These tests follow that escalation path:
//
//   1. Happy path: migration succeeds with and without backup
//      (AppliesPendingMigration, SkipsCleanupAndCompletes, CreatesBackupAppliesMigrationAndCleansUp).
//
//   2. Configuration gate: AutoMigrate disabled → ConfigurationRequired, no retry.
//
//   3. Backup creation failures: persistent (IOException → ManualIntervention) vs.
//      transient (TimeoutException → Transient, auto-retry) vs.
//      cancellation (OperationCanceledException → propagates cleanly).
//      In all non-cancellation cases, no migration is attempted.
//
//   4. Migration failure without backup: no safety net → ManualInterventionRequired.
//
//   5. Migration failure with backup + transient error: restore succeeds → Transient
//      (auto-retry on next startup).
//
//   6. Migration failure with backup + non-transient error: restore succeeds →
//      ManualInterventionRequired (operator must fix the migration before restarting).
//
//   7. Double failure: migration fails AND restore fails → AggregateException with
//      both exceptions preserved. ManualInterventionRequired.
//
//   8. Corrupt backup: empty migration history → restore cannot determine baseline →
//      same double-failure handling as (7).
//
//   9. Cancellation: OperationCanceledException during migration or restore propagates
//      cleanly without being misclassified.
//
//  10. Checkpoint detection at startup: valid backup → restore + Transient;
//      missing backup or unrecognized phase → ManualInterventionRequired;
//      cancellation during checkpoint resume propagates cleanly.
//
//  11. Edge cases: retention=0 (keep all backups), data fidelity through roundtrip,
//      RestoreOnFailure=true but no backup created, backup reuse on retry
//      (mLastBackupPath), stale backup reference cleared when file deleted.
//
// Prerequisites covered elsewhere: StartAsync (lifecycle, seeding, retry mechanics),
// ResumeRestore (6-phase restore internals in isolation).
public sealed partial class DatabaseInitializerTests
{
	#region HandleUpdateMigrationsAsync()

	// --- 1. Happy path: migration succeeds, backup optional ---

	/// <summary>
	/// Verifies the simplest happy path through <c>HandleUpdateMigrationsAsync()</c>: an existing database
	/// with only the first migration applied, <c>AutoMigration.Enabled = true</c>, and
	/// <c>CreateBackupBeforeMigration = false</c> successfully applies all pending migrations and completes
	/// initialization.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, backup creation disabled. This means the flow skips
	///     <c>CreateBackupAndReturnPathAsync()</c> entirely and proceeds straight to <c>MigrateAsync()</c>.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> All pending migrations are applied, initialization completes with
	///     <see cref="DatabaseInitializationState.Completed"/>. No backup files are created, no cleanup runs.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenAutoMigrateEnabledWithoutBackup_AppliesPendingMigration()
	{
		// Arrange — apply only the first migration, then let StartAsync() handle the remaining ones.
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = false;
		});
		try
		{
			// Apply only the very first migration, leaving the following ones pending for StartAsync() to handle.
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — all pending migrations applied successfully.
			await AssertCompletedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that after a successful update migration with <c>CreateBackupBeforeMigration = false</c>,
	/// the post-migration <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> is <b>not</b> invoked —
	/// even when <c>BackupRetentionDays</c> is configured to a positive value.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; a 30-day-old sentinel
	///     <c>.shuttle.sqlite</c> file exists in the backup directory.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, backup creation <b>disabled</b>,
	///     <c>BackupRetentionDays = 7</c>. The retention-policy guard at the end of
	///     <c>HandleUpdateMigrationsAsync()</c> requires <b>both</b> <c>CreateBackupBeforeMigration = true</c>
	///     <b>and</b> <c>BackupRetentionDays &gt; 0</c>, so cleanup must be skipped.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> Migrations succeed, initialization completes, and the old sentinel file
	///     survives — proving that <c>CleanupOldBackupsAsync()</c> was never called.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenAutoMigrateSucceedsWithBackupDisabled_SkipsCleanupAndCompletes()
	{
		// Arrange — place an old shuttle file in the backup directory as a sentinel.
		// If CleanupOldBackupsAsync() ran, it would delete this file (it's beyond the 7-day retention).
		using var backupDir = new TemporaryFolder("dbinit-nocleanup");
		string sentinelFile = Path.Combine(backupDir.Path, "old-sentinel.shuttle.sqlite");

		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = false;
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await TestHarness.CreateMinimalShuttleFileAsync(
				sentinelFile,
				harness.TimeProvider.GetUtcNow().AddDays(-30));

			// Apply only the very first migration, leaving the following ones pending for StartAsync() to handle.
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Act — migration succeeds, but cleanup is skipped because backup creation was disabled.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			await AssertCompletedAsync(harness);

			// Sentinel file survived — proves CleanupOldBackupsAsync() was not invoked.
			Assert.True(File.Exists(sentinelFile));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies the full happy path through <c>HandleUpdateMigrationsAsync()</c> with backup enabled:
	/// a shuttle backup is created before migration, all pending migrations are applied successfully,
	/// and the post-migration <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/> runs.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     A dedicated temporary backup directory is empty.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>BackupRetentionDays = 7</c>. This exercises all three stages of the method:
	///     </para>
	///     <list type="number">
	///         <item>
	///         <c>CreateBackupAndReturnPathAsync()</c> — shuttle backup is created (returns non-null path)
	///         </item>
	///         <item><c>MigrateAsync()</c> — pending migrations are applied</item>
	///         <item><c>CleanupOldBackupsAsync()</c> — retention-policy guard passes (both conditions met)</item>
	///     </list>
	///     <para>
	///     <b>Expected outcome:</b> Initialization completes. The backup directory contains exactly one
	///     <c>.shuttle.sqlite</c> file whose migration history matches only the first migration (backup was
	///     taken <b>before</b> the pending migrations were applied).
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenAutoMigrateWithBackupEnabled_CreatesBackupAppliesMigrationAndCleansUp()
	{
		// Arrange — use a dedicated temp directory for backups so we can verify file creation.
		using var backupDir = new TemporaryFolder("dbinit-backup");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.BackupRetentionDays = 7;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			// Apply only the very first migration, leaving the following ones pending for StartAsync() to handle.
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Act — backup is created, then the pending migrations are applied, then cleanup runs.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — initialization completed successfully with all migrations applied.
			await AssertCompletedAsync(harness);

			// Verify that a shuttle backup file was actually created and is structurally valid.
			// The backup is taken before pending migrations are applied, so it contains only the first migration.
			Assert.True(Directory.Exists(backupDir.Path));
			string[] backupFiles = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
			Assert.Single(backupFiles);
			await AssertShuttleBackupIntegrityAsync(backupFiles[0], [FirstMigrationId]);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Configuration gate: AutoMigrate disabled ---

	/// <summary>
	/// Verifies that an existing database with pending migrations and <c>AutoMigration.Enabled = false</c>
	/// fails immediately with <see cref="DatabaseFailureCategory.ConfigurationRequired"/>, without attempting
	/// to apply any migrations.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> <c>AutoMigration.Enabled = false</c>. This is the very first guard in
	///     <c>HandleUpdateMigrationsAsync()</c> — no backup creation, no <c>MigrateAsync()</c> call, and no
	///     cleanup is reached.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ConfigurationRequired"/>. The failure message instructs the
	///     operator to either run migrations manually or enable <c>AutoMigrate</c>. <c>ShouldRetry</c> is
	///     <see langword="false"/> because retrying without a configuration change would produce the same result.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenAutoMigrateDisabledOnExistingDatabase_FailsWithConfigurationRequired()
	{
		// Arrange — apply only the first migration, then disable auto-migration.
		TestHarness harness = CreateHarness(options => options.AutoMigration.Enabled = false);
		try
		{
			// Apply only the first migration, leaving subsequent ones as pending.
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Act — StartAsync() detects existing DB + pending migration + AutoMigrate disabled.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ConfigurationRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<DatabaseInitializationException>(harness.Status.FailureException);
			Assert.Matches(
				@"^Database has \d+ pending migration\(s\) and AutoMigrate is disabled\. " +
				@"Run 'dotnet ef database update' manually or set Database:AutoMigrate=true\.$",
				ex.Message);
			Assert.Equal(ex.Message, harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Backup creation failures: no backup means no migration ---

	/// <summary>
	/// Verifies that when backup creation throws an exception (e.g., disk full or permission error),
	/// the initialization aborts <b>before</b> applying any pending migrations and reports
	/// <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> — all realistic backup failure
	/// modes (disk full, permission denied, invalid path) require operator intervention; retrying
	/// automatically would hit the same error.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>.
	///     An <see cref="IOException"/> is injected via <see cref="ExecutionStageMonitor"/> at the
	///     <c>CreateBackup.BeforeCreate</c> stage, simulating a disk I/O failure during shuttle backup creation.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>catch (Exception ex)</c> block around
	///     <c>CreateBackupAndReturnPathAsync()</c> in <c>HandleUpdateMigrationsAsync()</c>. This is a safety
	///     guard that prevents migrations from running without a backup safety net.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. The original <see cref="IOException"/>
	///     is surfaced as the direct <c>FailureException</c> on the status (same instance). <c>ShouldRetry</c> is
	///     <see langword="false"/> because the operator must resolve the disk/permission issue first. Critically,
	///     no pending migrations are applied — the database remains at first-migration-only state.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenBackupCreationFails_FailsWithManualInterventionAndSkipsMigration()
	{
		// Arrange — apply first migration, enable backup, inject failure via stage monitor.
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
		});
		try
		{
			// Apply only the first migration, leaving subsequent ones as pending.
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Inject an IOException during backup creation to simulate a disk failure.
			// This should cause StartAsync() to fail with ManualInterventionRequired and skip applying the pending migration.
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt("CreateBackup.BeforeCreate", new IOException("Simulated disk failure"));

			// Act — backup creation fails → initialization aborts before applying migrations.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<IOException>(harness.Status.FailureException);
			Assert.Equal("Simulated disk failure", ex.Message);
			Assert.Equal(
				"Failed to create backup before migration. Cannot safely proceed without a backup. " +
				"Check disk space, permissions, and backup directory configuration, then restart the application.",
				harness.Status.FailureMessage);

			// Verify that no pending migrations were applied (startup aborted before migration).
			await AssertOnlyFirstMigrationAppliedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when backup creation fails due to a transient infrastructure error (e.g., database
	/// connection timeout), the failure is classified as <see cref="DatabaseFailureCategory.Transient"/>
	/// so the recovery service can retry automatically.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Distinction from <see cref="StartAsync_WhenBackupCreationFails_FailsWithManualInterventionAndSkipsMigration"/>:</b>
	///     That test uses an <see cref="IOException"/> (local disk failure → ManualInterventionRequired).
	///     This test uses a <see cref="TimeoutException"/> (transient infrastructure → Transient).
	///     </para>
	///     <para>
	///     <b>Key insight:</b> The database has NOT been modified when the backup fails — there is no
	///     inconsistent state. The failure category should therefore be determined by the error's nature
	///     (transient vs. persistent), not by a blanket ManualInterventionRequired.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenBackupCreationFailsWithTransientError_FailsWithTransient()
	{
		// Arrange — apply first migration, enable backup, inject transient failure via stage monitor.
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Inject a TimeoutException during backup creation to simulate a transient DB connection failure.
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt("CreateBackup.BeforeCreate", new TimeoutException("Simulated connection timeout"));

			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — transient, should retry
			AssertFailedStatusCore(harness.Status, DatabaseFailureCategory.Transient, 1, expectedShouldRetry: true);
			var ex = Assert.IsType<TimeoutException>(harness.Status.FailureException);
			Assert.Equal("Simulated connection timeout", ex.Message);
			Assert.Equal(
				"Failed to create backup before migration. Cannot safely proceed without a backup. " +
				"Check disk space, permissions, and backup directory configuration, then restart the application.",
				harness.Status.FailureMessage);

			// Verify that no pending migrations were applied.
			await AssertOnlyFirstMigrationAppliedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="OperationCanceledException"/> during backup creation propagates cleanly
	/// out of <see cref="DatabaseInitializer.StartAsync"/> without being misclassified as a backup creation
	/// failure (<see cref="DatabaseFailureCategory.ManualInterventionRequired"/>).
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Code path exercised:</b> The <c>catch (OperationCanceledException) { throw; }</c> clause inside
	///     the backup-creation try/catch of <c>HandleUpdateMigrationsAsync()</c>. Without this dedicated clause,
	///     cancellation would be caught by the <c>catch (Exception ex)</c> block and reported as a backup I/O
	///     failure — which would set <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> instead
	///     of letting the caller handle cancellation.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenOCEDuringBackupCreation_PropagatesOperationCanceledException()
	{
		// Arrange — apply first migration, enable backup, inject OCE during backup creation.
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt("CreateBackup.BeforeCreate", new OperationCanceledException());

			// Act + Assert — OCE propagates out of StartAsync().
			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				harness.Sut.StartAsync(CancellationToken.None));

			// Status was set to InProgress but never completed or failed.
			AssertInProgressStatus(harness.Status);

			// Verify that no pending migrations were applied.
			await AssertOnlyFirstMigrationAppliedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 4. Migration failure without backup: no safety net ---

	/// <summary>
	/// Verifies that when <c>MigrateAsync()</c> fails on an existing database and no backup was created
	/// (because <c>CreateBackupBeforeMigration = false</c>), automatic restore is impossible and the status
	/// transitions to <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied. A conflicting
	///     <c>Personas</c> table is manually created after the first migration, causing the pending migration's
	///     <c>CREATE TABLE Personas</c> to fail with a <see cref="DbException"/>.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, backup creation <b>disabled</b>. This means
	///     <c>backupPath</c> is <see langword="null"/> when the migration fails.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>else</c> branch of the migration-failure catch block in
	///     <c>HandleUpdateMigrationsAsync()</c>, where <c>RestoreOnFailure</c> is <see langword="false"/>
	///     or no backup exists — the database may be in an inconsistent state and requires manual repair.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. The <see cref="DbException"/>
	///     mentioning <c>Personas</c> is preserved. <c>ShouldRetry</c> is <see langword="false"/> because
	///     retrying would hit the same schema conflict.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenMigrationFailsWithoutBackup_FailsWithManualInterventionRequired()
	{
		// Arrange — apply first migration, then create a conflicting table to break the pending migration.
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = false;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Create a conflicting table to force the pending migration's CREATE TABLE to fail.
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.CreateConflictingTableAsync(dbContext, "Personas");
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Act — MigrateAsync() fails because Personas already exists; no backup to restore from.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsAssignableFrom<DbException>(harness.Status.FailureException);
			Assert.Contains("Personas", ex.Message);
			Assert.Equal(
				"Database migration failed. No backup was available for automatic restore. " +
				"Manual intervention may be required to fix or restore the database.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 5–6. Migration failure with backup: restore succeeds, error type determines category ---

	/// <summary>
	/// Verifies the full backup → migration-failure → automatic-restore → transient-retry flow: when a
	/// transient infrastructure error (recognized by
	/// <see cref="IDatabaseProviderOperations.IsServiceUnavailable"/>) causes <c>MigrateAsync()</c> to fail
	/// and a shuttle backup exists, the database is automatically restored to its pre-migration state and
	/// the status is <see cref="DatabaseFailureCategory.Transient"/> — allowing the application to retry
	/// on the next startup.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>RestoreOnFailure = true</c>. A <see cref="TimeoutException"/> is injected via
	///     <see cref="ExecutionStageMonitor"/> at <c>HandleUpdateMigrations.BeforeMigrate</c>, simulating a
	///     connection timeout during migration. <see cref="TimeoutException"/> is a provider-agnostic transient
	///     error that all <see cref="IDatabaseProviderOperations.IsServiceUnavailable"/> implementations recognize.
	///     </para>
	///     <para>
	///         <b>Code path exercised (integration):</b>
	///     </para>
	///     <list type="number">
	///         <item><c>CreateBackupAndReturnPathAsync()</c> — real shuttle backup creation</item>
	///         <item><c>MigrateAsync()</c> — fails with injected <see cref="TimeoutException"/></item>
	///         <item><c>RestoreFromShuttleBackupAsync()</c> — phases 1–2 (read backup, write checkpoint)</item>
	///         <item>
	///         <c>ResumeRestoreFromCheckpointAsync()</c> — phases 3–6 (schema drop, recreate, import, cleanup)
	///         </item>
	///         <item>
	///         <c>IsServiceUnavailable(ex)</c> returns <see langword="true"/> → transient classification
	///         </item>
	///     </list>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.Transient"/>. The original <see cref="TimeoutException"/> is
	///     preserved. <c>ShouldRetry</c> is <see langword="true"/>. The database is restored to
	///     first-migration-only state.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenTransientMigrationFailsWithBackup_RestoresAndRetriesAsTransient()
	{
		// Arrange — apply first migration, enable backup + restore, inject transient failure.
		using var backupDir = new TemporaryFolder("dbinit-transient");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Inject a TimeoutException — recognized as service-unavailable by all database providers.
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt(
					"HandleUpdateMigrations.BeforeMigrate",
					new TimeoutException("Simulated connection timeout during migration"));

			// Act — backup succeeds → migration "fails" (transient) → restore from backup → Transient.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — restore succeeded, classified as transient → automatic retry.
			AssertFailedStatusCore(harness.Status, DatabaseFailureCategory.Transient, 1, expectedShouldRetry: true);
			var ex = Assert.IsType<TimeoutException>(harness.Status.FailureException);
			Assert.Equal("Simulated connection timeout during migration", ex.Message);
			Assert.Equal(
				"Database migration failed due to a transient error but was automatically " +
				"restored from backup. Automatic retry will be attempted.",
				harness.Status.FailureMessage);

			// Verify that the database was restored to first-migration-only state.
			await AssertOnlyFirstMigrationAppliedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies the full backup → migration-failure → automatic-restore → manual-intervention flow: when a
	/// <b>non-transient</b> error causes <c>MigrateAsync()</c> to fail (not recognized by
	/// <see cref="IDatabaseProviderOperations.IsServiceUnavailable"/>), the database is restored from the
	/// shuttle backup but the status is <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> —
	/// the underlying issue (e.g., schema conflict, incompatible data state, or migration code bug) must be
	/// resolved before restarting.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>RestoreOnFailure = true</c>. An <see cref="InvalidOperationException"/> is injected via
	///     <see cref="ExecutionStageMonitor"/> at <c>HandleUpdateMigrations.BeforeMigrate</c>.
	///     <see cref="InvalidOperationException"/> is <b>not</b> recognized as a transient infrastructure error
	///     by any database provider.
	///     </para>
	///     <para>
	///         <b>Code path exercised (integration):</b>
	///     </para>
	///     <list type="number">
	///         <item><c>CreateBackupAndReturnPathAsync()</c> — real shuttle backup creation</item>
	///         <item><c>MigrateAsync()</c> — fails with injected <see cref="InvalidOperationException"/></item>
	///         <item><c>RestoreFromShuttleBackupAsync()</c> — phases 1–2 (read backup, write checkpoint)</item>
	///         <item>
	///         <c>ResumeRestoreFromCheckpointAsync()</c> — phases 3–6 (schema drop, recreate, import, cleanup)
	///         </item>
	///         <item>
	///         <c>IsServiceUnavailable(ex)</c> returns <see langword="false"/> → persistent classification
	///         </item>
	///     </list>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. The original
	///     <see cref="InvalidOperationException"/> is preserved. <c>ShouldRetry</c> is <see langword="false"/>.
	///     The database is restored to first-migration-only state.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenNonTransientMigrationFailsWithBackup_RestoresAndRequiresManualIntervention()
	{
		// Arrange — apply first migration, enable backup + restore, inject non-transient migration failure.
		using var backupDir = new TemporaryFolder("dbinit-restore");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Inject a non-transient failure (InvalidOperationException is not recognized as
			// service-unavailable by any provider) AFTER backup creation but BEFORE MigrateAsync().
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt(
					"HandleUpdateMigrations.BeforeMigrate",
					new InvalidOperationException("Simulated migration failure"));

			// Act — backup succeeds → migration "fails" → restore → ManualInterventionRequired.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — restore succeeded, but failure is non-transient → no automatic retry.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<InvalidOperationException>(harness.Status.FailureException);
			Assert.Equal("Simulated migration failure", ex.Message);
			Assert.Equal(
				"Database migration failed but was automatically restored from backup. " +
				"Review the migration error, resolve the underlying issue, and restart the application.",
				harness.Status.FailureMessage);

			// Verify that the database was restored to first-migration-only state.
			await AssertOnlyFirstMigrationAppliedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 7–8. Double failure: when even the restore can't save you ---

	/// <summary>
	/// Verifies the worst-case double-failure scenario: migration fails <b>and</b> the automatic restore
	/// from the shuttle backup also fails, leaving the database in an unknown state. Both exceptions are
	/// preserved in an <see cref="AggregateException"/> with the migration failure as the root cause.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>RestoreOnFailure = true</c>.
	///     </para>
	///     <para>
	///     <b>Fault injection strategy:</b> An <see cref="ExecutionStageMonitor.OnStage"/> callback at
	///     <c>HandleUpdateMigrations.BeforeMigrate</c> performs two actions:
	///     </para>
	///     <list type="number">
	///         <item>
	///         Corrupts the shuttle backup file (overwrites with null bytes) so
	///         <c>RestoreFromShuttleBackupAsync()</c> fails during the SQLite integrity check
	///         </item>
	///         <item>Throws an <see cref="InvalidOperationException"/> to simulate the migration failure</item>
	///     </list>
	///     <para>
	///     <b>Code path exercised:</b> The <c>catch (Exception restoreEx)</c> block in
	///     <c>HandleUpdateMigrationsAsync()</c> — reached when the restore attempt itself throws. Both the
	///     migration exception and the restore exception (<see cref="SqliteException"/> — "file is not a
	///     database") are wrapped in an <see cref="AggregateException"/>.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. The failure message includes the
	///     backup file path for manual recovery. <c>ShouldRetry</c> is <see langword="false"/>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenMigrationAndRestoreBothFail_FailsWithManualInterventionRequired()
	{
		// Arrange — apply first migration, enable backup + restore, inject faults at both stages.
		using var backupDir = new TemporaryFolder("dbinit-dblfail");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			// Apply only the first migration, leaving subsequent ones pending for StartAsync() to handle.
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Inject migration failure AND corrupt the backup file so restore also fails.
			// Strategy: let backup creation succeed, then truncate the file before migration is attempted.
			// The OnStage callback runs synchronously at the BeforeMigrate stage — after backup was created.
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.OnStage(
					"HandleUpdateMigrations.BeforeMigrate",
					() =>
					{
						// Corrupt the shuttle backup so RestoreFromShuttleBackupAsync() fails during integrity check.
						string[] backupFiles = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
						foreach (string file in backupFiles)
						{
							File.WriteAllBytes(file, "\0\0\0"u8);
						}

						// Now throw the "migration failure".
						throw new InvalidOperationException("Simulated migration failure");
					});

			// Act — backup succeeds → migration "fails" → restore fails (corrupt file) → ManualIntervention.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — both failed, status is ManualInterventionRequired.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<AggregateException>(harness.Status.FailureException);
			Assert.Equal(2, ex.InnerExceptions.Count);

			// InnerExceptions[0] = migration failure (root cause).
			var migrationEx = Assert.IsType<InvalidOperationException>(ex.InnerExceptions[0]);
			Assert.Equal("Simulated migration failure", migrationEx.Message);

			// InnerExceptions[1] = restore failure (corrupt backup file is not a valid SQLite database).
			// The Shuttle format is always SQLite regardless of the application's database provider.
			var restoreEx = Assert.IsType<SqliteException>(ex.InnerExceptions[1]);
			Assert.Contains("file is not a database", restoreEx.Message);
			Assert.Matches(
				@"^Database migration failed and automatic restore also failed\." +
				@" Manual intervention is required\. Backup file: .+\.shuttle\.sqlite$",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when a shuttle backup's <c>__EFMigrationsHistory</c> table is empty (no migration
	/// entries), the restore flow fails with <see cref="InvalidOperationException"/> because
	/// <c>RestoreFromShuttleBackupAsync()</c> cannot determine the baseline migration to restore to.
	/// This exercises the <c>baselineMigration</c> null/empty guard.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>RestoreOnFailure = true</c>.
	///     </para>
	///     <para>
	///         <b>Fault injection strategy (two stages):</b>
	///     </para>
	///     <list type="number">
	///         <item>
	///         An <see cref="ExecutionStageMonitor.OnStage"/> callback at
	///         <c>HandleUpdateMigrations.BeforeMigrate</c> opens the shuttle backup file via raw SQLite and
	///         deletes all rows from <c>__EFMigrationsHistory</c>, then throws an
	///         <see cref="InvalidOperationException"/> to trigger the migration-failure + restore path
	///         </item>
	///         <item>
	///         <c>RestoreFromShuttleBackupAsync()</c> reads the (now empty) migration history and throws
	///         <see cref="InvalidOperationException"/> ("does not contain migration history")
	///         </item>
	///     </list>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. The <see cref="AggregateException"/>
	///     contains both the injected migration failure (<c>InnerExceptions[0]</c>) and the restore failure
	///     (<c>InnerExceptions[1]</c>). <c>ShouldRetry</c> is <see langword="false"/>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenBackupHasNoMigrationHistory_FailsWithManualInterventionRequired()
	{
		// Arrange — apply first migration, enable backup + restore, strip migration history from backup.
		using var backupDir = new TemporaryFolder("dbinit-nohist");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			// Apply only the first migration, leaving subsequent ones pending for StartAsync() to handle.
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Two-stage fault injection:
			// 1. BeforeMigrate: strip migration history from the shuttle file, then throw (simulates migration failure)
			// 2. RestoreFromShuttleBackupAsync() reads the backup → finds no migration history → InvalidOperationException
			var ioe = new InvalidOperationException("Simulated migration failure");
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.OnStage(
					"HandleUpdateMigrations.BeforeMigrate",
					() =>
					{
						// Delete all rows from the migration history table in the shuttle backup.
						string[] backupFiles = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
						foreach (string file in backupFiles)
						{
							using var conn = new SqliteConnection($"Data Source={file}");
							conn.Open();
							using SqliteCommand cmd = conn.CreateCommand();
							cmd.CommandText = "DELETE FROM __EFMigrationsHistory";
							cmd.ExecuteNonQuery();
						}

						throw ioe;
					});

			// Act — backup succeeds → migration "fails" → restore reads empty history → InvalidOperationException
			// → caught by catch(Exception restoreEx) → ManualInterventionRequired.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — restore failed because of missing migration history.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<AggregateException>(harness.Status.FailureException);
			Assert.Equal(2, ex.InnerExceptions.Count);

			// InnerExceptions[0] = migration failure (root cause) — must be the exact instance we injected.
			var migrationEx = Assert.IsType<InvalidOperationException>(ex.InnerExceptions[0]);
			Assert.Same(ioe, migrationEx);
			Assert.Equal("Simulated migration failure", migrationEx.Message);

			// InnerExceptions[1] = restore failure (empty migration history in shuttle backup).
			var restoreEx = Assert.IsType<InvalidOperationException>(ex.InnerExceptions[1]);
			Assert.Equal(
				"Portable backup (LumaCore Shuttle format) does not contain migration history. " +
				"Automatic restore requires EF Core migration history.",
				restoreEx.Message);
			Assert.Matches(
				@"^Database migration failed and automatic restore also failed\." +
				@" Manual intervention is required\. Backup file: .+\.shuttle\.sqlite$",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when a shuttle backup's <see cref="SqliteShuttleSchema.ShuttleIdKey"/> metadata entry
	/// is missing, the restore flow fails with <see cref="InvalidOperationException"/> because
	/// <c>RestoreFromShuttleBackupAsync()</c> cannot establish a crash-safe checkpoint identity.
	/// This exercises the <c>shuttleId</c> null/empty guard.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>RestoreOnFailure = true</c>.
	///     </para>
	///     <para>
	///         <b>Fault injection strategy (two stages):</b>
	///     </para>
	///     <list type="number">
	///         <item>
	///         An <see cref="ExecutionStageMonitor.OnStage"/> callback at
	///         <c>HandleUpdateMigrations.BeforeMigrate</c> opens the shuttle backup file via raw SQLite and
	///         deletes the <see cref="SqliteShuttleSchema.ShuttleIdKey"/> row from the metadata table, then
	///         throws an <see cref="InvalidOperationException"/> to trigger the migration-failure + restore path
	///         </item>
	///         <item>
	///         <c>RestoreFromShuttleBackupAsync()</c> reads the (now identity-less) metadata and throws
	///         <see cref="InvalidOperationException"/> ("does not contain a ShuttleId")
	///         </item>
	///     </list>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. The <see cref="AggregateException"/>
	///     contains both the injected migration failure (<c>InnerExceptions[0]</c>) and the restore failure
	///     (<c>InnerExceptions[1]</c>). <c>ShouldRetry</c> is <see langword="false"/>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenBackupHasNoShuttleId_FailsWithManualInterventionRequired()
	{
		// Arrange — apply first migration, enable backup + restore, strip ShuttleId from backup.
		using var backupDir = new TemporaryFolder("dbinit-noid");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			// Apply only the first migration, leaving subsequent ones pending for StartAsync() to handle.
			await harness.MigrateToFirstMigrationOnlyAsync();

			var ioe = new InvalidOperationException("Simulated migration failure");
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.OnStage(
					"HandleUpdateMigrations.BeforeMigrate",
					() =>
					{
						// Delete the ShuttleId row from the shuttle metadata table.
						string[] backupFiles = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
						foreach (string file in backupFiles)
						{
							using var conn = new SqliteConnection($"Data Source={file}");
							conn.Open();
							using SqliteCommand cmd = conn.CreateCommand();
							cmd.CommandText = "DELETE FROM \"__Shuttle_BackupInfo\" WHERE \"key\" = 'ShuttleId'";
							cmd.ExecuteNonQuery();
						}

						throw ioe;
					});

			// Act — backup succeeds → migration "fails" → restore reads missing ShuttleId → InvalidOperationException
			// → caught by catch(Exception restoreEx) → ManualInterventionRequired.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — restore failed because of missing ShuttleId.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<AggregateException>(harness.Status.FailureException);
			Assert.Equal(2, ex.InnerExceptions.Count);

			// InnerExceptions[0] = migration failure (root cause) — must be the exact instance we injected.
			var migrationEx = Assert.IsType<InvalidOperationException>(ex.InnerExceptions[0]);
			Assert.Same(ioe, migrationEx);

			// InnerExceptions[1] = restore failure (missing ShuttleId in shuttle backup).
			var restoreEx = Assert.IsType<InvalidOperationException>(ex.InnerExceptions[1]);
			Assert.Equal(
				"Portable backup (LumaCore Shuttle format) does not contain a ShuttleId. " +
				"Only finalized shuttle files can be used for automatic restore.",
				restoreEx.Message);
			Assert.Matches(
				@"^Database migration failed and automatic restore also failed\." +
				@" Manual intervention is required\. Backup file: .+\.shuttle\.sqlite$",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 9. Cancellation: OCE must never be misclassified ---

	/// <summary>
	/// Verifies that <see cref="OperationCanceledException"/> during migration application propagates
	/// cleanly out of <see cref="DatabaseInitializer.StartAsync"/> without being wrapped in a
	/// <see cref="DatabaseInitializationException"/> or categorized as any failure type.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, backup creation <b>disabled</b>. An
	///     <see cref="OperationCanceledException"/> is injected via <see cref="ExecutionStageMonitor"/> at
	///     <c>HandleUpdateMigrations.BeforeMigrate</c>.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>catch (OperationCanceledException) { throw; }</c> block in
	///     <c>HandleUpdateMigrationsAsync()</c>. This dedicated catch clause exists to ensure cancellation is
	///     never misclassified as a migration failure (which would trigger restore or manual-intervention logic).
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> The <see cref="OperationCanceledException"/> propagates directly out of
	///     <c>StartAsync()</c>. The status remains at <see cref="DatabaseInitializationState.InProgress"/>
	///     because the initializer was interrupted before it could transition to <c>Completed</c> or
	///     <c>Failed</c>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenMigrationCancelledViaStageMonitor_PropagatesOperationCanceledException()
	{
		// Arrange — apply first migration, set up fault injection for the second.
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = false;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt("HandleUpdateMigrations.BeforeMigrate", new OperationCanceledException());

			// Act + Assert — OperationCanceledException propagates out of StartAsync().
			await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Sut.StartAsync(CancellationToken.None));

			// Status was set to InProgress but never completed or failed.
			AssertInProgressStatus(harness.Status);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion

	#region HandleMigrationsAsync() — Checkpoint detection

	// --- 10. Checkpoint detection at startup: resume, missing backup, invalid phase ---

	/// <summary>
	/// Verifies that when a restore checkpoint is detected at startup and the referenced shuttle backup
	/// file is valid, the full 6-phase restore completes successfully. Afterwards,
	/// <c>HandleMigrationsAsync()</c> throws <see cref="DatabaseInitializationException"/> with
	/// <see cref="DatabaseFailureCategory.Transient"/> so the current initialization exits cleanly.
	/// The <see cref="DatabaseConnectionMonitorService"/> will retry automatically — on the next attempt
	/// no checkpoint exists and the normal migration flow proceeds.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied). A real shuttle
	///     backup is created via <see cref="IDatabaseMaintenanceService.CreateShuttleBackupAsync"/>, then a
	///     restore checkpoint is manually written at the <c>schema_cleanup</c> phase (the earliest phase,
	///     ensuring all 6 restore phases execute).
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>checkpoint is not null</c> branch at the top of
	///     <c>HandleMigrationsAsync()</c>. Before inspecting any pending migrations, the method checks for
	///     an interrupted restore checkpoint via <c>TryReadRestoreCheckpointAsync()</c>. If found, it resumes
	///     the restore via <c>ResumeRestoreFromCheckpointAsync()</c> and throws
	///     <see cref="DatabaseInitializationException"/> with <see cref="DatabaseFailureCategory.Transient"/>.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.Transient"/>. The failure message confirms restore completion
	///     and indicates automatic retry. <c>ShouldRetry</c> is <see langword="true"/>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenCheckpointExistsWithValidBackup_CompletesRestoreAndFailsWithTransient()
	{
		// Arrange — fully initialize, create a real backup, then write a checkpoint.
		using var backupDir = new TemporaryFolder("dbinit-resume");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			// Create a real shuttle backup of the fully initialized database.
			(AsyncServiceScope backupScope, LumaCoreDbContext _) = harness.CreateScopedDbContext();
			string backupPath;
			try
			{
				var maintenanceService = backupScope.ServiceProvider
					.GetRequiredService<IDatabaseMaintenanceService>();
				backupPath = await maintenanceService.CreateShuttleBackupAsync(CancellationToken.None);
			}
			finally
			{
				await backupScope.DisposeAsync();
			}

			string shuttleId = await ReadShuttleIdAsync(backupPath);

			// Write a checkpoint at the schema_cleanup phase so all phases execute. BaselineMigrationId
			// must match the backup's migration level (full schema) because Phase 4 rebuilds the schema
			// via MigrateAsync(baseline) and the import verifies that migration histories match exactly.
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					shuttleId,
					SecondMigrationId,
					CancellationToken.None);
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Act — resume from checkpoint → full restore → Transient.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — restore completed, status is Transient ("Interrupted restore completed").
			AssertFailedStatusCore(harness.Status, DatabaseFailureCategory.Transient, 1, expectedShouldRetry: true);
			var ex = Assert.IsType<DatabaseInitializationException>(harness.Status.FailureException);
			Assert.Equal(
				"Interrupted restore completed successfully. The database has been restored to the pre-migration " +
				"state. Pending migrations will be applied automatically on the next recovery cycle.",
				ex.Message);
			Assert.Equal(ex.Message, harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when a restore checkpoint exists but no shuttle file with the checkpoint's
	/// <see cref="RestoreCheckpointData.ShuttleId"/> can be found in the backup directory,
	/// <c>HandleMigrationsAsync()</c> classifies the failure as
	/// <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> — a missing backup file cannot
	/// be resolved by a simple retry; an operator must restore the file or delete the checkpoint table.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied). A restore checkpoint
	///     is manually planted with a nonexistent <see cref="RestoreCheckpointData.ShuttleId"/>
	///     (<c>00000000-0000-0000-0000-000000000000</c>) that no shuttle file in the backup directory matches.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>checkpoint is not null</c> branch in
	///     <c>HandleMigrationsAsync()</c> detects the checkpoint and delegates to
	///     <c>ResumeRestoreFromCheckpointAsync()</c>. The <c>FindShuttleFileByIdAsync()</c> scan finds no
	///     matching file and throws <see cref="FileNotFoundException"/>. The try-catch around the resume call
	///     wraps this as <see cref="DatabaseInitializationException"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. The failure message instructs
	///     the operator to restore the backup file or delete the checkpoint table. <c>ShouldRetry</c> is
	///     <see langword="false"/> because retrying without operator action would produce the same result.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenRestoreCheckpointExistsWithMissingBackup_FailsWithManualInterventionRequired()
	{
		// Arrange — fully initialize, then plant a checkpoint with a nonexistent shuttle ID.
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			// Write a checkpoint referencing a nonexistent shuttle ID.
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					"00000000-0000-0000-0000-000000000000",
					FirstMigrationId, // Not consumed — the test fails before Phase 4 (migration) runs.
					CancellationToken.None);
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Act — StartAsync() detects checkpoint, tries to resume, fails because no matching file exists.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — ManualInterventionRequired because a missing backup cannot be resolved by retry.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<FileNotFoundException>(harness.Status.FailureException);
			Assert.Matches(
				@"^Cannot resume interrupted restore: .+" +
				@"shuttle file with ID '00000000-0000-0000-0000-000000000000'.+\.$",
				ex.Message);
			Assert.Equal(
				"Failed to resume interrupted restore. Either restore the backup file to the backup " +
				"directory or delete the '" + DatabaseInitializer.RestoreCheckpointTableName +
				"' table from the database to skip the restore and proceed with normal migration.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when a restore checkpoint contains an unrecognized phase value (e.g., a value from
	/// a newer application version), <c>HandleMigrationsAsync()</c> classifies the failure as
	/// <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> rather than
	/// <see cref="DatabaseFailureCategory.Transient"/> — an unrecognized phase cannot be resolved by
	/// a simple retry; an operator must deploy the correct application version or delete the checkpoint table.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied). A real shuttle
	///     backup is created (so the shuttle scan finds a match and the integrity check passes). A restore
	///     checkpoint is written and then its phase is updated to <c>"invalid_phase"</c> — a value that no
	///     restore logic recognizes.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The checkpoint detection branch in <c>HandleMigrationsAsync()</c>
	///     delegates to <c>ResumeRestoreFromCheckpointAsync()</c>, which validates the phase against known
	///     restore stages. The unrecognized value triggers the <c>!executeImport</c> guard, throwing
	///     <see cref="InvalidOperationException"/>. The try-catch around the resume call wraps this as
	///     <see cref="DatabaseInitializationException"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. <c>ShouldRetry</c> is
	///     <see langword="false"/>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenCheckpointHasUnrecognizedPhase_FailsWithManualInterventionRequired()
	{
		// Arrange — fully initialize, then plant a checkpoint with an invalid phase.
		using var backupDir = new TemporaryFolder("dbinit-phase");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			// Create a real shuttle backup so the shuttle scan finds it and integrity check passes.
			(AsyncServiceScope backupScope, LumaCoreDbContext _) = harness.CreateScopedDbContext();
			string backupPath;
			try
			{
				var maintenanceService = backupScope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceService>();
				backupPath = await maintenanceService.CreateShuttleBackupAsync(CancellationToken.None);
			}
			finally
			{
				await backupScope.DisposeAsync();
			}

			string shuttleId = await ReadShuttleIdAsync(backupPath);

			// Write checkpoint with an unrecognized phase.
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					shuttleId,
					FirstMigrationId, // Not consumed — the test fails before Phase 4 (migration) runs.
					CancellationToken.None);
				await harness.Sut.UpdateRestoreCheckpointPhaseAsync(dbContext, "invalid_phase", CancellationToken.None);
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Act — resume detects unrecognized phase → InvalidOperationException → ManualInterventionRequired.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — ManualInterventionRequired because an unrecognized phase cannot be resolved by retry.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<InvalidOperationException>(harness.Status.FailureException);
			Assert.Equal(
				"Unrecognized restore checkpoint phase: 'invalid_phase'. " +
				$"Expected '{RestoreCheckpointData.PhaseSchemaCleanup}', " +
				$"'{RestoreCheckpointData.PhaseMigration}', or '{RestoreCheckpointData.PhaseImport}'.",
				ex.Message);
			Assert.Equal(
				"Failed to resume interrupted restore. Either restore the backup file to the backup " +
				"directory or delete the '" + DatabaseInitializer.RestoreCheckpointTableName +
				"' table from the database to skip the restore and proceed with normal migration.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when a restore checkpoint exists but the referenced shuttle backup file is corrupt
	/// (overwritten with null bytes), <c>HandleMigrationsAsync()</c> classifies the failure as
	/// <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> — a corrupt backup cannot be
	/// resolved by a simple retry; an operator must restore a valid backup or delete the checkpoint table.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied). A real shuttle
	///     backup is created, its <see cref="SqliteShuttleSchema.ShuttleIdKey"/> is noted, and the file is
	///     then overwritten with null bytes to simulate post-crash corruption. A restore checkpoint is
	///     manually planted referencing the now-corrupt shuttle's ID.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>checkpoint is not null</c> branch in
	///     <c>HandleMigrationsAsync()</c> detects the checkpoint and delegates to
	///     <c>ResumeRestoreFromCheckpointAsync()</c>. The <c>FindShuttleFileByIdAsync()</c> scan opens
	///     the corrupt file, fails <c>InitializeAsync()</c> (not valid SQLite), skips it, and ultimately
	///     throws <see cref="FileNotFoundException"/>. The try-catch around the resume call wraps this as
	///     <see cref="DatabaseInitializationException"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. <c>ShouldRetry</c> is
	///     <see langword="false"/>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenCheckpointExistsWithCorruptBackup_FailsWithManualInterventionRequired()
	{
		// Arrange — fully initialize, create a real backup, corrupt it, then write a checkpoint.
		using var backupDir = new TemporaryFolder("dbinit-corrupt-resume");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			// Create a real shuttle backup and note its ShuttleId before corrupting the file.
			string backupPath = await harness.CreateShuttleBackupAsync();
			string shuttleId = await ReadShuttleIdAsync(backupPath);

			// Corrupt the shuttle file — overwrite with null bytes so it's no longer valid SQLite.
			await File.WriteAllBytesAsync(backupPath, "\0\0\0"u8.ToArray());

			// Write a checkpoint referencing the now-corrupt shuttle.
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					shuttleId,
					FirstMigrationId, // Not consumed — the test fails before Phase 4 (migration) runs.
					CancellationToken.None);
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Act — checkpoint detected → resume → FindShuttleFileByIdAsync skips corrupt file → FileNotFoundException.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — ManualInterventionRequired because the corrupt backup cannot be read.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<FileNotFoundException>(harness.Status.FailureException);
			Assert.Matches(
				@"^Cannot resume interrupted restore: no shuttle file with ID '.+' found " +
				@"in backup directory '.+'\. The backup file may have been moved or deleted\.$",
				ex.Message);
			Assert.Equal(
				"Failed to resume interrupted restore. Either restore the backup file to the backup " +
				"directory or delete the '" + DatabaseInitializer.RestoreCheckpointTableName +
				"' table from the database to skip the restore and proceed with normal migration.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="OperationCanceledException"/> during the <b>restore</b> flow (not during
	/// migration) propagates cleanly out of <see cref="DatabaseInitializer.StartAsync"/> without being
	/// misclassified as <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>RestoreOnFailure = true</c>.
	///     </para>
	///     <para>
	///         <b>Fault injection (two stages):</b>
	///     </para>
	///     <list type="number">
	///         <item>
	///         <c>HandleUpdateMigrations.BeforeMigrate</c>: throws <see cref="InvalidOperationException"/>
	///         to simulate a migration failure, which triggers the restore path
	///         </item>
	///         <item>
	///         <c>RestoreFromBackup.BeforePhase1</c>: throws <see cref="OperationCanceledException"/> to
	///         simulate application shutdown during the restore
	///         </item>
	///     </list>
	///     <para>
	///     <b>Code path exercised:</b> The <c>catch (OperationCanceledException) { throw; }</c> block inside
	///     the restore try/catch of <c>HandleUpdateMigrationsAsync()</c>. This dedicated clause prevents
	///     cancellation from being misclassified as a restore failure (which would wrap it in
	///     <see cref="AggregateException"/> and report <c>ManualInterventionRequired</c>).
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> The <see cref="OperationCanceledException"/> propagates directly out of
	///     <c>StartAsync()</c>. The database is in an unknown state, but the next startup will detect the
	///     restore checkpoint and resume.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenOCEDuringRestore_PropagatesOperationCanceledException()
	{
		// Arrange — apply first migration, enable backup + restore, inject dual faults.
		using var backupDir = new TemporaryFolder("dbinit-oce");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			// Apply only the first migration, leaving subsequent ones pending for StartAsync() to handle.
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Two-stage fault injection:
			// 1. BeforeMigrate: simulates migration failure → triggers restore
			// 2. BeforePhase1: simulates cancellation during restore → OCE propagates
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt(
					"HandleUpdateMigrations.BeforeMigrate",
					new InvalidOperationException("Simulated migration failure"))
				.ThrowAt(
					"RestoreFromBackup.BeforePhase1",
					new OperationCanceledException());

			// Act + Assert — OCE propagates out of StartAsync().
			await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Sut.StartAsync(CancellationToken.None));

			// Status was set to InProgress but never completed or failed — OCE bypasses both catch blocks.
			AssertInProgressStatus(harness.Status);

			// Backup was created before the migration failure, so the shuttle file exists.
			string[] backupFiles = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
			Assert.Single(backupFiles);

			// No checkpoint was written — OCE at BeforePhase1 aborted before Phase 2 (checkpoint write).
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				RestoreCheckpointData? checkpoint =
					await harness.Sut.TryReadRestoreCheckpointAsync(dbContext, CancellationToken.None);
				Assert.Null(checkpoint);
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Database remains at first-migration-only state — migration was never applied.
			await AssertOnlyFirstMigrationAppliedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="OperationCanceledException"/> during the <b>checkpoint-resume</b> path
	/// in <c>HandleMigrationsAsync()</c> propagates cleanly out of
	/// <see cref="DatabaseInitializer.StartAsync"/> without being misclassified as
	/// <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Distinction from <see cref="StartAsync_WhenOCEDuringRestore_PropagatesOperationCanceledException"/>:</b>
	///     That test exercises the <c>catch (OperationCanceledException) { throw; }</c> block inside the
	///     migration-failure restore path. This test exercises the
	///     <c>catch (Exception ex) when (ex is not OperationCanceledException)</c> filter around the
	///     <c>ResumeRestoreFromCheckpointAsync()</c> call at the top of <c>HandleMigrationsAsync()</c>.
	///     Both must propagate OCE cleanly, but they use different exception-filter mechanisms.
	///     </para>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied). A real shuttle
	///     backup is created and a checkpoint is written. The <see cref="IShuttleReaderFactory"/> is
	///     replaced with <see cref="OceThrowingShuttleReaderFactory"/> which throws
	///     <see cref="OperationCanceledException"/> when the resume path attempts to locate the shuttle
	///     file via <c>FindShuttleFileByIdAsync()</c>.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>catch (Exception ex) when (ex is not OperationCanceledException)</c>
	///     block in <c>HandleMigrationsAsync()</c> around the <c>ResumeRestoreFromCheckpointAsync()</c> call.
	///     The <c>when</c> filter evaluates to <see langword="false"/> for <see cref="OperationCanceledException"/>,
	///     so the exception bypasses the catch and propagates directly out of <c>StartAsync()</c>.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> The <see cref="OperationCanceledException"/> propagates directly out of
	///     <c>StartAsync()</c>. The status remains at <see cref="DatabaseInitializationState.InProgress"/>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenOCEDuringCheckpointResume_PropagatesOperationCanceledException()
	{
		// Arrange — fully initialize, create backup, write checkpoint, replace shuttle reader factory.
		using var backupDir = new TemporaryFolder("dbinit-oce-resume");
		TestHarness harness = CreateHarness(
			options =>
			{
				options.AutoMigration.BackupDirectory = backupDir.Path;
			},
			services =>
			{
				// Replace the shuttle reader factory with one that throws OCE.
				// No code path during the first StartAsync() or manual backup creation
				// uses IShuttleReaderFactory, so this only fires during the checkpoint resume
				// in the second StartAsync() call.
				services.AddSingleton<IShuttleReaderFactory>(new OceThrowingShuttleReaderFactory());
			});
		try
		{
			// Step 1: Initialize the database (IShuttleReaderFactory is NOT called during initialization).
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			// Step 2: Create a real shuttle backup (uses IDatabaseMaintenanceService, not IShuttleReaderFactory).
			string backupPath = await harness.CreateShuttleBackupAsync();
			string shuttleId = await ReadShuttleIdAsync(backupPath);

			// Step 3: Write a checkpoint so HandleMigrationsAsync() enters the checkpoint-resume path.
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					dbContext,
					shuttleId,
					FirstMigrationId, // Not consumed — the test throws before Phase 4 (migration) runs.
					CancellationToken.None);
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Act — second StartAsync detects the checkpoint → ResumeRestoreFromCheckpointAsync() →
			// FindShuttleFileByIdAsync() → IShuttleReaderFactory.Create() → OCE thrown →
			// propagates through "catch (Exception ex) when (ex is not OperationCanceledException)".
			await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Sut.StartAsync(CancellationToken.None));

			// Assert — OCE propagated cleanly; status remains InProgress.
			AssertInProgressStatus(harness.Status);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion

	#region HandleUpdateMigrationsAsync() — Edge cases

	// --- 11. Edge cases: retention policy, data fidelity, defense-in-depth ---

	/// <summary>
	/// Verifies that after a successful update migration with <c>CreateBackupBeforeMigration = true</c> but
	/// <c>BackupRetentionDays = 0</c>, the post-migration <see cref="DatabaseInitializer.CleanupOldBackupsAsync"/>
	/// is <b>not</b> invoked. Setting retention to zero effectively means "keep all backups indefinitely".
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied. A 30-day-old sentinel
	///     <c>.shuttle.sqlite</c> file exists in the backup directory.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>BackupRetentionDays = 0</c>. The retention-policy guard at the end of
	///     <c>HandleUpdateMigrationsAsync()</c> uses a pattern match requiring <c>BackupRetentionDays: &gt; 0</c>,
	///     so zero disables the cleanup entirely.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> Initialization completes. Both the old sentinel file <b>and</b> the
	///     freshly created backup survive — proving that <c>CleanupOldBackupsAsync()</c> was not called
	///     (2 shuttle files total in the backup directory).
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenAutoMigrateSucceedsWithRetentionZero_SkipsCleanupAndCompletes()
	{
		// Arrange — apply first migration, enable backup, set retention to 0.
		// Place an old sentinel shuttle file that CleanupOldBackupsAsync() would delete if it ran.
		using var backupDir = new TemporaryFolder("dbinit-ret0");
		string sentinelFile = Path.Combine(backupDir.Path, "old-sentinel.shuttle.sqlite");

		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.BackupRetentionDays = 0;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await TestHarness.CreateMinimalShuttleFileAsync(
				sentinelFile,
				harness.TimeProvider.GetUtcNow().AddDays(-30));

			await harness.MigrateToFirstMigrationOnlyAsync();

			// Act — migration succeeds, cleanup is skipped because BackupRetentionDays == 0.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			await AssertCompletedAsync(harness);

			// Sentinel file survived — proves CleanupOldBackupsAsync() was not invoked.
			Assert.True(File.Exists(sentinelFile));

			// The fresh backup from this run also still exists (2 shuttle files total).
			string[] backupFiles = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
			Assert.Equal(2, backupFiles.Length);

			// Exactly one of the two files is the sentinel; the other is the fresh backup.
			Assert.Single(backupFiles, f => f == sentinelFile);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies end-to-end data fidelity through the backup → migration-failure → restore cycle: a
	/// <see cref="UserEntity"/> (with a linked <see cref="ParticipantEntity"/>) is created before the backup,
	/// the migration is forced to fail, the database is automatically restored from the shuttle backup, and
	/// all user data survives the full export → import round-trip intact.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied. A user with username
	///     <c>restore-test</c> and a linked participant with display name <c>Restore Test User</c> are
	///     inserted into the database before the backup is created. All optional properties are set to
	///     non-null values (<c>Email</c>, <c>LastLoginAtUtc</c>, <c>LastTokenRefreshAtUtc</c>,
	///     <c>AvatarUrl</c>) to ensure that a silent column drop during export/import is detected —
	///     asserting <see langword="null"/> on an unset property cannot distinguish "correctly restored
	///     null" from "column was never exported."
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>RestoreOnFailure = true</c>. An <see cref="InvalidOperationException"/> is injected at
	///     <c>HandleUpdateMigrations.BeforeMigrate</c> (after backup creation, before <c>MigrateAsync()</c>).
	///     </para>
	///     <para>
	///     This is the most comprehensive integration test for the restore pipeline, exercising:
	///     </para>
	///     <list type="bullet">
	///         <item>Shuttle backup creation with real relational data (FK between user and participant)</item>
	///         <item>
	///         Full 6-phase restore (<c>RestoreFromShuttleBackupAsync()</c> →
	///         <c>ResumeRestoreFromCheckpointAsync()</c>)
	///         </item>
	///         <item>
	///         Data import fidelity — all scalar properties, foreign keys, and navigation properties must
	///         survive the export → import cycle
	///         </item>
	///     </list>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> (non-transient
	///     error). The restored database contains the original user with all scalar properties intact:
	///     <c>Username</c>, <c>UsernameNormalized</c>, <c>PasswordHash</c>, <c>Email</c>,
	///     <c>LastLoginAtUtc</c>, <c>LastTokenRefreshAtUtc</c>, and the linked participant's
	///     <c>PublicId</c>, <c>DisplayName</c>, <c>CreatedAtUtc</c>, and <c>AvatarUrl</c>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenMigrationFailsWithBackup_RestoresUserDataIntact()
	{
		// Arrange — apply first migration, create a user, enable backup + restore, inject migration failure.
		using var backupDir = new TemporaryFolder("dbinit-data");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Insert a user (with linked participant) so the backup has real data.
			var participantPublicId = Guid.NewGuid();
			DateTime seedCreatedAtUtc = harness.TimeProvider.GetUtcNow().UtcDateTime;
			(AsyncServiceScope seedScope, LumaCoreDbContext seedCtx) = harness.CreateScopedDbContext();
			try
			{
				var participant = new ParticipantEntity
				{
					PublicId = participantPublicId,
					DisplayName = "Restore Test User",
					CreatedAtUtc = seedCreatedAtUtc,
					AvatarUrl = "/avatars/restore-test.png"
				};
				seedCtx.Participants.Add(participant);
				await seedCtx.SaveChangesAsync();

				seedCtx.Users.Add(
					new UserEntity
					{
						ParticipantId = participant.Id,
						Username = "restore-test",
						UsernameNormalized = "RESTORE-TEST",
						PasswordHash = "$2a$11$fakehashfortest",
						Email = "restore-test@example.com",
						LastLoginAtUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
						LastTokenRefreshAtUtc = new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc)
					});
				await seedCtx.SaveChangesAsync();
			}
			finally
			{
				await seedScope.DisposeAsync();
			}

			// Inject failure AFTER backup creation but BEFORE MigrateAsync().
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt(
					"HandleUpdateMigrations.BeforeMigrate",
					new InvalidOperationException("Simulated migration failure"));

			// Act — backup (incl. user data) → migration "fails" → restore from backup.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — restore succeeded, non-transient failure → ManualInterventionRequired.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<InvalidOperationException>(harness.Status.FailureException);
			Assert.Equal("Simulated migration failure", ex.Message);
			Assert.Equal(
				"Database migration failed but was automatically restored from backup. " +
				"Review the migration error, resolve the underlying issue, and restart the application.",
				harness.Status.FailureMessage);

			// Verify that the user data survived the restore.
			(AsyncServiceScope verifyScope, LumaCoreDbContext verifyCtx) = harness.CreateScopedDbContext();
			try
			{
				UserEntity? restoredUser = await verifyCtx.Users
					                           .Include(u => u.Participant)
					                           .FirstOrDefaultAsync(u => u.Username == "restore-test");

				Assert.NotNull(restoredUser);

				// User scalar properties.
				Assert.Equal("restore-test", restoredUser.Username);
				Assert.Equal("RESTORE-TEST", restoredUser.UsernameNormalized);
				Assert.Equal("$2a$11$fakehashfortest", restoredUser.PasswordHash);
				Assert.Equal("restore-test@example.com", restoredUser.Email);
				Assert.Equal(
					new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
					restoredUser.LastLoginAtUtc);
				Assert.Equal(
					new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc),
					restoredUser.LastTokenRefreshAtUtc);

				// Participant navigation + scalar properties.
				Assert.NotNull(restoredUser.Participant);
				Assert.Equal(participantPublicId, restoredUser.Participant.PublicId);
				Assert.Equal("Restore Test User", restoredUser.Participant.DisplayName);
				Assert.Equal(seedCreatedAtUtc, restoredUser.Participant.CreatedAtUtc);
				Assert.Equal("/avatars/restore-test.png", restoredUser.Participant.AvatarUrl);
			}
			finally
			{
				await verifyScope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies the defense-in-depth behavior when <c>RestoreOnFailure = true</c> but
	/// <c>CreateBackupBeforeMigration = false</c>: migration fails and automatic restore is impossible
	/// because no backup was created, resulting in <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Relationship to options validation:</b> Under normal operation, this configuration combination
	///     is rejected at startup by <see cref="DatabaseOptions.Validate"/> (the <see cref="IValidatableObject"/>
	///     implementation). This test verifies that <c>HandleUpdateMigrationsAsync()</c> handles the situation
	///     gracefully even when validation is bypassed — e.g., via direct <see cref="DatabaseOptions"/> construction
	///     in tests, custom DI registrations, or future code paths that skip the options pipeline.
	///     </para>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied. A conflicting
	///     <c>Personas</c> table is manually created to force the pending migration's
	///     <c>CREATE TABLE Personas</c> to fail.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = false</c>,
	///     <c>RestoreOnFailure = true</c>. Because backup creation is disabled, <c>backupPath</c> remains
	///     <see langword="null"/> throughout the method. The compound condition
	///     <c>RestoreOnFailure &amp;&amp; !string.IsNullOrWhiteSpace(backupPath)</c> evaluates to
	///     <see langword="false"/>, falling into the <c>else</c> branch.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Failed"/> with
	///     <see cref="DatabaseFailureCategory.ManualInterventionRequired"/>. <c>ShouldRetry</c> is
	///     <see langword="false"/>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenMigrationFailsWithRestoreEnabledButNoBackup_FailsWithManualIntervention()
	{
		// Arrange — apply first migration, enable restore but NOT backup creation,
		// then create a conflicting table to break the pending migration.
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = false;
			options.AutoMigration.RestoreOnFailure = true;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				await harness.CreateConflictingTableAsync(dbContext, "Personas");
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Act — migration fails, RestoreOnFailure is true but backupPath is null
			// because CreateBackupBeforeMigration is false → else branch → ManualIntervention.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<DbException>(harness.Status.FailureException, exactMatch: false);
			Assert.Contains("Personas", ex.Message);
			Assert.Equal(
				"Database migration failed. No backup was available for automatic restore. " +
				"Manual intervention may be required to fix or restore the database.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that two shuttle backups created at the same <see cref="FakeTimeProvider"/> time produce
	/// distinct file paths (no collision). This pins the production fix that added milliseconds and a
	/// random suffix to the backup filename format.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied).
	///     </para>
	///     <para>
	///     <b>Setup:</b> The harness uses a <see cref="FakeTimeProvider"/> that is <b>not</b> advanced
	///     between the two backup calls, so both backups share the same timestamp component. The GUID
	///     suffix in the filename format must prevent collisions.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> Both calls succeed, return different paths, and both files exist on disk.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task CreateShuttleBackupAsync_WhenCalledTwiceAtSameTime_ProducesDistinctFiles()
	{
		// Arrange — fully initialize, then create two backups without advancing time.
		using var backupDir = new TemporaryFolder("dbinit-uniqueness");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			// Act — create two backups at the exact same FakeTimeProvider time.
			string path1 = await harness.CreateShuttleBackupAsync();
			string path2 = await harness.CreateShuttleBackupAsync();

			// Assert — paths are different and both files exist.
			Assert.NotEqual(path1, path2);
			Assert.True(File.Exists(path1), $"First backup file should exist: {path1}");
			Assert.True(File.Exists(path2), $"Second backup file should exist: {path2}");

			// Both must be valid shuttle files.
			await AssertShuttleBackupIntegrityAsync(path1, AllMigrationIds);
			await AssertShuttleBackupIntegrityAsync(path2, AllMigrationIds);

			// ShuttleIds must differ (each export generates a unique GUID).
			string id1 = await ReadShuttleIdAsync(path1);
			string id2 = await ReadShuttleIdAsync(path2);
			Assert.NotEqual(id1, id2);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when a transient migration failure triggers backup → restore → retry, the second
	/// <see cref="DatabaseInitializer.StartAsync"/> call reuses the existing pre-migration backup instead
	/// of creating a new one. This exercises the
	/// <c>mLastBackupPath is not null &amp;&amp; File.Exists(mLastBackupPath)</c> branch in
	/// <c>HandleUpdateMigrationsAsync()</c>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied; subsequent migrations are pending.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>RestoreOnFailure = true</c>, <c>BackupRetentionDays = 0</c> (cleanup disabled for
	///     deterministic file count assertions).
	///     </para>
	///     <para>
	///         <b>Test sequence:</b>
	///     </para>
	///     <list type="number">
	///         <item>
	///         First <see cref="DatabaseInitializer.StartAsync"/>: creates a shuttle backup, injects a
	///         <see cref="TimeoutException"/> at <c>HandleUpdateMigrations.BeforeMigrate</c>, restores
	///         the database, fails with <see cref="DatabaseFailureCategory.Transient"/>. One shuttle file
	///         exists in the backup directory.
	///         </item>
	///         <item>
	///         Second <see cref="DatabaseInitializer.StartAsync"/>: detects pending migrations, finds
	///         <c>mLastBackupPath</c> still set and the file still on disk → reuses the existing backup
	///         instead of creating a new one. Migration succeeds normally.
	///         </item>
	///     </list>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Completed"/>. The backup
	///     directory still contains exactly one shuttle file — proving the backup was reused, not recreated.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenRetryAfterTransientMigrationFailure_ReusesExistingBackup()
	{
		// Arrange — apply first migration, enable backup + restore, inject transient failure.
		using var backupDir = new TemporaryFolder("dbinit-reuse");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupRetentionDays = 0;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			// First attempt — transient migration failure → backup created → restore → Transient.
			using (ExecutionStageMonitor.Configure()
				       .ThrowAt(
					       "HandleUpdateMigrations.BeforeMigrate",
					       new TimeoutException("Simulated connection timeout")))
			{
				await harness.Sut.StartAsync(CancellationToken.None);
			}

			Assert.Equal(DatabaseInitializationState.Failed, harness.Status.State);
			Assert.Equal(DatabaseFailureCategory.Transient, harness.Status.FailureCategory);

			// Exactly one shuttle file from the first attempt.
			string[] backupFilesAfterFirstAttempt = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
			Assert.Single(backupFilesAfterFirstAttempt);

			// Act — second attempt without fault → reuses existing backup, migration succeeds.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — migration succeeded on retry.
			await AssertCompletedAsync(harness);

			// Still exactly one shuttle file — the existing backup was reused, not a new one created.
			string[] backupFilesAfterRetry = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
			Assert.Single(backupFilesAfterRetry);
			Assert.Equal(backupFilesAfterFirstAttempt[0], backupFilesAfterRetry[0]);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that when the pre-migration backup file is deleted between retries (e.g., by operator
	/// cleanup), the stale <c>mLastBackupPath</c> reference is cleared and a fresh backup is created
	/// on the next attempt. This exercises the <c>mLastBackupPath = null</c> branch when
	/// <c>File.Exists(mLastBackupPath)</c> returns <see langword="false"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Same as
	///     <see cref="StartAsync_WhenRetryAfterTransientMigrationFailure_ReusesExistingBackup"/> — database
	///     has only the first migration, transient migration failure triggers backup → restore.
	///     </para>
	///     <para>
	///     <b>Key difference:</b> Between the two <see cref="DatabaseInitializer.StartAsync"/> calls, the
	///     backup file created in the first attempt is manually deleted. This simulates an operator cleaning
	///     up disk space between recovery cycles.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>else</c> branch of the backup-reuse check in
	///     <c>HandleUpdateMigrationsAsync()</c>: <c>mLastBackupPath is not null</c> but
	///     <c>File.Exists(mLastBackupPath)</c> returns <see langword="false"/> → <c>mLastBackupPath = null</c>
	///     → <c>CreateBackupAndReturnPathAsync()</c> creates a fresh backup.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseInitializationState.Completed"/>. The backup
	///     directory contains exactly one shuttle file — the fresh backup, not the deleted one.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenBackupDeletedBetweenRetries_CreatesFreshBackup()
	{
		// Arrange — same setup as the backup-reuse test.
		using var backupDir = new TemporaryFolder("dbinit-stale");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupRetentionDays = 0;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			// First attempt — transient migration failure → backup created → restore → Transient.
			using (ExecutionStageMonitor.Configure()
				       .ThrowAt(
					       "HandleUpdateMigrations.BeforeMigrate",
					       new TimeoutException("Simulated connection timeout")))
			{
				await harness.Sut.StartAsync(CancellationToken.None);
			}

			Assert.Equal(DatabaseFailureCategory.Transient, harness.Status.FailureCategory);

			// Simulate operator cleanup: delete the backup file between retries.
			string[] originalBackupFiles = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
			Assert.Single(originalBackupFiles);
			File.Delete(originalBackupFiles[0]);
			Assert.Empty(Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite"));

			// Act — second attempt: mLastBackupPath still set but File.Exists returns false →
			// stale reference cleared → fresh backup created → migration succeeds.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — migration succeeded.
			await AssertCompletedAsync(harness);

			// A new backup file was created (the stale reference was cleared).
			string[] newBackupFiles = Directory.GetFiles(backupDir.Path, "*.shuttle.sqlite");
			Assert.Single(newBackupFiles);
			Assert.NotEqual(originalBackupFiles[0], newBackupFiles[0]);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion
}
