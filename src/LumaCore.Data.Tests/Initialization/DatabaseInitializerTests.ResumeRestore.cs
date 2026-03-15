// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Core.IO;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Import.Implementations;
using LumaCore.Data.Entities;
using LumaCore.Data.Initialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

// ReSharper disable AccessToDisposedClosure

namespace LumaCore.Data.Tests.Initialization;

// ResumeRestoreFromCheckpointAsync(): the 6-phase restore pipeline exercised in isolation.
//
// This file covers each phase entry point independently of the migration trigger that
// normally kicks it off (see HandleUpdateMigrations for that). StartAsync covers the
// lifecycle basics. The anchor file (DatabaseInitializerTests.cs) has the full reading order.
//
//   1. Phase entry points: SchemaCleanup (all phases), Migration (skip Phase 3),
//      Import (skip Phases 3–4) — verifies that each resume point correctly skips
//      earlier phases (ExecutesAllPhases, SkipsSchemaCleanup, SkipsSchemaPhases).
//
//   2. Error handling: missing backup file → FileNotFoundException,
//      missing backup directory → FileNotFoundException,
//      unrecognized phase → InvalidOperationException.
//
//   3. Volume / data fidelity: chunked import spanning multiple batches preserves
//      all rows across the chunk boundary (PreservesAllRows).
public sealed partial class DatabaseInitializerTests
{
	#region ResumeRestoreFromCheckpointAsync()

	// --- 1. Phase entry points: each resume point skips earlier phases ---

