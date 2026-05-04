// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.TestUtilities.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests;

// Resource garbage collector behaviour, exercised one cycle at a time.
//
// The BackgroundService loop itself (ExecuteAsync, RunAfterDelayAsync) is just a thin scheduler
// around RunCycleAsync; we drive the cycle directly via the InternalsVisibleTo seam so the tests
// stay deterministic and free of timing flakiness. The story:
//
//   1. MARK respects the grace period — fresh orphaned resources stay Active.
//   2. MARK skips a resource whose ContentHash already has a PendingDeletion sibling
//      (would violate the composite unique index).
//   3. SWEEP deletes the file FIRST, then the row — and on file failure the row stays put as
//      recovery metadata for the next cycle.
//   4. The throttle in ShouldRunAsync skips the whole cycle when another instance ran recently.
//   5. The ExecuteAsync entry point is a no-op when Enabled == false.
//   6. The IsBenignSweepRaceShape filter only admits the recovery path when the EF entries list
//      contains exactly the one expected Deleted ResourceEntity — every other shape (empty,
//      wrong state, foreign instance, extra entry) is rejected so a real concurrency conflict
//      surfaces as a Warning instead of being silently swallowed.
//
// Constructor null-guards live in ResourceCleanupServiceTests.Construction.cs.
// Fixture lifecycle, factory and seeding helpers live in ResourceCleanupServiceTests.Helpers.cs.
// Test-only doubles (RecordingStore, ShapedConcurrencyException, NewResourceEntity) live in
// ResourceCleanupServiceTests.TestModels.cs.

