// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Initialization;
using LumaCore.Data.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Data;

/// <summary>
/// Background service that periodically reclaims orphaned resource files via a two-phase
/// MARK → SWEEP garbage collection cycle.
/// </summary>
/// <remarks>
///     <para>
///     <b>MARK phase:</b> Promotes <see cref="ResourceDeletionState.Active"/> resources to
///     <see cref="ResourceDeletionState.PendingDeletion"/> when all of the following are true:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>The resource has zero <see cref="ResourceReferenceEntity"/> rows.</description>
///         </item>
///         <item>
///             <description>
///             The resource's <see cref="ResourceEntity.CreatedAtUtc"/> is older than the configured grace period
///             (protects freshly uploaded resources whose reference may not have been attached yet).
///             </description>
///         </item>
///         <item>
///             <description>
///             No <see cref="ResourceDeletionState.PendingDeletion"/> row with the same
///             <see cref="ResourceEntity.ContentHash"/> already exists. Promoting would violate the composite
///             unique index on <c>(ContentHash, DeletionState)</c> — the existing PendingDeletion row must be
///             swept first.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>SWEEP phase:</b> For each <see cref="ResourceDeletionState.PendingDeletion"/> row (up to
///     <see cref="ResourceCleanupOptions.SweepBatchSize"/>):
///     </para>
///     <list type="number">
///         <item>
///             <description>Delete the physical file via <see cref="IResourceStore.DeleteAsync"/>.</description>
///         </item>
///         <item>
///             <description>Remove the database row.</description>
///         </item>
///     </list>
///     <para>
///     File-first ordering preserves the database row as recovery metadata if the file deletion fails.
///     Each resource is processed independently — a failure on one does not block others.
///     </para>
///     <para>
///     <b>Horizontal scaling (best-effort throttle):</b> The <see cref="ResourceGcStateEntity"/> singleton row
///     stores the timestamp of the last completed run. Before starting a cycle, the service checks whether
///     the configured interval has elapsed since that timestamp. This is a <em>best-effort</em> throttle —
///     the read in <c>ShouldRunAsync()</c> and the write in <c>UpdateGcStateAsync()</c> are not serialized as a
///     single atomic compare-and-swap, so two instances starting their cycles within the same scheduler tick
///     can both observe "interval elapsed" and run concurrently. The MARK and SWEEP phases are designed to be
///     safe under this scenario:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             MARK is a single <c>UPDATE</c> statement — duplicate executions promote the same rows to the
///             same state without producing inconsistent intermediate states.
///             </description>
///         </item>
///         <item>
///             <description>
///             SWEEP iterates per-row with independent error handling, and <see cref="IResourceStore.DeleteAsync"/>
///             is idempotent — a missing file is reported, not an error.
///             </description>
///         </item>
///     </list>
///     <para>
///     The throttle therefore reduces — but does not eliminate — redundant work across instances. If strict
///     single-runner semantics are required, an external lease (e.g. a database advisory lock) would be needed.
///     </para>
/// </remarks>
public sealed class ResourceCleanupService : BackgroundService
{
	/// <summary>
	/// Short delay before the first GC cycle to let the application stabilize (DB migrations, seeding).
	/// This is an internal startup strategy — not user-configurable. The <see cref="ShouldRunAsync"/> throttle
	/// prevents redundant work if another instance completed a cycle recently.
	/// </summary>
	private static readonly TimeSpan sInitialDelay = TimeSpan.FromSeconds(30);