	/// <summary>
	/// Verifies the full restore resume flow from the
	/// <see cref="RestoreCheckpointData.PhaseSchemaCleanup"/> phase: all tables are dropped (Phase 3),
	/// migrations are re-applied to the baseline (Phase 4), the import runs (Phase 5), and the checkpoint
	/// table is dropped (Phase 6). Uses a real Shuttle backup so the full pipeline (export → integrity →
	/// import) is exercised.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied. A real shuttle backup is
	///     created at that state, then the database is fully initialized (all remaining migrations applied)
	///     so the schema differs from the backup. A checkpoint is written at the
	///     <see cref="RestoreCheckpointData.PhaseSchemaCleanup"/> phase.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> <c>BackupDirectory</c> set to a temporary directory. No auto-migration
	///     flags needed because <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/> is called
	///     directly.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> All four resume phases in
	///     <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/>:
	///     </para>
	///     <list type="number">
	///         <item>Phase 3 — schema cleanup (drop all tables except checkpoint)</item>
	///         <item>Phase 4 — re-apply migrations to the baseline (<c>InitialCreate</c>)</item>
	///         <item>Phase 5 — import data from shuttle backup</item>
	///         <item>Phase 6 — drop checkpoint table</item>
	///     </list>
	///     <para>
	///     <b>Expected outcome:</b> Checkpoint table is dropped (Phase 6 completed). The database schema
	///     is rebuilt to the first migration only. Entity tables are queryable.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task ResumeRestoreFromCheckpointAsync_WhenPhaseIsSchemaCleanup_ExecutesAllPhasesAndDropsCheckpoint()
	{
		// Arrange
		using var backupDir = new TemporaryFolder("dbinit-resume-sc");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			// Step 1+2: Apply the first migration only and create a backup at that state.
			await harness.MigrateToFirstMigrationOnlyAsync();
			string backupPath = await harness.CreateShuttleBackupAsync();
			string shuttleId = await ReadShuttleIdAsync(backupPath);

			// Advance time so the automatic pre-migration backup in StartAsync() gets a different
			// filename than our manual backup (both derive from FakeTimeProvider's timestamp).
			harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));

			// Step 3: Fully initialize (applies all remaining migrations) so the DB schema
			// diverges from the backup — Phase 3 must drop this schema.
			await harness.Sut.StartAsync(CancellationToken.None);
			await AssertCompletedAsync(harness);

			// Step 4: Write checkpoint table into the DB. ResumeRestoreFromCheckpointAsync() needs
			// this table to exist because it updates the phase column as it progresses through
			// phases, and drops the table at the end (Phase 6). The initial phase written to the
			// DB is always schema_cleanup (hardcoded in WriteCheckpointAsync()).
			(AsyncServiceScope writeScope, LumaCoreDbContext writeCtx) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					writeCtx,
					shuttleId,
					FirstMigrationId,
					CancellationToken.None);
			}
			finally
			{
				await writeScope.DisposeAsync();
			}

			// Step 5: Build the in-memory checkpoint that controls which phase the resume starts
			// from. In production, this object is read from the DB via TryReadRestoreCheckpointAsync().
			// Here we construct it manually to test the PhaseSchemaCleanup entry point.
			var checkpoint = new RestoreCheckpointData(
				ShuttleId: shuttleId,
				BaselineMigrationId: FirstMigrationId,
				Phase: RestoreCheckpointData.PhaseSchemaCleanup,
				StartedUtc: "2026-01-01T00:00:00.0000000Z");

			// Act — resumes from Phase 3: drop schema → migrate to baseline → import → drop checkpoint.
			await harness.Sut.ResumeRestoreFromCheckpointAsync(harness.Options, checkpoint, CancellationToken.None);

			// Assert — checkpoint table has been dropped (Phase 6 completed).
			(AsyncServiceScope readScope, LumaCoreDbContext readCtx) = harness.CreateScopedDbContext();
			try
			{
				RestoreCheckpointData? result = await harness.Sut
					                                .TryReadRestoreCheckpointAsync(readCtx, CancellationToken.None);
				Assert.Null(result);

				// Smoke-test: Phase 4 rebuilt the schema to the first migration. If the Roles table
				// didn't exist, this query would throw a DbException ("no such table").
				await readCtx.Roles.CountAsync(CancellationToken.None);

				// Only the first migration should be applied after restore.
				List<string> applied = [..await readCtx.Database.GetAppliedMigrationsAsync()];
				Assert.Single(applied);
				Assert.Equal(FirstMigrationId, applied[0]);
			}
			finally
			{
				await readScope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that resuming from the <see cref="RestoreCheckpointData.PhaseMigration"/> phase skips
	/// schema cleanup (Phase 3), re-applies migrations to the baseline (Phase 4), runs the import
	/// (Phase 5), and drops the checkpoint table (Phase 6). This exercises the middle phase where
	/// <c>executeSchemaCleanup = false</c> and <c>executeMigration = true</c>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied. A real shuttle backup is
	///     created at that state, then the database is fully initialized (all remaining migrations applied).
	///     A checkpoint is written, and all schema objects are manually dropped (preserving the checkpoint
	///     table) to simulate that Phase 3 already completed in a previous interrupted attempt.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> <c>BackupDirectory</c> set to a temporary directory.
	///     <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/> is called directly.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <see cref="RestoreCheckpointData.PhaseMigration"/> branch in
	///     <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/>: Phase 3 is skipped,
	///     Phases 4–6 execute (migrate → import → drop checkpoint).
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> Checkpoint table is dropped. The database schema is rebuilt to the
	///     first migration only. Entity tables are queryable.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task ResumeRestoreFromCheckpointAsync_WhenPhaseIsMigration_SkipsSchemaCleanupAndDropsCheckpoint()
	{
		// Arrange
		using var backupDir = new TemporaryFolder("dbinit-resume-mig");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			// Step 1+2: Apply the first migration only and create a backup at that state.
			await harness.MigrateToFirstMigrationOnlyAsync();
			string backupPath = await harness.CreateShuttleBackupAsync();
			string shuttleId = await ReadShuttleIdAsync(backupPath);

			// Advance time so the automatic pre-migration backup in StartAsync() gets a different
			// filename than our manual backup (both derive from FakeTimeProvider's timestamp).
			harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));

			// Step 3: Fully initialize so we can write a checkpoint.
			await harness.Sut.StartAsync(CancellationToken.None);
			await AssertCompletedAsync(harness);

			// Step 4: Write checkpoint table into the DB (always starts at schema_cleanup phase).
			// The table must exist because ResumeRestoreFromCheckpointAsync() updates its phase
			// column as it progresses and drops it at the end.
			(AsyncServiceScope writeScope, LumaCoreDbContext writeCtx) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					writeCtx,
					shuttleId,
					FirstMigrationId,
					CancellationToken.None);
			}
			finally
			{
				await writeScope.DisposeAsync();
			}

			// Step 5: Simulate that Phase 3 already completed by dropping all schema objects
			// (preserving the checkpoint table). This leaves the DB in the state that Phase 4 expects:
			// empty schema, checkpoint still present.
			(AsyncServiceScope dropScope, LumaCoreDbContext dropCtx) = harness.CreateScopedDbContext();
			try
			{
				var preserveTables = new HashSet<string>(StringComparer.Ordinal)
				{
					DatabaseInitializer.RestoreCheckpointTableName
				};
				await harness.ProviderOperations.DropSchemaObjectsAsync(
					dropCtx,
					preserveTables,
					CancellationToken.None);
			}
			finally
			{
				await dropScope.DisposeAsync();
			}

			// Step 6: Build the in-memory checkpoint at PhaseMigration. In production, this
			// object comes from TryReadRestoreCheckpointAsync() and would reflect the phase that
			// was last written to the DB. Here we set it to PhaseMigration to simulate that
			// Phase 3 completed (and updated the DB row) before the process was interrupted.
			var checkpoint = new RestoreCheckpointData(
				ShuttleId: shuttleId,
				BaselineMigrationId: FirstMigrationId,
				Phase: RestoreCheckpointData.PhaseMigration,
				StartedUtc: "2026-01-01T00:00:00.0000000Z");

			// Act — resumes from Phase 4: migrate to baseline → import → drop checkpoint.
			await harness.Sut.ResumeRestoreFromCheckpointAsync(harness.Options, checkpoint, CancellationToken.None);

			// Assert — checkpoint table has been dropped (Phase 6 completed).
			(AsyncServiceScope readScope, LumaCoreDbContext readCtx) = harness.CreateScopedDbContext();
			try
			{
				RestoreCheckpointData? result = await harness.Sut
					                                .TryReadRestoreCheckpointAsync(readCtx, CancellationToken.None);
				Assert.Null(result);

				// Smoke-test: Phase 4 rebuilt the schema to the first migration. If the Roles table
				// didn't exist, this query would throw a DbException ("no such table").
				await readCtx.Roles.CountAsync(CancellationToken.None);

				// Only the first migration should be applied after restore.
				List<string> applied = [..await readCtx.Database.GetAppliedMigrationsAsync()];
				Assert.Single(applied);
				Assert.Equal(FirstMigrationId, applied[0]);
			}
			finally
			{
				await readScope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that resuming from the <see cref="RestoreCheckpointData.PhaseImport"/> phase skips schema
	/// cleanup (Phase 3) and migration (Phase 4), runs the import (Phase 5), and drops the checkpoint
	/// table (Phase 6). The original schema (all migrations applied) remains intact because the backup
	/// matches.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied). A real shuttle
	///     backup is created at the full-schema state. A checkpoint is written at the
	///     <see cref="RestoreCheckpointData.PhaseImport"/> phase, simulating that Phases 3–4 already
	///     completed.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> <c>BackupDirectory</c> set to a temporary directory.
	///     <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/> is called directly.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <see cref="RestoreCheckpointData.PhaseImport"/> branch in
	///     <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/>:
	///     <c>executeSchemaCleanup = false</c>, <c>executeMigration = false</c>,
	///     <c>executeImport = true</c> — only Phases 5–6 execute.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> Checkpoint table is dropped. All migrations remain applied (Phase 4
	///     was skipped). Entity tables are queryable.
	///     </para>
	///     <para>
	///     <b>Known limitation:</b> The backup contains no user data (created from a freshly initialized
	///     DB), so Phase 5 imports an empty dataset. This test does not prove that the import
	///     <em>actually ran</em> — only that Phase 5 was <em>reached</em> without error. Data fidelity
	///     through the import pipeline is covered by
	///     <see cref="StartAsync_WhenRestoreSpansMultipleChunks_PreservesAllRows"/>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task ResumeRestoreFromCheckpointAsync_WhenPhaseIsImport_SkipsSchemaPhasesAndDropsCheckpoint()
	{
		// Arrange
		using var backupDir = new TemporaryFolder("dbinit-resume-imp");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			// Step 1: Fully initialize the DB.
			await harness.Sut.StartAsync(CancellationToken.None);
			await AssertCompletedAsync(harness);

			// Step 2: Create a backup at the full-schema state.
			string backupPath = await harness.CreateShuttleBackupAsync();
			string shuttleId = await ReadShuttleIdAsync(backupPath);

			// Step 3: Write checkpoint table into the DB (always starts at schema_cleanup phase).
			// The table must exist for phase-update bookkeeping and the final drop in Phase 6.
			(AsyncServiceScope writeScope, LumaCoreDbContext writeCtx) = harness.CreateScopedDbContext();
			try
			{
				await harness.Sut.WriteRestoreCheckpointAsync(
					writeCtx,
					shuttleId,
					FirstMigrationId, // Not consumed — Phase 4 (migration) is skipped for PhaseImport.
					CancellationToken.None);
			}
			finally
			{
				await writeScope.DisposeAsync();
			}

			// Step 4: Build the in-memory checkpoint at PhaseImport. This simulates that
			// Phases 3–4 completed (and updated the DB row to "import") before interruption.
			var checkpoint = new RestoreCheckpointData(
				ShuttleId: shuttleId,
				BaselineMigrationId: FirstMigrationId, // Not consumed — Phase 4 (migration) is skipped for PhaseImport.
				Phase: RestoreCheckpointData.PhaseImport,
				StartedUtc: "2026-01-01T00:00:00.0000000Z");

			// Act — resumes from Phase 5: import data → drop checkpoint.
			await harness.Sut.ResumeRestoreFromCheckpointAsync(harness.Options, checkpoint, CancellationToken.None);

			// Assert — checkpoint dropped, original schema intact.
			(AsyncServiceScope readScope, LumaCoreDbContext readCtx) = harness.CreateScopedDbContext();
			try
			{
				RestoreCheckpointData? result = await harness.Sut
					                                .TryReadRestoreCheckpointAsync(readCtx, CancellationToken.None);
				Assert.Null(result);

				// Smoke-test: schema survived intact (Phases 3–4 were skipped). If the Roles table
				// didn't exist, this query would throw a DbException ("no such table").
				await readCtx.Roles.CountAsync(CancellationToken.None);

				// All migrations still applied (Phase 4 was skipped).
				List<string> applied = [..await readCtx.Database.GetAppliedMigrationsAsync()];
				Assert.Equal(AllMigrationIds.Length, applied.Count);
			}
			finally
			{
				await readScope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Error handling: missing backup, unrecognized phase ---

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/> throws
	/// <see cref="FileNotFoundException"/> when the backup file referenced by the checkpoint no longer
	/// exists on disk.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied). A checkpoint is
	///     created with a nonexistent <see cref="RestoreCheckpointData.ShuttleId"/>
	///     (<c>00000000-0000-0000-0000-000000000000</c>) that no shuttle file matches.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Default options (no explicit backup directory). No shuttle files exist.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>FindShuttleFileByIdAsync()</c> scan in
	///     <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/> finds no matching shuttle
	///     file and throws <see cref="FileNotFoundException"/> before any restore phase executes.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="FileNotFoundException"/> whose message contains the
	///     nonexistent shuttle ID.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task ResumeRestoreFromCheckpointAsync_WhenBackupFileMissing_ThrowsFileNotFoundException()
	{
		// Arrange — checkpoint references a non-existent backup file.
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			await AssertCompletedAsync(harness);

			var checkpoint = new RestoreCheckpointData(
				ShuttleId: "00000000-0000-0000-0000-000000000000",
				BaselineMigrationId: FirstMigrationId, // Not consumed — the test fails before Phase 4 (migration) runs.
				Phase: RestoreCheckpointData.PhaseSchemaCleanup,
				StartedUtc: "2026-01-01T00:00:00.0000000Z");

			// Act + Assert
			var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
				         harness.Sut.ResumeRestoreFromCheckpointAsync(
					         harness.Options,
					         checkpoint,
					         CancellationToken.None));

			Assert.Matches(
				@"^Cannot resume interrupted restore: .*'00000000-0000-0000-0000-000000000000'.*\.$",
				ex.Message);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/> throws
	/// <see cref="FileNotFoundException"/> when the configured backup directory does not exist on disk.
	/// This is a separate error path from "shuttle file not found in existing directory" — the
	/// <c>!Directory.Exists()</c> guard in <c>FindShuttleFileByIdAsync()</c> fires before any file scan.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied). The backup
	///     directory is set to a non-existent path.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> <c>FindShuttleFileByIdAsync()</c> checks
	///     <c>Directory.Exists(backupDirectory)</c> → <see langword="false"/> → throws
	///     <see cref="FileNotFoundException"/> with a message containing "backup directory" and
	///     "does not exist".
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="FileNotFoundException"/> whose message mentions the
	///     missing directory.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task ResumeRestoreFromCheckpointAsync_WhenBackupDirectoryDoesNotExist_ThrowsFileNotFoundException()
	{
		// Arrange — use a non-existent directory as the backup path.
		string nonExistentDir = Path.Combine(Path.GetTempPath(), $"dbinit-missing-{Guid.NewGuid():N}");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupDirectory = nonExistentDir;
		});
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			await AssertCompletedAsync(harness);

			var checkpoint = new RestoreCheckpointData(
				ShuttleId: "00000000-0000-0000-0000-000000000000",
				BaselineMigrationId: FirstMigrationId, // Not consumed — the test fails before Phase 4 (migration) runs.
				Phase: RestoreCheckpointData.PhaseSchemaCleanup,
				StartedUtc: "2026-01-01T00:00:00.0000000Z");
			Assert.False(Directory.Exists(nonExistentDir));

			// Act + Assert
			var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
				         harness.Sut.ResumeRestoreFromCheckpointAsync(
					         harness.Options,
					         checkpoint,
					         CancellationToken.None));

			Assert.Matches(
				@"^Cannot resume interrupted restore: backup directory '.+' does not exist\. " +
				@"No shuttle file with ID '.+' can be located\.$",
				ex.Message);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/> throws
	/// <see cref="InvalidOperationException"/> when the checkpoint phase is not one of the recognized
	/// values (<see cref="RestoreCheckpointData.PhaseSchemaCleanup"/>,
	/// <see cref="RestoreCheckpointData.PhaseMigration"/>, <see cref="RestoreCheckpointData.PhaseImport"/>).
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database is fully initialized (all migrations applied). A real shuttle
	///     backup is created so that <c>FindShuttleFileByIdAsync()</c> and the integrity check pass. A
	///     checkpoint is created with <c>Phase: "invalid_phase"</c> — a value that no restore logic
	///     recognizes.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> <c>BackupDirectory</c> set to a temporary directory.
	///     <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/> is called directly.
	///     </para>
	///     <para>
	///     <b>Code path exercised:</b> The <c>!executeImport</c> guard in
	///     <see cref="DatabaseInitializer.ResumeRestoreFromCheckpointAsync"/> — all three phase
	///     comparisons (<see cref="RestoreCheckpointData.PhaseSchemaCleanup"/>,
	///     <see cref="RestoreCheckpointData.PhaseMigration"/>,
	///     <see cref="RestoreCheckpointData.PhaseImport"/>) fail, triggering the
	///     <see cref="InvalidOperationException"/>.
	///     </para>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="InvalidOperationException"/> whose message contains
	///     <c>"Unrecognized"</c> and <c>"invalid_phase"</c>.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task ResumeRestoreFromCheckpointAsync_WhenPhaseUnrecognized_ThrowsInvalidOperationException()
	{
		// Arrange — create a real backup so File.Exists and integrity check pass.
		using var backupDir = new TemporaryFolder("dbinit-resume-phase");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			await AssertCompletedAsync(harness);

			string backupPath = await harness.CreateShuttleBackupAsync();
			string shuttleId = await ReadShuttleIdAsync(backupPath);

			var checkpoint = new RestoreCheckpointData(
				ShuttleId: shuttleId,
				BaselineMigrationId: FirstMigrationId, // Not consumed — the test fails before Phase 4 (migration) runs.
				Phase: "invalid_phase",
				StartedUtc: "2026-01-01T00:00:00.0000000Z");

			// Act + Assert
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
				         harness.Sut.ResumeRestoreFromCheckpointAsync(
					         harness.Options,
					         checkpoint,
					         CancellationToken.None));

			Assert.Equal(
				"Unrecognized restore checkpoint phase: 'invalid_phase'. " +
				$"Expected '{RestoreCheckpointData.PhaseSchemaCleanup}', " +
				$"'{RestoreCheckpointData.PhaseMigration}', or '{RestoreCheckpointData.PhaseImport}'.",
				ex.Message);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Volume / data fidelity: chunked import across batch boundaries ---

	/// <summary>
	/// Verifies that the chunked import pipeline correctly handles datasets that span multiple
	/// <see cref="DataPortTuning.ImportChunkSizeRows"/> boundaries: each chunk is committed in its own
	/// transaction with a checkpoint update, and all rows survive the full export → restore → import cycle.
	/// </summary>
	/// <remarks>
	///     <para>
	///     <b>Starting state:</b> Database has only the first migration applied. A total of
	///     <c>ImportChunkSizeRows + 1</c> (5001) <see cref="ParticipantEntity"/> rows are inserted so that
	///     the import spans exactly two chunks (5000 + 1). <see cref="ParticipantEntity"/> is used because
	///     it has no required FK dependencies and exists in the first migration.
	///     </para>
	///     <para>
	///     <b>Configuration:</b> Auto-migration enabled, <c>CreateBackupBeforeMigration = true</c>,
	///     <c>RestoreOnFailure = true</c>. An <see cref="InvalidOperationException"/> is injected at
	///     <c>HandleUpdateMigrations.BeforeMigrate</c> to trigger the backup → restore flow.
	///     </para>
	///     <para>
	///         <b>What this test proves that <c>RestoresUserDataIntact</c> does not:</b>
	///     </para>
	///     <list type="bullet">
	///         <item>The import pipeline commits in batches and resumes correctly across chunk boundaries</item>
	///         <item>
	///         No rows are lost or duplicated at the chunk boundary (row 5000 → 5001 transition)
	///         </item>
	///         <item>
	///         No <see cref="Guid"/> values are corrupted (e.g., zeroed out) across all rows, not just a handful
	///         </item>
	///     </list>
	///     <para>
	///     <b>Expected outcome:</b> <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> (non-transient).
	///     The restored database contains exactly <c>ImportChunkSizeRows + 1</c> participants, each with its
	///     original <see cref="ParticipantEntity.PublicId"/> intact.
	///     </para>
	///     <para>
	///     <b>Scope &amp; limitations:</b> This test exercises a single table with an auto-increment
	///     <c>long</c> PK and no FK dependencies. The import pipeline uses the same generic
	///     <c>INSERT INTO ... (col1, col2, ...) VALUES (...)</c> strategy for all tables
	///     (see <see cref="SqliteImportWriter"/>), so PK preservation and chunk-boundary
	///     behavior are representative. The following aspects are <em>not yet covered</em> anywhere and
	///     need dedicated tests in <c>SqliteImportWriterTests</c>:
	///     </para>
	///     <list type="bullet">
	///         <item>Auto-increment PK preservation across import (INSERT with explicit Id values)</item>
	///         <item>Tables with composite primary keys (e.g., <c>ConversationParticipantEntity</c>)</item>
	///         <item>FK-dependent insert ordering across multiple tables</item>
	///         <item>Sequence/auto-increment counter reset after import (<c>CleanupAfterImportAsync</c>)</item>
	///     </list>
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenRestoreSpansMultipleChunks_PreservesAllRows()
	{
		// Arrange — ImportChunkSizeRows + 1 rows forces the import to span exactly 2 chunks
		// (5000 + 1). This is the minimum needed to exercise the chunk-boundary commit logic.
		int rowCount = DataPortTuning.ImportChunkSizeRows + 1;

		using var backupDir = new TemporaryFolder("dbinit-volume");
		TestHarness harness = CreateHarness(options =>
		{
			options.AutoMigration.Enabled = true;
			options.AutoMigration.CreateBackupBeforeMigration = true;
			options.AutoMigration.RestoreOnFailure = true;
			options.AutoMigration.BackupDirectory = backupDir.Path;
		});
		try
		{
			// Start at the first migration only — this is the baseline the restore will rebuild to.
			await harness.MigrateToFirstMigrationOnlyAsync();

			// Seed 5001 participants with unique PublicIds. The set of GUIDs is our
			// "ground truth" for verifying that every row survives the chunk boundary.
			var expectedPublicIds = new HashSet<Guid>(rowCount);
			(AsyncServiceScope seedScope, LumaCoreDbContext seedCtx) = harness.CreateScopedDbContext();
			try
			{
				for (int i = 0; i < rowCount; i++)
				{
					var publicId = Guid.NewGuid();
					expectedPublicIds.Add(publicId);
					seedCtx.Participants.Add(
						new ParticipantEntity
						{
							PublicId = publicId,
							DisplayName = $"Volume-{i:D5}",
							CreatedAtUtc = harness.TimeProvider.GetUtcNow().UtcDateTime
						});

					// Flush every 500 rows to avoid excessive memory/change-tracker pressure.
					if ((i + 1) % 500 == 0)
					{
						await seedCtx.SaveChangesAsync();
					}
				}

				await seedCtx.SaveChangesAsync();
			}
			finally
			{
				await seedScope.DisposeAsync();
			}

			// Inject a failure that fires AFTER the pre-migration backup is written (so the
			// shuttle file contains all 5001 participants) but BEFORE MigrateAsync() executes.
			// StartAsync() will then: detect pending migrations → create backup → attempt
			// migration → fail → restore from backup (which forces a 2-chunk import).
			using ExecutionStageMonitor monitor = ExecutionStageMonitor
				.Configure()
				.ThrowAt(
					"HandleUpdateMigrations.BeforeMigrate",
					new InvalidOperationException("Simulated migration failure"));

			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — the migration failed (as intended), but the restore recovered the DB.
			// ManualInterventionRequired means: "migration broke, but your data is safe."
			Assert.Equal(DatabaseInitializationState.Failed, harness.Status.State);
			Assert.Equal(DatabaseFailureCategory.ManualInterventionRequired, harness.Status.FailureCategory);

			// Verify that all 5001 rows survived the roundtrip across the chunk boundary.
			(AsyncServiceScope verifyScope, LumaCoreDbContext verifyCtx) = harness.CreateScopedDbContext();
			try
			{
				// Row count — catches rows lost at the chunk boundary (5000 → 5001 transition).
				int restoredCount = await verifyCtx.Participants.CountAsync();
				Assert.Equal(rowCount, restoredCount);

				// Set equality — catches duplicated rows (A appears twice, B missing) and
				// corrupted GUIDs (e.g., zeroed out). PK stability is intentionally not
				// verified here because ParticipantEntity has no FK dependents in this test;
				// that concern belongs in SqliteImportWriterTests.
				var restoredIds = new HashSet<Guid>(
					await verifyCtx.Participants
						.Select(p => p.PublicId)
						.ToListAsync());
				Assert.True(
					expectedPublicIds.SetEquals(restoredIds),
					$"Expected {expectedPublicIds.Count} unique PublicIds, " +
					$"got {restoredIds.Count}. " +
					$"Missing: {expectedPublicIds.Except(restoredIds).Count()}, " +
					$"unexpected: {restoredIds.Except(expectedPublicIds).Count()}");
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

	#endregion
}