/// <summary>
/// Tests for <see cref="ResourceCleanupService"/>: the MARK → SWEEP garbage-collection background
/// service that promotes orphan <see cref="ResourceEntity"/> rows to
/// <see cref="ResourceDeletionState.PendingDeletion"/> and then deletes the underlying file +
/// row after the configured grace period.
/// </summary>
[Trait("Category", "Resources")]
public sealed partial class ResourceCleanupServiceTests
{
	#region MARK phase

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> does <b>not</b> mark an orphaned resource
	/// whose <see cref="ResourceEntity.CreatedAtUtc"/> is still inside the configured grace period.
	/// </summary>
	[Fact]
	public async Task RunCycleAsync_WhenOrphanInsideGracePeriod_LeavesResourceActive()
	{
		// Arrange: resource is orphaned (no references) but only 1 minute old; grace period is 5 minutes.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		ResourceEntity orphan = await SeedOrphanResourceAsync(createdAt: time.GetUtcNow().UtcDateTime.AddMinutes(-1));
		var store = new RecordingStore();
		ResourceCleanupService sut = CreateSut(store, time, gracePeriodMinutes: 5);

		// Act
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5), CancellationToken.None);

		// Assert: still Active, file untouched.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceEntity reloaded = await verify.Resources.AsNoTracking().SingleAsync(r => r.Id == orphan.Id);
		Assert.Equal(ResourceDeletionState.Active, reloaded.DeletionState);
		Assert.Equal(0, store.DeleteCount);
	}

	/// <summary>
	/// Verifies that an orphaned resource older than the grace period is promoted to
	/// <see cref="ResourceDeletionState.PendingDeletion"/>.
	/// </summary>
	[Fact]
	public async Task RunCycleAsync_WhenOrphanOlderThanGracePeriod_MarksPendingDeletion()
	{
		// Arrange
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		ResourceEntity orphan = await SeedOrphanResourceAsync(createdAt: time.GetUtcNow().UtcDateTime.AddMinutes(-30));
		var store = new RecordingStore();
		ResourceCleanupService sut = CreateSut(store, time, gracePeriodMinutes: 5);

		// Act
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5), CancellationToken.None);

		// Assert: the same cycle marks AND sweeps, so we look at the absence of the row instead.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.False(await verify.Resources.AsNoTracking().AnyAsync(r => r.Id == orphan.Id));
		Assert.Equal(1, store.DeleteCount);
	}

	/// <summary>
	/// Verifies that MARK skips an orphaned <see cref="ResourceDeletionState.Active"/> resource when
	/// another row with the same <see cref="ResourceEntity.ContentHash"/> already sits in
	/// <see cref="ResourceDeletionState.PendingDeletion"/> — promoting it would violate the composite
	/// unique index on <c>(ContentHash, DeletionState)</c>.
	/// </summary>
	[Fact]
	public async Task RunCycleAsync_WhenPendingSiblingWithSameHashExists_DoesNotPromoteActive()
	{
		// Arrange: two rows share the same ContentHash. One is already PendingDeletion (and will be
		// swept this cycle); the other is Active and orphaned. MARK must NOT promote the second one
		// because the unique index would refuse the duplicate.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		const string hash = "duplicatehash00000000000000000000000000000000000000000000000001";

		var pending = new ResourceEntity
		{
			ContentHash = hash,
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = 1,
			CreatedAtUtc = time.GetUtcNow().UtcDateTime.AddMinutes(-60),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.PendingDeletion
		};
		var active = new ResourceEntity
		{
			ContentHash = hash,
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = 1,
			CreatedAtUtc = time.GetUtcNow().UtcDateTime.AddMinutes(-60),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.Active
		};
		mFixture.DbContext.Resources.AddRange(pending, active);
		await mFixture.DbContext.SaveChangesAsync();

		var store = new RecordingStore();
		ResourceCleanupService sut = CreateSut(store, time, gracePeriodMinutes: 5);

		// Act
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5), CancellationToken.None);

		// Assert: pending sibling was swept, but the Active row must still be Active.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceEntity reloaded = await verify.Resources.AsNoTracking().SingleAsync(r => r.Id == active.Id);
		Assert.Equal(ResourceDeletionState.Active, reloaded.DeletionState);
		Assert.False(await verify.Resources.AsNoTracking().AnyAsync(r => r.Id == pending.Id));
	}

	/// <summary>
	/// Verifies that MARK leaves an <see cref="ResourceDeletionState.Active"/> resource alone when it
	/// has at least one <see cref="ResourceReferenceEntity"/> — even if the row is well past the grace
	/// period. This pins the <c>!References.Any()</c> clause of the MARK predicate: without it, a
	/// resource that legitimately belongs to a long-lived owner would be promoted (and then swept) on
	/// the very next cycle, causing silent data loss for the owning entity.
	/// </summary>
	[Fact]
	public async Task RunCycleAsync_WhenResourceHasReference_DoesNotPromoteEvenIfPastGracePeriod()
	{
		// Arrange: a resource that would qualify on every other criterion (Active, well past grace).
		// The single reference is the only thing keeping it alive — exactly the negative case the
		// MARK predicate must respect.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		ResourceEntity referenced = await SeedOrphanResourceAsync(
			                            createdAt: time.GetUtcNow().UtcDateTime.AddMinutes(-60));

		var reference = new ResourceReferenceEntity
		{
			PublicId = Guid.NewGuid(),
			ResourceId = referenced.Id,
			OwnerKind = ResourceOwnerKind.User,
			OwnerId = new ResourceOwnerId(42),
			ContentType = "text/plain",
			OriginalFileName = null,
			CreatedAtUtc = time.GetUtcNow().UtcDateTime.AddMinutes(-30)
		};
		mFixture.DbContext.ResourceReferences.Add(reference);
		await mFixture.DbContext.SaveChangesAsync();

		var store = new RecordingStore();
		ResourceCleanupService sut = CreateSut(store, time, gracePeriodMinutes: 5);

		// Act
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5), CancellationToken.None);

		// Assert: the resource row survives untouched and the store was not asked to delete anything.
		// Both checks together rule out a "promoted-but-not-yet-swept" intermediate state.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceEntity reloaded = await verify.Resources.AsNoTracking().SingleAsync(r => r.Id == referenced.Id);
		Assert.Equal(ResourceDeletionState.Active, reloaded.DeletionState);
		Assert.Equal(0, store.DeleteCount);
	}

	#endregion

	#region SWEEP phase

	/// <summary>
	/// Verifies that SWEEP deletes the physical file <em>before</em> removing the database row, and
	/// that on file-deletion failure the row remains as recovery metadata for the next cycle.
	/// </summary>
	[Fact]
	public async Task RunCycleAsync_WhenStoreDeleteThrows_LeavesRowForRecovery()
	{
		// Arrange: one PendingDeletion resource. The store throws on DeleteAsync, so the row must
		// stay in the DB. Use a fresh hash so MARK has nothing to do this cycle.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		var pending = new ResourceEntity
		{
			ContentHash = "feedface00000000000000000000000000000000000000000000000000000001",
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = 1,
			CreatedAtUtc = time.GetUtcNow().UtcDateTime.AddMinutes(-60),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.PendingDeletion
		};
		mFixture.DbContext.Resources.Add(pending);
		await mFixture.DbContext.SaveChangesAsync();

		var store = new RecordingStore { ThrowOnDelete = true };
		ResourceCleanupService sut = CreateSut(store, time, gracePeriodMinutes: 5);

		// Act
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5), CancellationToken.None);

		// Assert: file delete was attempted (and failed); DB row survived.
		Assert.Equal(1, store.DeleteCount);
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceEntity reloaded = await verify.Resources.AsNoTracking().SingleAsync(r => r.Id == pending.Id);
		Assert.Equal(ResourceDeletionState.PendingDeletion, reloaded.DeletionState);
	}

	/// <summary>
	/// Verifies that SWEEP honours <see cref="ResourceCleanupOptions.SweepBatchSize"/>: when more
	/// <see cref="ResourceDeletionState.PendingDeletion"/> rows exist than fit into one batch, only
	/// the configured batch size is processed in a single cycle. The remainder must stay in the DB
	/// (and on disk) for the next cycle. Without this cap a single cycle could lock the table for
	/// minutes on large backlogs and starve concurrent uploads.
	/// </summary>
	[Fact]
	public async Task RunCycleAsync_WhenPendingExceedsSweepBatchSize_SweepsOnlyBatchSizePerCycle()
	{
		// Arrange: 5 PendingDeletion resources with unique hashes. Use a high grace period and an
		// orphan timestamp INSIDE the grace window so MARK has nothing to do — that way the cycle's
		// only effect comes from SWEEP, and we can assert exactly on the batch-size behaviour.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		const int seedCount = 5;
		const int batchSize = 3;

		var seeded = new List<ResourceEntity>(seedCount);
		for (int i = 0; i < seedCount; i++)
		{
			var pending = new ResourceEntity
			{
				ContentHash = $"batch{i:D2}00000000000000000000000000000000000000000000000000000000000",
				StoragePath = Guid.NewGuid().ToString(),
				SizeBytes = 1,
				CreatedAtUtc = time.GetUtcNow().UtcDateTime.AddMinutes(-1),
				CreatedByParticipantId = null,
				DeletionState = ResourceDeletionState.PendingDeletion
			};
			mFixture.DbContext.Resources.Add(pending);
			seeded.Add(pending);
		}
		await mFixture.DbContext.SaveChangesAsync();

		var store = new RecordingStore();
		// Use a high grace period so MARK is a no-op this cycle (the orphans were created 1 minute
		// ago, well inside the 60-minute grace window) and we measure pure SWEEP behaviour.
		ResourceCleanupOptions options = new()
		{
			Enabled = true,
			GracePeriodMinutes = 60,
			IntervalMinutes = 60,
			SweepBatchSize = batchSize
		};
		ResourceCleanupService sut = CreateSut(
			store,
			time,
			gracePeriodMinutes: options.GracePeriodMinutes,
			intervalMinutes: options.IntervalMinutes);

		// Act
		await sut.RunCycleAsync(options, CancellationToken.None);

		// Assert: exactly batchSize rows were swept; the remainder is still in PendingDeletion and
		// the store was called exactly batchSize times. This rules out both "swept too many" and
		// "swept the file but left the row" failure modes.
		Assert.Equal(batchSize, store.DeleteCount);
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		int remaining = await verify.Resources
			                .AsNoTracking()
			                .CountAsync(r => r.DeletionState == ResourceDeletionState.PendingDeletion);
		Assert.Equal(seedCount - batchSize, remaining);
	}

	/// <summary>
	/// Verifies that SWEEP treats a <see cref="DbUpdateConcurrencyException"/> caused by a
	/// concurrent multi-node cleanup (another instance already deleted the row between this
	/// instance's SELECT and DELETE) as a successful sweep — not a failure — and does <b>not</b>
	/// surface a <see cref="LogLevel.Warning"/> entry.
	/// </summary>
	/// <remarks>
	/// Multi-node operation is the supported topology, so these races are expected and benign:
	/// the file is deleted file-first (idempotent), the row is gone, and the per-iteration goal
	/// is reached. Logging a Warning here would alarm operators without cause.
	/// <para>
	/// Race injection mirrors <see cref="RunCycleAsync_WhenGcStateInsertedConcurrently_RecoversAndUpdatesExistingRow"/>:
	/// when the SUT is about to <c>SaveChangesAsync</c> the <see cref="EntityState.Deleted"/>
	/// <see cref="ResourceEntity"/>, a side context on the shared SQLite connection deletes the
	/// same row first, so the SUT's UPDATE/DELETE affects 0 rows and EF throws.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RunCycleAsync_WhenSweepRowDeletedByConcurrentNode_CountsAsSweptAndDoesNotWarn()
	{
		// Arrange: one PendingDeletion resource ready to be swept this cycle.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 17, 12, 0, 0, TimeSpan.Zero));
		var pending = new ResourceEntity
		{
			ContentHash = "c0ffee0000000000000000000000000000000000000000000000000000000042",
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = 1,
			CreatedAtUtc = time.GetUtcNow().UtcDateTime.AddMinutes(-60),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.PendingDeletion
		};
		mFixture.DbContext.Resources.Add(pending);
		await mFixture.DbContext.SaveChangesAsync();

		bool racePerformed = false;
		var store = new RecordingStore();
		var logger = new ListLogger<ResourceCleanupService>();

		// Race injection: when the SUT is about to save the Deleted ResourceEntity, a "concurrent"
		// cleanup instance (simulated via a side context on the same shared SQLite connection)
		// deletes the same row first. The SUT's SaveChangesAsync then affects 0 rows and EF throws
		// DbUpdateConcurrencyException — exercising the new benign-race handler.
		ResourceCleanupService sut = CreateSut(
			store,
			time,
			gracePeriodMinutes: 5,
			configureContext: ctx =>
			{
				ctx.SavingChanges += (_, _) =>
				{
					if (racePerformed)
						return;

					// Only inject when the SUT is saving the Deleted ResourceEntity (the SWEEP DELETE) —
					// not for the GC state singleton or any earlier MARK ExecuteUpdate.
					if (!ctx.ChangeTracker.Entries<ResourceEntity>()
						    .Any(e => e.State == EntityState.Deleted))
						return;

					racePerformed = true;
					using LumaCoreDbContext side = mFixture.CreateDbContext();
					side.Resources
						.Where(r => r.Id == pending.Id)
						.ExecuteDelete();
				};
			},
			logger: logger);

		// Act: must not throw — the new catch (DbUpdateConcurrencyException) handler swallows it.
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5), CancellationToken.None);

		// Assert: the race was actually triggered (otherwise the test asserts on the wrong path).
		Assert.True(racePerformed, "Race injection did not fire — test does not exercise the concurrent-delete path.");

		// Assert: file delete was attempted file-first, exactly once.
		Assert.Equal(1, store.DeleteCount);

		// Assert: the row is gone (the side context's delete won; the SUT detached its tracker entry).
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.False(await verify.Resources.AsNoTracking().AnyAsync(r => r.Id == pending.Id));

		// Assert: no Warning was logged — multi-node sweep races are benign and must stay quiet.
		// A Debug entry is acceptable (and expected), but Warning/Error must not appear. Assert.All
		// (instead of Assert.DoesNotContain) keeps the diagnostic faithful: on regression the failing
		// assertion message names the offending entry's level and message instead of just "a match was
		// found".
		Assert.All(
			logger.Entries,
			e => Assert.True(
				e.Level < LogLevel.Warning,
				$"Unexpected log entry at level {e.Level}: {e.Message}"));
	}

	#endregion

	#region Throttle

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> skips a cycle when the persisted
	/// <see cref="ResourceGcStateEntity.LastRunAtUtc"/> indicates another instance ran within the
	/// configured interval.
	/// </summary>
	[Fact]
	public async Task RunCycleAsync_WhenLastRunIsWithinInterval_SkipsCycle()
	{
		// Arrange: persisted LastRunAtUtc only 5 minutes ago, but the configured interval is 60.
		// A pending-deletion row is also seeded — if the throttle does NOT skip, it would be swept.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		mFixture.DbContext.ResourceGcState.Add(
			new ResourceGcStateEntity
			{
				Id = 1,
				LastRunAtUtc = time.GetUtcNow().UtcDateTime.AddMinutes(-5)
			});
		var pending = new ResourceEntity
		{
			ContentHash = "ca11ab1e000000000000000000000000000000000000000000000000000000a1",
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = 1,
			CreatedAtUtc = time.GetUtcNow().UtcDateTime.AddMinutes(-60),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.PendingDeletion
		};
		mFixture.DbContext.Resources.Add(pending);
		await mFixture.DbContext.SaveChangesAsync();

		var store = new RecordingStore();
		ResourceCleanupService sut = CreateSut(store, time, gracePeriodMinutes: 5);

		// Act
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5, intervalMinutes: 60), CancellationToken.None);

		// Assert: nothing was deleted; the pending row is still there.
		Assert.Equal(0, store.DeleteCount);
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.True(await verify.Resources.AsNoTracking().AnyAsync(r => r.Id == pending.Id));
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> upserts the
	/// <see cref="ResourceGcStateEntity"/> row with the current UTC timestamp on a successful cycle.
	/// </summary>
	[Fact]
	public async Task RunCycleAsync_WhenCompletes_UpsertsLastRunTimestamp()
	{
		// Arrange: no prior state row; clock is fixed.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		var store = new RecordingStore();
		ResourceCleanupService sut = CreateSut(store, time, gracePeriodMinutes: 5);

		// Act
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5), CancellationToken.None);

		// Assert
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceGcStateEntity state = await verify.ResourceGcState.AsNoTracking().SingleAsync();
		Assert.Equal(1, state.Id);
		Assert.Equal(time.GetUtcNow().UtcDateTime, state.LastRunAtUtc);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> updates the existing
	/// <see cref="ResourceGcStateEntity"/> singleton row in place (no second insert) when the row
	/// was already persisted by a previous cycle. Exercises the UPDATE branch of <c>UpdateGcStateAsync</c>,
	/// which the first-run INSERT branch and the concurrent-insert recovery branch leave uncovered.
	/// </summary>
	[Fact]
	public async Task RunCycleAsync_WhenGcStateAlreadyExists_UpdatesTimestampInPlace()
	{
		// Arrange: the singleton row already exists from an earlier (simulated) cycle, but its
		// timestamp is old enough that the throttle still admits the new cycle (interval = 60min,
		// last run = 90min ago).
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		DateTime previousRun = time.GetUtcNow().UtcDateTime.AddMinutes(-90);
		mFixture.DbContext.ResourceGcState.Add(new ResourceGcStateEntity { Id = 1, LastRunAtUtc = previousRun });
		await mFixture.DbContext.SaveChangesAsync();

		var store = new RecordingStore();
		ResourceCleanupService sut = CreateSut(store, time, gracePeriodMinutes: 5);

		// Act
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5, intervalMinutes: 60), CancellationToken.None);

		// Assert: still exactly one row, timestamp updated to the SUT's "now".
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		List<ResourceGcStateEntity> rows = await verify.ResourceGcState.AsNoTracking().ToListAsync();
		Assert.Single(rows);
		Assert.Equal(1, rows[0].Id);
		Assert.Equal(time.GetUtcNow().UtcDateTime, rows[0].LastRunAtUtc);
		Assert.NotEqual(previousRun, rows[0].LastRunAtUtc);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> tolerates a primary-key conflict on the
	/// <see cref="ResourceGcStateEntity"/> singleton row when a concurrent cleanup instance inserts
	/// the row between this instance's <c>FirstOrDefaultAsync</c> read (which returned <see langword="null"/>)
	/// and its <c>SaveChangesAsync</c> insert: the cycle must classify the
	/// <see cref="DbUpdateException"/>, detach the pending insert, re-fetch the winning row, apply the
	/// timestamp update, and complete without surfacing the exception.
	/// </summary>
	/// <remarks>
	/// We inject the race via <see cref="DbContext.SavingChanges"/>: the handler fires once, when the
	/// SUT is about to save the new <see cref="ResourceGcStateEntity"/>. It opens a side context on
	/// the shared SQLite connection and inserts the singleton row first, so the SUT's own
	/// <c>SaveChangesAsync</c> immediately fails with a PK conflict on <c>Id = 1</c>. The recovery
	/// path must then update the existing row to the SUT's <c>now</c> value (overwriting the older
	/// race-winner timestamp).
	/// </remarks>
	[Fact]
	public async Task RunCycleAsync_WhenGcStateInsertedConcurrently_RecoversAndUpdatesExistingRow()
	{
		// Arrange: no prior state row; clock is fixed at the SUT's expected "now".
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 16, 12, 0, 0, TimeSpan.Zero));
		DateTime sutNow = time.GetUtcNow().UtcDateTime;
		DateTime raceWinnerTimestamp = sutNow.AddMinutes(-1);

		bool racePerformed = false;
		var store = new RecordingStore();

		// Race injection: when the SUT is about to save its newly Added ResourceGcStateEntity, a
		// "concurrent" cleanup instance (simulated via a side context on the same shared SQLite
		// connection) sneaks in and inserts the singleton row first. The SUT's SaveChangesAsync then
		// hits a PK conflict on Id = 1 and must take the recovery path.
		ResourceCleanupService sut = CreateSut(
			store,
			time,
			gracePeriodMinutes: 5,
			configureContext: ctx =>
			{
				ctx.SavingChanges += (_, _) =>
				{
					if (racePerformed)
						return;

					// Only inject when the SUT is saving the GC state singleton — not for any earlier
					// SaveChanges within the same cycle (e.g., MARK/SWEEP).
					if (!ctx.ChangeTracker.Entries<ResourceGcStateEntity>().Any())
						return;

					racePerformed = true;
					using LumaCoreDbContext side = mFixture.CreateDbContext();
					side.ResourceGcState.Add(
						new ResourceGcStateEntity
						{
							Id = 1,
							LastRunAtUtc = raceWinnerTimestamp
						});
					side.SaveChanges();
				};
			});

		// Act: must not throw — the recovery path swallows the DbUpdateException.
		await sut.RunCycleAsync(BuildOptions(gracePeriodMinutes: 5), CancellationToken.None);

		// Assert: the race was actually triggered (otherwise the test asserts on the wrong path).
		Assert.True(racePerformed, "Race injection did not fire — test does not exercise the recovery path.");

		// Assert: exactly one row exists, and its timestamp was updated by the SUT's recovery path
		// (overwriting the older race-winner timestamp).
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		ResourceGcStateEntity state = await verify.ResourceGcState.AsNoTracking().SingleAsync();
		Assert.Equal(1, state.Id);
		Assert.Equal(sutNow, state.LastRunAtUtc);
	}

	/// <summary>
	/// F3 audit-fix regression: verifies the defensive fallback in <c>UpdateGcStateAsync</c>'s
	/// <see cref="DbUpdateException"/> handler. When the insert fails for a reason <em>other</em>
	/// than the documented PK race (i.e. the row is still missing after the conflict-recovery
	/// re-query), the service must throw an <see cref="InvalidOperationException"/> with the
	/// original <see cref="DbUpdateException"/> preserved as <see cref="Exception.InnerException"/>
	/// — instead of the misleading "sequence contains no elements" that the previous
	/// <c>SingleAsync</c>-based code produced.
	/// </summary>
	/// <remarks>
	/// Fault injection: a <see cref="DbContext.SavingChanges"/> hook throws a synthetic
	/// <see cref="DbUpdateException"/> when the SUT is about to insert the
	/// <see cref="ResourceGcStateEntity"/> singleton. Because EF never actually issues the INSERT,
	/// the conflict-recovery re-query returns <see langword="null"/>, exactly the pathological
	/// shape the defensive branch is designed to surface loudly.
	/// </remarks>
	[Fact]
	public async Task RunCycleAsync_WhenGcStateInsertFailsAndRowStillMissing_ThrowsWithOriginalDbUpdateAsInner()
	{
		// Arrange: no prior state row.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 17, 12, 0, 0, TimeSpan.Zero));
		var store = new RecordingStore();
		var injectedDbUpdate = new DbUpdateException("synthetic non-PK failure");

		ResourceCleanupService sut = CreateSut(
			store,
			time,
			gracePeriodMinutes: 5,
			configureContext: ctx =>
			{
				ctx.SavingChanges += (_, _) =>
				{
					// Only intercept the GC-state insert; let MARK/SWEEP saves pass through.
					if (!ctx.ChangeTracker.Entries<ResourceGcStateEntity>().Any(e => e.State == EntityState.Added))
						return;

					// Throwing from SavingChanges aborts the SaveChanges call with this exception
					// directly (no DbUpdateException wrapping by EF), which is exactly what the catch
					// (DbUpdateException) handler expects to receive when the underlying provider
					// surfaces a non-PK failure. The row is never persisted, so the recovery re-query
					// will return null → defensive branch fires.
					throw injectedDbUpdate;
				};
			});

		// Act + Assert: the SUT must wrap the failure in InvalidOperationException with the
		// original DbUpdateException preserved as InnerException — not a misleading
		// "sequence contains no elements".
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RunCycleAsync(
			         BuildOptions(gracePeriodMinutes: 5),
			         CancellationToken.None));
		Assert.Equal(
			"ResourceGcState upsert failed and the singleton row is still missing after the " +
			"conflict-recovery re-query. The original DbUpdateException is preserved as the " +
			"inner exception.",
			ex.Message);
		Assert.Same(injectedDbUpdate, ex.InnerException);

		// Assert: nothing got committed.
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.False(await verify.ResourceGcState.AsNoTracking().AnyAsync());
	}

	#endregion

	#region ExecuteAsync gating

	/// <summary>
	/// Verifies that the BackgroundService loop is a no-op when
	/// <see cref="ResourceCleanupOptions.Enabled"/> is <see langword="false"/>: it returns
	/// immediately without scheduling any cycle.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenDisabled_ReturnsWithoutRunningCycle()
	{
		// Arrange: a pending row exists; if any cycle ran, it would be swept.
		FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
		var pending = new ResourceEntity
		{
			ContentHash = "d15ab1ed00000000000000000000000000000000000000000000000000000001",
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = 1,
			CreatedAtUtc = time.GetUtcNow().UtcDateTime.AddMinutes(-60),
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.PendingDeletion
		};
		mFixture.DbContext.Resources.Add(pending);
		await mFixture.DbContext.SaveChangesAsync();

		var store = new RecordingStore();
		ResourceCleanupService sut = CreateSut(store, time, gracePeriodMinutes: 5, enabled: false);

		// Act: StartAsync starts ExecuteAsync; since Enabled=false the loop returns immediately.
		await sut.StartAsync(CancellationToken.None);
		await sut.StopAsync(CancellationToken.None);

		// Assert: nothing happened.
		Assert.Equal(0, store.DeleteCount);
		await using LumaCoreDbContext verify = mFixture.CreateDbContext();
		Assert.True(await verify.Resources.AsNoTracking().AnyAsync(r => r.Id == pending.Id));
	}

	#endregion

	#region IsBenignSweepRaceShape

	// These tests pin down the shape filter that decides whether a DbUpdateConcurrencyException
	// raised by the SWEEP SaveChangesAsync() may be treated as a benign multi-node race. The
	// filter must be strict — every non-matching shape (empty, wrong state, foreign instance,
	// extra entry) must be rejected so a real concurrency conflict surfaces as a Warning instead
	// of being silently swallowed. The positive case is already exercised end-to-end by
	// RunCycleAsync_WhenSweepRowDeletedByConcurrentNode_CountsAsSweptAndDoesNotWarn; the cases
	// here cover the negative branches that the end-to-end harness cannot reach.

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService.IsBenignSweepRaceShape"/> rejects an empty
	/// <see cref="DbUpdateException.Entries"/> list — without entries the exception cannot be
	/// attributed to the expected single-row delete.
	/// </summary>
	[Fact]
	public void IsBenignSweepRaceShape_WhenEntriesIsEmpty_ReturnsFalse()
	{
		// Arrange
		ResourceEntity expected = NewResourceEntity();
		var ex = new ShapedConcurrencyException([]);

		// Act
		bool result = ResourceCleanupService.IsBenignSweepRaceShape(ex, expected);

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService.IsBenignSweepRaceShape"/> rejects an entry
	/// whose state is not <see cref="EntityState.Deleted"/> — a SWEEP cycle only ever issues a
	/// <c>DELETE</c>, so a <see cref="EntityState.Modified"/> entry signals a different conflict.
	/// </summary>
	[Fact]
	public async Task IsBenignSweepRaceShape_WhenEntryStateIsNotDeleted_ReturnsFalse()
	{
		// Arrange: attach the expected resource as Modified instead of Deleted.
		ResourceEntity expected = await SeedOrphanResourceAsync(
			                          createdAt: new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
		EntityEntry entry = mFixture.DbContext.Entry(expected);
		entry.State = EntityState.Modified;
		var ex = new ShapedConcurrencyException([entry]);

		// Act
		bool result = ResourceCleanupService.IsBenignSweepRaceShape(ex, expected);

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService.IsBenignSweepRaceShape"/> rejects a Deleted
	/// entry that points at a <i>different</i> entity instance than the one this iteration is
	/// trying to delete — the conflict belongs to a sibling row, not to <c>expected</c>.
	/// </summary>
	[Fact]
	public async Task IsBenignSweepRaceShape_WhenEntryReferencesDifferentEntity_ReturnsFalse()
	{
		// Arrange: two separate resources; the exception's entry is for the foreign one.
		DateTime createdAt = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		ResourceEntity expected = await SeedOrphanResourceAsync(createdAt);
		ResourceEntity foreign = await SeedOrphanResourceAsync(createdAt);
		EntityEntry foreignEntry = mFixture.DbContext.Entry(foreign);
		foreignEntry.State = EntityState.Deleted;
		var ex = new ShapedConcurrencyException([foreignEntry]);

		// Act
		bool result = ResourceCleanupService.IsBenignSweepRaceShape(ex, expected);

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService.IsBenignSweepRaceShape"/> rejects a list
	/// that contains the expected delete <em>plus</em> at least one additional entry — a benign
	/// race involves exactly one row, so any extra entry indicates a wider conflict that must
	/// surface as a real failure.
	/// </summary>
	[Fact]
	public async Task IsBenignSweepRaceShape_WhenEntriesContainExtraEntry_ReturnsFalse()
	{
		// Arrange: expected delete is in the list, but a second Deleted entry tags along.
		DateTime createdAt = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		ResourceEntity expected = await SeedOrphanResourceAsync(createdAt);
		ResourceEntity foreign = await SeedOrphanResourceAsync(createdAt);
		EntityEntry expectedEntry = mFixture.DbContext.Entry(expected);
		EntityEntry foreignEntry = mFixture.DbContext.Entry(foreign);
		expectedEntry.State = EntityState.Deleted;
		foreignEntry.State = EntityState.Deleted;
		var ex = new ShapedConcurrencyException([expectedEntry, foreignEntry]);

		// Act
		bool result = ResourceCleanupService.IsBenignSweepRaceShape(ex, expected);

		// Assert: the loop hits the expected entry first (passes), then the foreign one (fails on
		// ReferenceEquals) and returns false — confirming that a single match is not enough.
		Assert.False(result);
	}

	/// <summary>
	/// Positive control: verifies that <see cref="ResourceCleanupService.IsBenignSweepRaceShape"/>
	/// returns <see langword="true"/> for the canonical benign-race shape — a single Deleted entry
	/// pointing at exactly the expected entity. Pairs with the four negative tests above so the
	/// branch table is fully covered by direct unit tests.
	/// </summary>
	[Fact]
	public async Task IsBenignSweepRaceShape_WhenSingleDeletedEntryMatchesExpected_ReturnsTrue()
	{
		// Arrange
		ResourceEntity expected = await SeedOrphanResourceAsync(
			                          createdAt: new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
		EntityEntry entry = mFixture.DbContext.Entry(expected);
		entry.State = EntityState.Deleted;
		var ex = new ShapedConcurrencyException([entry]);

		// Act
		bool result = ResourceCleanupService.IsBenignSweepRaceShape(ex, expected);

		// Assert
		Assert.True(result);
	}

	#endregion
}