	private readonly IServiceProvider                 mServiceProvider;
	private readonly IResourceStore                   mStore;
	private readonly DatabaseInitializationStatus     mDbStatus;
	private readonly IOptions<ResourceCleanupOptions> mOptions;
	private readonly TimeProvider                     mTimeProvider;
	private readonly ILogger<ResourceCleanupService>  mLogger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceCleanupService"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for creating scoped database contexts.</param>
	/// <param name="store">The resource file store for deleting physical files during sweep.</param>
	/// <param name="dbStatus">The database initialization status to gate GC until the database is ready.</param>
	/// <param name="options">The cleanup configuration options.</param>
	/// <param name="timeProvider">The time provider for obtaining UTC timestamps and delays.</param>
	/// <param name="logger">The logger for diagnostic output.</param>
	public ResourceCleanupService(
		IServiceProvider                 serviceProvider,
		IResourceStore                   store,
		DatabaseInitializationStatus     dbStatus,
		IOptions<ResourceCleanupOptions> options,
		TimeProvider                     timeProvider,
		ILogger<ResourceCleanupService>  logger)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(dbStatus);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(timeProvider);
		ArgumentNullException.ThrowIfNull(logger);

		mServiceProvider = serviceProvider;
		mStore = store;
		mDbStatus = dbStatus;
		mOptions = options;
		mTimeProvider = timeProvider;
		mLogger = logger;
	}

	/// <inheritdoc/>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		ResourceCleanupOptions options = mOptions.Value;

		if (!options.Enabled)
		{
			mLogger.LogDebug("Resource cleanup service is disabled via configuration");
			return;
		}

		TimeSpan interval = TimeSpan.FromMinutes(options.IntervalMinutes);

		mLogger.LogDebug(
			"Resource cleanup service started (interval: {IntervalMinutes}min, grace: {GracePeriodMinutes}min, " +
			"batch: {SweepBatchSize})",
			options.IntervalMinutes,
			options.GracePeriodMinutes,
			options.SweepBatchSize);

		// Run an initial cycle shortly after startup to sweep PendingDeletion leftovers from a
		// previous crash or unclean shutdown. The ShouldRunAsync() throttle prevents redundant work
		// if another instance (or the same instance before a quick restart) completed a cycle recently.
		await RunAfterDelayAsync(sInitialDelay, options, stoppingToken).ConfigureAwait(false);

		while (!stoppingToken.IsCancellationRequested)
		{
			await RunAfterDelayAsync(interval, options, stoppingToken).ConfigureAwait(false);
		}

		mLogger.LogDebug("Resource cleanup service stopped");
	}

	/// <summary>
	/// Waits for the specified <paramref name="delay"/>, then runs a single GC cycle if the database is ready.
	/// </summary>
	/// <param name="delay">The delay to wait before attempting the cycle.</param>
	/// <param name="options">The cleanup configuration.</param>
	/// <param name="stoppingToken">A token that signals when the hosted service should stop.</param>
	private async Task RunAfterDelayAsync(
		TimeSpan               delay,
		ResourceCleanupOptions options,
		CancellationToken      stoppingToken)
	{
		try
		{
			await Task.Delay(delay, mTimeProvider, stoppingToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		if (!mDbStatus.IsReady)
		{
			mLogger.LogDebug("Resource cleanup skipped: database not ready");
			return;
		}

		try
		{
			await RunCycleAsync(options, stoppingToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Shutting down — exit gracefully.
		}
		catch (Exception ex)
		{
			// Non-fatal: log and continue — the next cycle will retry.
			mLogger.LogWarning(ex, "Resource cleanup cycle failed");
		}
	}

	/// <summary>
	/// Executes a single MARK → SWEEP cycle within a scoped database context.
	/// </summary>
	/// <param name="options">The cleanup configuration.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// Exposed as <see langword="internal"/> so the data-layer test assembly can drive a single cycle
	/// deterministically via the <c>InternalsVisibleTo</c> grant on <c>LumaCore.Data.Tests</c>, instead
	/// of going through the long-lived <see cref="ExecuteAsync"/> loop and its startup delay.
	/// </remarks>
	internal async Task RunCycleAsync(ResourceCleanupOptions options, CancellationToken cancellationToken)
	{
		AsyncServiceScope scope = mServiceProvider.CreateAsyncScope();
		try
		{
			var dbContext = scope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();

			// Throttle check: skip if another instance completed a cycle recently.
			if (!await ShouldRunAsync(dbContext, options, cancellationToken).ConfigureAwait(false))
			{
				mLogger.LogDebug("Resource cleanup skipped: another instance ran recently");
				return;
			}

			// MARK: promote orphaned Active resources to PendingDeletion.
			int marked = await MarkOrphanedResourcesAsync(dbContext, options, cancellationToken)
				             .ConfigureAwait(false);

			// SWEEP: delete files and DB rows for PendingDeletion resources.
			(int swept, int failed) = await SweepPendingResourcesAsync(dbContext, options, cancellationToken)
				                          .ConfigureAwait(false);

			// Update the throttle timestamp.
			await UpdateGcStateAsync(dbContext, cancellationToken).ConfigureAwait(false);

			if (marked > 0 || swept > 0)
			{
				mLogger.LogInformation(
					"Resource cleanup completed: {Marked} marked, {Swept} swept, {Failed} failed",
					marked,
					swept,
					failed);
			}
			else
			{
				mLogger.LogDebug("Resource cleanup completed: nothing to do");
			}
		}
		finally
		{
			await scope.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Checks whether enough time has elapsed since the last GC run.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="options">The cleanup configuration.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if a new cycle should run; otherwise, <see langword="false"/>.</returns>
	private async Task<bool> ShouldRunAsync(
		LumaCoreDbContext      dbContext,
		ResourceCleanupOptions options,
		CancellationToken      cancellationToken)
	{
		ResourceGcStateEntity? state = await dbContext.ResourceGcState
			                               .FirstOrDefaultAsync(cancellationToken)
			                               .ConfigureAwait(false);

		if (state is null)
			return true;

		DateTime now = mTimeProvider.GetUtcNow().UtcDateTime;
		TimeSpan elapsed = now - state.LastRunAtUtc;

		return elapsed >= TimeSpan.FromMinutes(options.IntervalMinutes);
	}

	/// <summary>
	/// MARK phase: promotes orphaned Active resources past the grace period to PendingDeletion.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="options">The cleanup configuration.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of resources marked for deletion.</returns>
	private async Task<int> MarkOrphanedResourcesAsync(
		LumaCoreDbContext      dbContext,
		ResourceCleanupOptions options,
		CancellationToken      cancellationToken)
	{
		DateTime gracePeriodCutoff = mTimeProvider.GetUtcNow()
			.UtcDateTime
			.AddMinutes(-options.GracePeriodMinutes);

		// Content hashes that already have a PendingDeletion row — marking another row with the
		// same hash would violate the composite unique index (ContentHash, DeletionState).
		IQueryable<string> hashesWithPending = dbContext.Resources
			.Where(r => r.DeletionState == ResourceDeletionState.PendingDeletion)
			.Select(r => r.ContentHash);

		int marked = await dbContext.Resources
			             .Where(r => r.DeletionState == ResourceDeletionState.Active)
			             .Where(r => r.CreatedAtUtc < gracePeriodCutoff)
			             .Where(r => !dbContext.ResourceReferences.Any(rr => rr.ResourceId == r.Id))
			             .Where(r => !hashesWithPending.Contains(r.ContentHash))
			             .ExecuteUpdateAsync(
				             s => s.SetProperty(r => r.DeletionState, ResourceDeletionState.PendingDeletion),
				             cancellationToken)
			             .ConfigureAwait(false);

		return marked;
	}

	/// <summary>
	/// SWEEP phase: deletes physical files and then database rows for PendingDeletion resources.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="options">The cleanup configuration.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A tuple of (swept, failed) counts.</returns>
	private async Task<(int Swept, int Failed)> SweepPendingResourcesAsync(
		LumaCoreDbContext      dbContext,
		ResourceCleanupOptions options,
		CancellationToken      cancellationToken)
	{
		List<ResourceEntity> batch = await dbContext.Resources
			                             .Where(r => r.DeletionState == ResourceDeletionState.PendingDeletion)
			                             .OrderBy(r => r.Id)
			                             .Take(options.SweepBatchSize)
			                             .ToListAsync(cancellationToken)
			                             .ConfigureAwait(false);

		int swept = 0;
		int failed = 0;

		foreach (ResourceEntity resource in batch)
		{
			try
			{
				// File-first: delete the physical file before the DB row.
				// If file deletion fails, the DB row remains as recovery metadata.
				await mStore.DeleteAsync(resource.StoragePath, cancellationToken).ConfigureAwait(false);

				dbContext.Resources.Remove(resource);
				await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

				swept++;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (DbUpdateConcurrencyException ex) when (IsBenignSweepRaceShape(ex, resource))
			{
				// Shape check passed: every conflicting EF entry is the Deleted ResourceEntity we
				// were about to remove. Now we still need to distinguish the two reasons EF can
				// raise this exception for a single-row delete:
				//
				//   (a) The row is gone — another node's sweep already deleted it between our
				//       SELECT and our DELETE. File was deleted file-first (idempotent), row is
				//       gone, goal of this iteration reached → count as swept, log Debug.
				//
				//   (b) The row still exists but a concurrency-token check failed (only possible
				//       once ResourceEntity gains a RowVersion / IsConcurrencyToken mapping; today
				//       that path can't fire). Then a concurrent UPDATE has changed the row
				//       underneath us — we already deleted the file (file-first), so the entity is
				//       in an inconsistent state. That's a real failure → log Warning, count as
				//       failed, let the next cycle retry.
				//
				// Verifying via a fresh AsNoTracking() lookup keeps the handler correct under both
				// schemas, with the extra round-trip only paid on the (rare) race event itself.
				bool stillExists = await dbContext.Resources
					                   .AsNoTracking()
					                   .AnyAsync(r => r.Id == resource.Id, cancellationToken)
					                   .ConfigureAwait(false);

				// Detach in either branch so the next SaveChangesAsync() in this cycle doesn't
				// try to re-delete this entity.
				dbContext.Entry(resource).State = EntityState.Detached;

				if (stillExists)
				{
					mLogger.LogWarning(
						ex,
						"Resource sweep incomplete for {ResourceId} (path: {StoragePath}); concurrent update detected, will retry next cycle",
						resource.Id.Value,
						resource.StoragePath);

					failed++;
				}
				else
				{
					mLogger.LogDebug(
						"Resource {ResourceId} (path: {StoragePath}) was already removed by a concurrent sweep; counting as swept",
						resource.Id.Value,
						resource.StoragePath);

					swept++;
				}
			}
			catch (Exception ex)
			{
				// Non-fatal per resource: log and continue with the next one. Note that the file may have
				// already been deleted by the time the exception fires (file-first ordering). The DB row
				// remains in PendingDeletion state and will be retried on the next sweep — DeleteAsync() is
				// idempotent so a missing file is handled cleanly.
				mLogger.LogWarning(
					ex,
					"Resource sweep incomplete for {ResourceId} (path: {StoragePath}); will retry next cycle",
					resource.Id.Value,
					resource.StoragePath);

				failed++;

				// Detach the failed entity so the next SaveChangesAsync() doesn't retry it.
				dbContext.Entry(resource).State = EntityState.Detached;
			}
		}

		return (swept, failed);
	}

	/// <summary>
	/// Returns <see langword="true"/> when the <i>shape</i> of a <see cref="DbUpdateConcurrencyException"/>
	/// raised by the SWEEP <c>SaveChangesAsync()</c> matches the single-row delete this iteration
	/// attempted — i.e., every conflicting EF entry is the <see cref="EntityState.Deleted"/>
	/// <see cref="ResourceEntity"/> referenced by <paramref name="expected"/>.
	/// </summary>
	/// <param name="ex">The concurrency exception thrown by EF Core.</param>
	/// <param name="expected">The resource entity the current iteration is trying to delete.</param>
	/// <returns>
	/// <see langword="true"/> when every conflicting entry is the expected delete of
	/// <paramref name="expected"/>; <see langword="false"/> otherwise (in which case the caller
	/// treats the exception as a real failure and logs a Warning).
	/// </returns>
	/// <remarks>
	/// This is a <b>shape</b> check only — it filters out concurrency conflicts caused by
	/// <i>other</i> changes (e.g., a future UPDATE in the same try-block, a different entity type,
	/// or a multi-row delete). EF reports every conflicting entry on
	/// <see cref="DbUpdateException.Entries"/>; we admit the recovery path only when that list
	/// contains exactly our expected delete and nothing else.
	/// <para>
	/// The catch block then performs an additional <i>existence</i> check (<c>AnyAsync()</c>) to
	/// distinguish the two reasons EF can raise this exception for a single-row delete: the row
	/// is actually gone (benign multi-node sweep race → swept), or the row still exists and a
	/// concurrency-token check failed (concurrent UPDATE → real failure). This keeps the handler
	/// correct regardless of whether <see cref="ResourceEntity"/> ever gains a concurrency token.
	/// </para>
	/// </remarks>
	internal static bool IsBenignSweepRaceShape(DbUpdateConcurrencyException ex, ResourceEntity expected)
	{
		IReadOnlyList<EntityEntry> entries = ex.Entries;
		if (entries.Count == 0)
			return false;

		foreach (EntityEntry entry in entries)
		{
			if (entry.State != EntityState.Deleted)
				return false;

			if (!ReferenceEquals(entry.Entity, expected))
				return false;
		}

		return true;
	}

	/// <summary>
	/// Upserts the <see cref="ResourceGcStateEntity"/> singleton row with the current timestamp.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// The singleton is keyed on <c>Id = 1</c>. When two <see cref="ResourceCleanupService"/> instances
	/// (e.g., on different nodes) execute their very first cycle concurrently, both observe a missing
	/// row and both attempt to <c>INSERT</c> with the same primary key — the second <c>SaveChanges</c>
	/// then throws <see cref="DbUpdateException"/> on the PK conflict. Subsequent cycles never hit
	/// this race because the row exists and the code takes the <c>UPDATE</c> path. We classify the
	/// conflict by clearing the change tracker, re-fetching the winning row, and applying the
	/// timestamp update, so the cleanup cycle reports success instead of a noisy warning.
	/// </remarks>
	private async Task UpdateGcStateAsync(LumaCoreDbContext dbContext, CancellationToken cancellationToken)
	{
		DateTime now = mTimeProvider.GetUtcNow().UtcDateTime;

		ResourceGcStateEntity? state = await dbContext.ResourceGcState
			                               .FirstOrDefaultAsync(cancellationToken)
			                               .ConfigureAwait(false);

		if (state is null)
		{
			dbContext.ResourceGcState.Add(new ResourceGcStateEntity { Id = 1, LastRunAtUtc = now });

			try
			{
				await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (DbUpdateException insertEx)
			{
				// The singleton has no FK or non-PK uniqueness constraints, so a DbUpdateException here
				// is almost certainly the documented PK race (another cleanup instance inserted the row
				// first). Detach our pending insert, re-fetch the winning row, and apply the timestamp
				// update instead.
				//
				// Defensive fallback: if the row is somehow still missing after the conflict (e.g. the
				// failure was actually transient connectivity rather than a PK race), we rethrow the
				// original DbUpdateException as the inner cause so the outer cycle handler can log a
				// faithful diagnostic instead of a misleading "sequence contains no elements".
				dbContext.ChangeTracker.Clear();
				state = await dbContext.ResourceGcState
					        .FirstOrDefaultAsync(cancellationToken)
					        .ConfigureAwait(false);

				if (state is null)
				{
					throw new InvalidOperationException(
						"ResourceGcState upsert failed and the singleton row is still missing after the " +
						"conflict-recovery re-query. The original DbUpdateException is preserved as the " +
						"inner exception.",
						insertEx);
				}

				state.LastRunAtUtc = now;
				await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			}

			return;
		}

		state.LastRunAtUtc = now;
		await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
	}
}
