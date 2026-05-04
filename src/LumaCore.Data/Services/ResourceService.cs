// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Cryptography;

using LumaCore.Core.IO;
using LumaCore.Data.Entities;
using LumaCore.Data.Queries;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumaCore.Data.Services;

/// <summary>
/// Default implementation of <see cref="IResourceService"/> that coordinates between
/// <see cref="LumaCoreDbContext"/> and <see cref="IResourceStore"/> for resource lifecycle management.
/// </summary>
/// <remarks>
///     <para>
///         <b>Upload algorithm:</b>
///     </para>
///     <list type="number">
///         <item>
///             <description>Buffer the stream into a <see cref="MemoryBlockStream"/> and compute the SHA-256 hash.</description>
///         </item>
///         <item>
///             <description>Query for an existing <see cref="ResourceDeletionState.Active"/> resource with the same hash.</description>
///         </item>
///         <item>
///             <description>
///             If found (dedup hit): attach a new <see cref="ResourceReferenceEntity"/> to the existing
///             resource, then re-read its <see cref="ResourceEntity.DeletionState"/> to confirm the
///             garbage collector did not promote it to <see cref="ResourceDeletionState.PendingDeletion"/>
///             between lookup and attach. On a detected race the orphan reference is removed and
///             the dedup decision is retried; otherwise no file I/O is performed.
///             </description>
///         </item>
///         <item>
///             <description>
///             If not found: open a transaction, generate a GUID-based <see cref="ResourceEntity.StoragePath"/>,
///             persist the file, insert the resource row, attach the reference, commit.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>Conflict handling:</b> A <see cref="DbUpdateException"/> on the resource insert is classified by
///     re-querying the database (provider-agnostic, no index-name string matching):
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             A row with the same <see cref="ResourceEntity.StoragePath"/> indicates a GUID collision —
///             statistically impossible without a broken RNG. The orphan-file cleanup is <b>skipped</b>
///             (the file at that path belongs to the foreign row, not to us) and an
///             <see cref="InvalidOperationException"/> is thrown so the operator can investigate.
///             </description>
///         </item>
///         <item>
///             <description>
///             A concurrently inserted Active resource with the same <see cref="ResourceEntity.ContentHash"/>
///             converts the operation to a dedup hit. The orphan file (whose path we own and the foreign
///             row does not) is deleted.
///             </description>
///         </item>
///         <item>
///             <description>
///             Any other failure (e.g. a <see cref="ResourceReferenceEntity.PublicId"/> collision raised
///             from the reference attach, FK violation, connection error) rethrows the original exception
///             after deleting the orphan file.
///             </description>
///         </item>
///     </list>
///     <para>
///     <b>GUID uniqueness:</b> Both <see cref="ResourceEntity.StoragePath"/> and
///     <see cref="ResourceReferenceEntity.PublicId"/> are <see cref="Guid.NewGuid"/> values (122 random
///     bits, ~5.3 × 10^36 possible values). The implementation does <b>not</b> retry on collision: a real
///     collision indicates a compromised RNG and warrants operator attention, not a silent retry.
///     </para>
/// </remarks>
public sealed class ResourceService : IResourceService
{
	/// <summary>
	/// Maximum number of dedup attempts before falling through to a fresh upload. A second pass is enough
	/// to cover the documented MARK-race window: either another concurrent upload has already inserted a
	/// fresh <see cref="ResourceDeletionState.Active"/> row for the same hash (dedup succeeds), or none
	/// exists and we proceed with a new upload. Two attempts keep the loop bounded under pathological
	/// conditions; see <see cref="UploadAsync"/> for the full reasoning.
	/// </summary>
	private const int MaxDedupAttempts = 2;

	private readonly LumaCoreDbContext        mDbContext;
	private readonly IResourceStore           mStore;
	private readonly IStreamBufferPool        mStreamBufferPool;
	private readonly TimeProvider             mTimeProvider;
	private readonly ILogger<ResourceService> mLogger;
	private readonly bool                     mPreferCompiledHotPathQueries;

	/// <summary>
	/// Initializes a new instance of the <see cref="ResourceService"/> class.
	/// </summary>
	/// <param name="dbContext">The database context for resource and reference entities.</param>
	/// <param name="store">The filesystem store for persisting and reading resource files.</param>
	/// <param name="streamBufferPool">The buffer pool for managing memory blocks used during stream operations.</param>
	/// <param name="databaseOptions">
	/// The database options used to opt into pre-compiled hot-path queries via
	/// <see cref="DatabaseOptions.PreferCompiledHotPathQueries"/>.
	/// </param>
	/// <param name="timeProvider">
	/// The time provider used as the fallback clock for upload timestamps when the caller does not supply one.
	/// </param>
	/// <param name="logger">The logger for diagnostic output.</param>
	public ResourceService(
		LumaCoreDbContext         dbContext,
		IResourceStore            store,
		IStreamBufferPool         streamBufferPool,
		IOptions<DatabaseOptions> databaseOptions,
		TimeProvider              timeProvider,
		ILogger<ResourceService>  logger)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(streamBufferPool);
		ArgumentNullException.ThrowIfNull(databaseOptions);
		ArgumentNullException.ThrowIfNull(timeProvider);
		ArgumentNullException.ThrowIfNull(logger);
		mDbContext = dbContext;
		mStore = store;
		mStreamBufferPool = streamBufferPool;
		mTimeProvider = timeProvider;
		mLogger = logger;
		mPreferCompiledHotPathQueries = databaseOptions.Value.PreferCompiledHotPathQueries;
	}

	/// <inheritdoc/>
	public async Task<ResourceUploadResult> UploadAsync(
		Stream            content,
		ResourceOwnerKind ownerKind,
		ResourceOwnerId   ownerId,
		string            contentType,
		ParticipantId?    createdByParticipantId,
		DateTime?         utcNow            = null,
		string?           originalFileName  = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
		ValidateOriginalFileName(originalFileName);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		// Buffer the entire stream so we can hash it and replay it for storage.
		await using MemoryBlockStream buffer = mStreamBufferPool.CreateMemoryBlockStream();
		await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
		long sizeBytes = buffer.Length;

		// Compute SHA-256 content hash (lowercase hex, 64 characters).
		buffer.Position = 0;
		byte[] hashBytes = await SHA256.HashDataAsync(buffer, cancellationToken).ConfigureAwait(false);
		string contentHash = Convert.ToHexStringLower(hashBytes);

		// Detect an ambient transaction once, up front: it gates both the per-attempt pessimistic
		// lock in the dedup loop below AND the savepoint-vs-own-transaction branch in the new-upload
		// path further down.
		IDbContextTransaction? ambientTransaction = mDbContext.Database.CurrentTransaction;
		bool hasAmbientTransaction = ambientTransaction is not null;

		// Attempt dedup: find an existing Active resource with the same content hash.
		//
		// MARK-race hardening — TWO defence lines, see also (A) inside the loop:
		//
		// (A) Pessimistic write-lock on the dedup target (ambient path only). Required because
		//     under an ambient transaction our reference INSERT stays uncommitted in the caller's
		//     outer transaction, and MARK runs on a separate DbContext / connection — under
		//     READ COMMITTED it cannot observe our uncommitted reference, so the post-attach
		//     revalidation (B) cannot detect this race here. The lock forces MARK's UPDATE to
		//     wait until our outer commit; once it acquires the lock it re-evaluates the
		//     predicate, observes our committed reference, and skips the row. See the detailed
		//     comment at the lock site below.
		//
		// (B) Post-attach revalidation. The standalone (own-transaction) path's only defence:
		//     between the SELECT below and the reference INSERT in
		// AttachReferenceAsync(), ResourceCleanupService.MarkOrphanedResourcesAsync() may
		// promote this very row from Active to PendingDeletion (its MARK predicate is "no
		// references AND older than grace AND no PendingDeletion sibling"). If we proceeded
		// blindly, the next SWEEP would delete the file and cascade-delete the reference we just
		// attached — a silent data loss for the caller, who already received a PublicId.
		//
		// Two observations make a simple post-attach revalidation sufficient:
		//
		//   1. Once our reference INSERT is committed, the MARK predicate's "no references" clause
		//      is false, so MARK cannot promote this row. The race window is therefore strictly
		//      "MARK promoted between our SELECT and our INSERT becoming visible to MARK".
		//   2. We only need to detect that window after the fact: re-read DeletionState with
		//      AsNoTracking() so the change tracker does NOT short-circuit the query and return the
		//      stale cached entity from step 1. With the project-wide default isolation level
		//      (READ COMMITTED on every supported provider) this guarantees we observe the latest
		//      committed DeletionState. NOTE: under stricter isolation (REPEATABLE READ / SNAPSHOT)
		//      the SELECT would still return the transaction-start snapshot and the race would
		//      remain undetectable from inside the transaction — if we ever raise the default we
		//      must escalate to an out-of-band check via a fresh DbContext.
		//      If DeletionState is still Active we have won the race; if it has changed, MARK got
		//      there first and we must roll back the orphaned reference.
		//
		// On a detected race we delete the just-attached reference (the FK is still intact because
		// SWEEP has not run yet) and retry. Two passes are sufficient: either a concurrent Upload
		// has inserted a fresh Active row for the same hash (we dedup against it on pass 2), or no
		// Active row exists (we fall through to the new-upload path). MaxDedupAttempts caps the
		// loop defensively against pathological scheduling.
		for (int dedupAttempt = 1; dedupAttempt <= MaxDedupAttempts; dedupAttempt++)
		{
			ResourceEntity? existing;
			if (mPreferCompiledHotPathQueries)
			{
				// EF Core compiled queries cannot accept a CancellationToken. We honour cancellation as
				// best-effort by checking immediately before the call: this minimises the cancellation
				// window to the pure DB roundtrip, which is exactly what the hot-path opt-in trades away.
				cancellationToken.ThrowIfCancellationRequested();
				existing = await ResourceQueries
					           .GetActiveByContentHash(mDbContext, contentHash)
					           .ConfigureAwait(false);
			}
			else
			{
				existing = await mDbContext.Resources
					           .FirstOrDefaultAsync(
						           r => r.ContentHash == contentHash && r.DeletionState == ResourceDeletionState.Active,
						           cancellationToken)
					           .ConfigureAwait(false);
			}

			if (existing is null)
				break;

			// AMBIENT-TRANSACTION HARDENING — pessimistic write lock on the dedup target. The
			// hardening is identical here and in the hash-race-winner recovery path further below,
			// so both sites share TryAcquirePessimisticDedupLockAsync(). See the helper's XMLDoc for
			// the full rationale (race window, provider-portable locking strategy, detach ordering).
			if (hasAmbientTransaction)
			{
				bool acquired = await TryAcquirePessimisticDedupLockAsync(
						                existing,
						                contentHash,
						                dedupAttempt,
						                cancellationToken)
					                .ConfigureAwait(false);

				if (!acquired)
					continue;
			}

			ResourceReferenceEntity dedupReference = await AttachReferenceAsync(
					                                         existing.Id,
					                                         ownerKind,
					                                         ownerId,
					                                         contentType,
					                                         originalFileName,
					                                         effectiveUtcNow,
					                                         cancellationToken)
				                                         .ConfigureAwait(false);

			ResourceDeletionState currentState;
			if (mPreferCompiledHotPathQueries)
			{
				// Compiled queries cannot accept a CancellationToken — see comment on the dedup SELECT
				// above. A null result means the row was deleted concurrently; we treat it as not-Active
				// so the retry path runs and rolls back our just-attached reference.
				cancellationToken.ThrowIfCancellationRequested();
				ResourceDeletionState? state = await ResourceQueries
					                               .GetDeletionStateById(mDbContext, existing.Id)
					                               .ConfigureAwait(false);
				currentState = state ?? ResourceDeletionState.PendingDeletion;
			}
			else
			{
				// FirstOrDefaultAsync() (not FirstAsync()): if the row was hard-deleted between our
				// dedup SELECT and this re-read — possible when MARK promotes the row and SWEEP
				// then cascades it away in the same GC cycle, taking our just-attached reference
				// with it — the projection would otherwise throw InvalidOperationException and
				// surface a misleading error to the caller. Treat the missing row identically to
				// the compiled-query path (see ResourceQueries.GetDeletionStateById): map it to
				// PendingDeletion so the retry branch below detaches and re-runs.
				ResourceDeletionState? state = await mDbContext.Resources
					                               .AsNoTracking()
					                               .Where(r => r.Id == existing.Id)
					                               .Select(r => (ResourceDeletionState?)r.DeletionState)
					                               .FirstOrDefaultAsync(cancellationToken)
					                               .ConfigureAwait(false);
				currentState = state ?? ResourceDeletionState.PendingDeletion;
			}

			if (currentState == ResourceDeletionState.Active)
			{
				mLogger.LogDebug(
					"Upload deduplicated: hash {ContentHash}, reusing resource {ResourceId}",
					contentHash,
					existing.Id.Value);

				return new ResourceUploadResult(
					dedupReference.PublicId,
					contentHash,
					sizeBytes,
					WasDeduplicated: true);
			}

			// MARK won the race. Roll back our attach in-band so the change tracker stays clean
			// and the caller never sees a transiently broken state. Cascade would also handle this
			// during SWEEP, but doing it explicitly here avoids producing a "ghost" PublicId that
			// the caller might cache before SWEEP runs.
			mLogger.LogInformation(
				"Dedup target {ResourceId} for hash {ContentHash} was promoted to {State} mid-upload " +
				"(attempt {Attempt}/{MaxAttempts}); detaching reference and retrying",
				existing.Id.Value,
				contentHash,
				currentState,
				dedupAttempt,
				MaxDedupAttempts);

			mDbContext.ResourceReferences.Remove(dedupReference);
			try
			{
				await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (DbUpdateConcurrencyException)
			{
				// The row was already gone — i.e., MARK promoted the resource AND SWEEP cascade-deleted
				// the reference in the same GC cycle (the very scenario the FirstOrDefaultAsync() above
				// guards against). Our intent was to remove the orphan; the cascade beat us to it.
				// Detach the now-stale tracked entity so the change tracker stays clean and the next
				// dedup attempt — or the fresh-upload fallthrough — runs against a pristine context.
				mDbContext.Entry(dedupReference).State = EntityState.Detached;
			}

			// Also detach the dedup target itself: if SWEEP hard-deleted it, leaving the (now stale)
			// tracked instance in the IdentityMap would collide with a fresh ResourceEntity Insert
			// when the underlying store reuses the freed primary-key value (SQLite ROWID, sequence
			// recycling, etc.). Detaching is safe regardless of whether the row still exists, because
			// from this point on the loop either retries with a brand-new SELECT or falls through to
			// the new-upload path — both of which materialize their own tracked instance.
			//
			// Skip on the ambient path: TryAcquirePessimisticDedupLockAsync() already detached `existing`
			// (EF Core forbids ExecuteUpdate on a table with tracked entities of the same type), so the
			// IdentityMap is already clean. A redundant detach would be a no-op but reads as if the
			// caller still believes the entity to be tracked.
			if (!hasAmbientTransaction)
			{
				mDbContext.Entry(existing).State = EntityState.Detached;
			}
		}

		// No existing resource — persist a new file and insert a resource row inside a transaction.
		// The transaction guarantees that file + resource row + reference row are all-or-nothing from
		// the database's perspective; on failure we also clean up the orphaned file.
		//
		// Ambient transaction handling (ambientTransaction / hasAmbientTransaction were captured
		// above the dedup loop so the loop's pessimistic lock can use them): if the caller already
		// opened a transaction (e.g. SaveAvatarAsync() wraps DeleteReferences + UploadAsync() atomically),
		// we must NOT open a nested DbContextTransaction — EF Core forbids that. Instead we use
		// savepoints for per-attempt isolation, which Postgres requires anyway because a failed
		// statement aborts the whole transaction otherwise. The orphan file (written before the
		// savepoint) is cleaned up via a rollback compensation registered on the context, so it
		// survives both savepoint rollback AND outer transaction rollback.
		// 'N' format (32 hex chars, no dashes) matches savepointName and stays compact in logs/filesystems.
		string storagePath = Guid.NewGuid().ToString("N");

		// Register the orphan-file cleanup BEFORE writing the file. Two reasons:
		//
		//   1. FailFast misuse guard: RegisterRollbackCompensation triggers FailFast when the caller
		//      opened a conventional Database.BeginTransactionAsync() instead of going through
		//      BeginCompensatingTransactionAsync(). Registering FIRST ensures the process dies BEFORE
		//      any file lands on disk — no orphan to clean up.
		//
		//   2. Symmetric failure handling: if SaveAsync, CreateSavepointAsync, or BeginTransactionAsync
		//      throws, the compensation is already on the LIFO list. The local catches below either
		//      hand off to the outer compensating transaction (ambient path — the rollback will fire it)
		//      or unregister + delete inline (standalone path — no outer rollback will ever fire).
		//
		object compensationHandle = mDbContext.RegisterRollbackCompensation(ct => mStore.DeleteAsync(storagePath, ct));

		buffer.Position = 0;
		try
		{
			await mStore.SaveAsync(storagePath, buffer, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			// SaveAsync failed — no file persisted (LocalFileResourceStore cleans up its partial file
			// in its own finally). A foreign IResourceStore implementation might leave debris; we
			// therefore call DeleteAsync best-effort to keep both store contracts symmetric. Drop the
			// compensation regardless: there is no committed file to roll back.
			mDbContext.UnregisterRollbackCompensation(compensationHandle);
			try
			{
				await mStore.DeleteAsync(storagePath, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception cleanupEx)
			{
				mLogger.LogWarning(
					cleanupEx,
					"Best-effort cleanup after SaveAsync failure could not delete {StoragePath}",
					storagePath);
			}

			throw;
		}

		IDbContextTransaction? ownTransaction = null;
		string? savepointName = null;

		if (hasAmbientTransaction)
		{
			// Provider-agnostic savepoint name (alphanumeric, unique per upload). If
			// CreateSavepointAsync throws, the compensation we registered above will fire on the outer
			// compensating transaction's rollback — that is the entire point of the ambient path.
			savepointName = $"sp_resource_upload_{Guid.NewGuid():N}";
			await ambientTransaction!
				.CreateSavepointAsync(savepointName, cancellationToken)
				.ConfigureAwait(false);
		}
		else
		{
			// Standalone path: there is NO outer compensating transaction that could ever fire our
			// compensation. If BeginTransactionAsync itself throws (connection drop, provider quirk,
			// cancellation), the compensation would otherwise stay registered against the scoped
			// DbContext and a later unrelated BeginCompensatingTransactionAsync rollback would happily
			// delete this file. Unregister + best-effort delete inline before rethrowing.
			try
			{
				ownTransaction = await mDbContext.Database
					                 .BeginTransactionAsync(cancellationToken)
					                 .ConfigureAwait(false);
			}
			catch
			{
				mDbContext.UnregisterRollbackCompensation(compensationHandle);
				try
				{
					await mStore.DeleteAsync(storagePath, CancellationToken.None).ConfigureAwait(false);
				}
				catch (Exception cleanupEx)
				{
					mLogger.LogWarning(
						cleanupEx,
						"Best-effort cleanup after BeginTransactionAsync failure could not delete " +
						"{StoragePath}",
						storagePath);
				}

				throw;
			}
		}

		ResourceEntity? resource = null;
		try
		{
			resource = new ResourceEntity
			{
				ContentHash = contentHash,
				StoragePath = storagePath,
				SizeBytes = sizeBytes,
				CreatedAtUtc = effectiveUtcNow,
				CreatedByParticipantId = createdByParticipantId,
				DeletionState = ResourceDeletionState.Active
			};

			mDbContext.Resources.Add(resource);
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			ResourceReferenceEntity reference = await AttachReferenceAsync(
					                                    resource.Id,
					                                    ownerKind,
					                                    ownerId,
					                                    contentType,
					                                    originalFileName,
					                                    effectiveUtcNow,
					                                    cancellationToken)
				                                    .ConfigureAwait(false);

			if (ownTransaction is not null)
			{
				await ownTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
				// Own transaction succeeded — the file is now committed and must NOT be deleted on
				// any later compensation pass. Drop the cleanup we registered for it BEFORE
				// disposing the transaction: if DisposeAsync() throws (connection drop, provider
				// quirk, ...) we would otherwise leave a stale compensation registered against the
				// context that an unrelated later rollback would happily fire — deleting a file
				// whose row is already permanently committed.
				//
				// We deliberately do NOT wrap this in try/finally. UnregisterRollbackCompensation()
				// is a pure in-memory list operation and cannot fail under normal conditions; if
				// it ever did, the surrounding catch (Exception) block would take over — but that
				// catch already calls RollbackAsync() + DisposeAsync() on this same transaction. A
				// finally that pre-disposed the transaction here would cause the catch's RollbackAsync()
				// to throw ObjectDisposedException (caught + logged) and then its DisposeAsync() to
				// throw a *second* ObjectDisposedException (NOT caught), masking the original error.
				mDbContext.UnregisterRollbackCompensation(compensationHandle);
				await ownTransaction.DisposeAsync().ConfigureAwait(false);
			}
			else
			{
				await ambientTransaction!
					.ReleaseSavepointAsync(savepointName!, cancellationToken)
					.ConfigureAwait(false);
				// Savepoint released — file is now part of the outer transaction's fate. The
				// compensation stays registered so it fires if the outer transaction rolls back.
			}

			mLogger.LogDebug(
				"Upload completed: hash {ContentHash}, new resource {ResourceId}, path {StoragePath}",
				contentHash,
				resource.Id.Value,
				storagePath);

			return new ResourceUploadResult(
				reference.PublicId,
				contentHash,
				sizeBytes,
				WasDeduplicated: false);
		}
		catch (Exception ex) when (ex is not DbUpdateException)
		{
			// Non-DbUpdate failure (cancellation, timeout, provider-specific exception not wrapped as
			// DbUpdateException, ...). The standalone path has no outer ICompensatingTransaction that
			// could ever fire our compensation, so we MUST clean up the orphan file inline and unregister
			// the handle — otherwise an unrelated later rollback in this scope would delete the wrong
			// file. The ambient path can rely on the outer compensating transaction, but
			// unregistering+deleting here is still safe and keeps both paths symmetric.
			if (ownTransaction is not null)
			{
				try
				{
					await ownTransaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
				}
				catch (Exception rollbackEx)
				{
					mLogger.LogWarning(
						rollbackEx,
						"Rollback of own transaction failed during upload cleanup for {StoragePath}",
						storagePath);
				}

				await ownTransaction.DisposeAsync().ConfigureAwait(false);
			}
			else
			{
				try
				{
					await ambientTransaction!
						.RollbackToSavepointAsync(savepointName!, CancellationToken.None)
						.ConfigureAwait(false);
					await ambientTransaction
						.ReleaseSavepointAsync(savepointName!, CancellationToken.None)
						.ConfigureAwait(false);
				}
				catch (Exception rollbackEx)
				{
					mLogger.LogWarning(
						rollbackEx,
						"Savepoint rollback failed during upload cleanup for {StoragePath}",
						storagePath);
				}
			}

			if (resource is not null)
			{
				mDbContext.Entry(resource).State = EntityState.Detached;
			}

			mDbContext.UnregisterRollbackCompensation(compensationHandle);

			try
			{
				await mStore.DeleteAsync(storagePath, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception cleanupEx)
			{
				mLogger.LogWarning(
					cleanupEx,
					"Failed to delete orphan file {StoragePath} during upload cleanup",
					storagePath);
			}

			throw;
		}
		catch (DbUpdateException ex)
		{
			// Roll back this attempt and detach the failed entity BEFORE any classification re-query so
			// the change tracker is clean. Use CancellationToken.None for cleanup so we do not skip it
			// on a cancelled upload (rolling back is essential for a healthy DbContext).
			if (ownTransaction is not null)
			{
				await ownTransaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
				await ownTransaction.DisposeAsync().ConfigureAwait(false);
			}
			else
			{
				await ambientTransaction!
					.RollbackToSavepointAsync(savepointName!, CancellationToken.None)
					.ConfigureAwait(false);
				await ambientTransaction
					.ReleaseSavepointAsync(savepointName!, CancellationToken.None)
					.ConfigureAwait(false);
			}

			if (resource is not null)
			{
				mDbContext.Entry(resource).State = EntityState.Detached;
			}

			// Provider-agnostic conflict classification: re-query the database to see what is actually
			// there now. This avoids brittle index-name string matching on ex.InnerException (Postgres
			// reports the index name, SQLite reports "Table.Column", SQL Server differs again).
			//
			// CLASSIFICATION ORDER MATTERS:
			//
			//   1. StoragePath collision FIRST. If a foreign row already owns our just-generated GUID
			//      path, the file at that path belongs to the foreign row — NOT to us. Deleting it would
			//      silently destroy another resource's data on disk, leaving the foreign row pointing
			//      into the void. We therefore MUST classify this BEFORE any DeleteAsync() call. We also
			//      unregister the compensation: a later outer rollback firing it would commit the same
			//      data-loss bug. Statistically a Guid.NewGuid() collision is impossible (~5.3 × 10^36
			//      possible values); a real one indicates a compromised RNG and warrants a loud throw.
			//
			//   2. Hash-race winner SECOND. A concurrent upload of the same content has won the unique
			//      (ContentHash, DeletionState) race; our file at our unique GUID path is genuinely
			//      orphaned and must be deleted before we attach a dedup reference to the winner.
			//
			//   3. Anything else (PublicId collision raised from AttachReferenceAsync(), FK violation,
			//      connection error, ...) — our file is orphaned, delete it and rethrow.
			// CancellationToken.None for both classification queries: a cancellation between rollback
			// and classification would leave the rollback compensation registered while exiting the
			// method. Under the ambient path the outer compensating transaction would then fire the
			// compensation and delete the file — but in the StoragePath-collision branch that file
			// belongs to the FOREIGN row (see classification rationale above). Honouring cancellation
			// here would therefore reintroduce exactly the silent-data-loss bug the classification is
			// meant to prevent. Cleanup wins over cancellation, identical to the rollback branches.
			bool storagePathCollision = await mDbContext.Resources
				                            .AnyAsync(r => r.StoragePath == storagePath, CancellationToken.None)
				                            .ConfigureAwait(false);

			if (storagePathCollision)
			{
				// Foreign row owns this path — leave the file on disk untouched and drop the
				// compensation so a later outer rollback cannot delete it either. The orphan-file
				// cleanup below is also intentionally skipped.
				mDbContext.UnregisterRollbackCompensation(compensationHandle);

				mLogger.LogError(
					ex,
					"GUID collision on Resource.StoragePath {StoragePath} for hash {ContentHash}. " +
					"This is statistically impossible with Guid.NewGuid (~5.3e36 values) and indicates " +
					"a compromised random number generator. The orphan file was NOT deleted because " +
					"the colliding row owns it on disk",
					storagePath,
					contentHash);

				throw new InvalidOperationException(
					$"GUID collision on Resource.StoragePath '{storagePath}'. The random number " +
					"generator may be compromised. The file on disk was preserved because it belongs " +
					"to a foreign row. Investigate the host's entropy source.",
					ex);
			}

			// Path is ours — safe to delete. Wrap in try/catch (mirrors the non-DbUpdate cleanup
			// branch above): a failure to delete the orphan file must NOT mask the original
			// DbUpdateException — the caller needs to see the conflict so it can react (dedup-race
			// retry, error surface, ...). A leaked file is recoverable by the next SWEEP cycle; a
			// swallowed conflict is not.
			try
			{
				await mStore.DeleteAsync(storagePath, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception cleanupEx)
			{
				mLogger.LogWarning(
					cleanupEx,
					"Failed to delete orphan file {StoragePath} during upload conflict cleanup",
					storagePath);
			}

			mDbContext.UnregisterRollbackCompensation(compensationHandle);

			ResourceEntity? hashRaceWinner = await mDbContext.Resources
				                                 .FirstOrDefaultAsync(
					                                 r => r.ContentHash == contentHash &&
					                                      r.DeletionState == ResourceDeletionState.Active,
					                                 CancellationToken.None)
				                                 .ConfigureAwait(false);

			if (hashRaceWinner is not null)
			{
				mLogger.LogInformation(
					"Concurrent upload won the hash race for {ContentHash}; attaching as dedup reference",
					contentHash);

				// AMBIENT-TRANSACTION HARDENING — same race shape as the primary dedup path: under
				// an ambient transaction our attach-INSERT stays uncommitted in the caller's outer
				// transaction, so MARK on a separate DbContext could still promote the winner row
				// between this read and the outer commit. The pessimistic lock held until commit
				// forces MARK to wait and observe our reference. If the lock cannot be acquired the
				// winner was itself promoted between our read and the lock attempt — in that case
				// surface the original DbUpdateException (there is no retry budget left here) so the
				// caller can decide whether to retry the whole upload.
				if (hasAmbientTransaction)
				{
					bool acquired = await TryAcquirePessimisticDedupLockAsync(
							                existing: hashRaceWinner,
							                contentHash: contentHash,
							                dedupAttempt: 1,
							                cancellationToken: cancellationToken)
						                .ConfigureAwait(false);

					if (!acquired)
					{
						mLogger.LogError(
							ex,
							"Hash-race winner {ResourceId} for {ContentHash} was itself promoted " +
							"before the dedup lock could be acquired; surfacing the original failure",
							hashRaceWinner.Id.Value,
							contentHash);
						throw;
					}
				}

				ResourceReferenceEntity racedRef = await AttachReferenceAsync(
						                                   hashRaceWinner.Id,
						                                   ownerKind,
						                                   ownerId,
						                                   contentType,
						                                   originalFileName,
						                                   effectiveUtcNow,
						                                   cancellationToken)
					                                   .ConfigureAwait(false);

				// STANDALONE-PATH POST-ATTACH REVALIDATION — MARK runs on a separate DbContext and
				// can promote the winner row between the hashRaceWinner SELECT above and our
				// AttachReferenceAsync commit. Once our reference is committed the MARK predicate's
				// `!References.Any()` clause is false, so no future MARK can promote the row — but
				// the race window is exactly "MARK promoted between our SELECT and our commit
				// becoming visible to MARK". In that window MARK marks the row PendingDeletion and
				// the same cycle's SWEEP cascade-deletes both the row and our fresh reference,
				// leaving the caller with a ReferencePublicId that points at nothing. Under an
				// ambient transaction the pessimistic lock above prevents this; standalone needs an
				// explicit post-attach re-read against the just-committed state.
				//
				// We only need to detect it after the fact: AsNoTracking() forces a round-trip to
				// the DB instead of returning the stale tracked entity. With READ COMMITTED (the
				// default on every supported provider) this observes the latest committed state.
				// If DeletionState is anything other than Active, MARK won the race: we delete our
				// now-orphaned reference (the cascade has not necessarily run yet because MARK and
				// SWEEP can be in different cycles — detecting and removing it in-band beats
				// waiting for cascade) and surface the original DbUpdateException.
				if (!hasAmbientTransaction)
				{
					ResourceDeletionState? currentState = await mDbContext.Resources
						                                      .AsNoTracking()
						                                      .Where(r => r.Id == hashRaceWinner.Id)
						                                      .Select(r => (ResourceDeletionState?)r.DeletionState)
						                                      .FirstOrDefaultAsync(cancellationToken)
						                                      .ConfigureAwait(false);

					if (currentState != ResourceDeletionState.Active)
					{
						mLogger.LogError(
							ex,
							"Hash-race winner {ResourceId} for {ContentHash} was promoted to {State} " +
							"between the recovery read and the reference attach; removing the orphaned " +
							"reference and surfacing the original failure",
							hashRaceWinner.Id.Value,
							contentHash,
							currentState?.ToString() ?? "HardDeleted");

						// Remove the just-committed reference so the caller never observes a
						// PublicId that points at a doomed resource. Use a fresh scope-agnostic
						// delete (ExecuteDelete) so it runs regardless of change-tracker state.
						// If cascade from SWEEP has already removed it (hashRaceWinner row gone),
						// ExecuteDelete returns 0 — that is also fine.
						await mDbContext.ResourceReferences
							.Where(rr => rr.Id == racedRef.Id)
							.ExecuteDeleteAsync(CancellationToken.None)
							.ConfigureAwait(false);

						mDbContext.Entry(racedRef).State = EntityState.Detached;

						throw;
					}
				}

				return new ResourceUploadResult(
					racedRef.PublicId,
					contentHash,
					sizeBytes,
					WasDeduplicated: true);
			}

			// Neither a known dedup race nor a StoragePath collision — surface the original failure
			// instead of swallowing it. The orphan file has already been cleaned up above.
			mLogger.LogError(
				ex,
				"Resource upload failed for hash {ContentHash} (path {StoragePath}) with an " +
				"unrecognised database error",
				contentHash,
				storagePath);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<ResourceDownloadInfo?> GetDownloadInfoAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default)
	{
		if (mPreferCompiledHotPathQueries)
		{
			// EF Core compiled queries cannot accept a CancellationToken. Best-effort cancellation:
			// check immediately before the call so the cancellation window is the pure DB roundtrip.
			cancellationToken.ThrowIfCancellationRequested();
			return await ResourceQueries.GetDownloadInfoByPublicId(mDbContext, publicId).ConfigureAwait(false);
		}

		ResourceDownloadInfo? result = await mDbContext.ResourceReferences
			                               .Where(rr => rr.PublicId == publicId)
			                               .Join(
				                               mDbContext.Resources,
				                               rr => rr.ResourceId,
				                               r => r.Id,
				                               (rr, r) => new ResourceDownloadInfo(
					                               r.StoragePath,
					                               rr.ContentType,
					                               rr.OriginalFileName,
					                               r.SizeBytes))
			                               .FirstOrDefaultAsync(cancellationToken)
			                               .ConfigureAwait(false);

		return result;
	}

	/// <inheritdoc/>
	public async Task<int> DeleteReferencesByOwnerAsync(
		ResourceOwnerKind ownerKind,
		ResourceOwnerId   ownerId,
		CancellationToken cancellationToken = default)
	{
		// Detach any references for this owner that the ChangeTracker still holds from earlier reads in
		// the same DbContext scope. ExecuteDeleteAsync bypasses the tracker, so without this step a
		// subsequent Add() of a fresh ResourceReferenceEntity can collide with the now-stale tracked
		// instance once the database reuses the same rowid (e.g. SQLite reusing the freed primary key).
		// Materialize via ToList() before mutating state to avoid modifying the tracker mid-enumeration.
		List<EntityEntry<ResourceReferenceEntity>> staleEntries = mDbContext.ChangeTracker
			.Entries<ResourceReferenceEntity>()
			.Where(e => e.Entity.OwnerKind == ownerKind && e.Entity.OwnerId == ownerId)
			.ToList();
		foreach (EntityEntry<ResourceReferenceEntity> entry in staleEntries)
		{
			entry.State = EntityState.Detached;
		}

		int deleted = await mDbContext.ResourceReferences
			              .Where(rr => rr.OwnerKind == ownerKind && rr.OwnerId == ownerId)
			              .ExecuteDeleteAsync(cancellationToken)
			              .ConfigureAwait(false);

		if (deleted > 0)
		{
			mLogger.LogDebug(
				"Deleted {Count} resource reference(s) for {OwnerKind} {OwnerId}",
				deleted,
				ownerKind,
				ownerId.Value);
		}

		return deleted;
	}

	/// <summary>
	/// Attempts to acquire a pessimistic row-level write lock on <paramref name="existing"/> for the
	/// duration of the caller's ambient transaction, so the MARK phase of
	/// <see cref="ResourceCleanupService"/> cannot promote the row between the caller's dedup-target
	/// check and the outer commit.
	/// </summary>
	/// <param name="existing">The tracked <see cref="ResourceEntity"/> that the caller intends to dedup against.</param>
	/// <param name="contentHash">The content hash, used only for diagnostic logging.</param>
	/// <param name="dedupAttempt">The attempt index in the caller's dedup loop, used for diagnostic logging.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> if the lock was acquired (<paramref name="existing"/> is still
	/// <see cref="ResourceDeletionState.Active"/> and now locked until commit);
	/// <see langword="false"/> if the row has already been promoted or hard-deleted by a concurrent
	/// cleanup — in which case the caller must NOT proceed with an attach against this row.
	/// </returns>
	/// <remarks>
	///     <para>
	///     Closes the residual race the post-attach revalidation cannot detect under an ambient
	///     transaction: the reference INSERT stays uncommitted until the caller's outer commit, so
	///     MARK on a separate <see cref="LumaCoreDbContext"/> (READ COMMITTED) could promote the row
	///     AFTER revalidation observed Active and BEFORE the outer commit lands — leading to either
	///     an FK-violation commit or (worse) a dangling reference whose file SWEEP later deletes.
	///     </para>
	///     <para>
	///     The provider-portable no-op <c>ExecuteUpdate()</c> (<c>SetProperty x =&gt; x</c>) takes a
	///     row-level write lock on every supported provider (Postgres/MySQL row-exclusive, SQL Server
	///     X lock, SQLite single-writer DB lock — degenerate but harmless). The lock is held until
	///     the outer transaction commits, forcing MARK's own UPDATE to wait; when MARK then acquires
	///     the lock and re-evaluates <c>!References.Any()</c> it observes the committed reference
	///     and skips the row.
	///     </para>
	///     <para>
	///     <paramref name="existing"/> is detached BEFORE the <c>ExecuteUpdate()</c> because EF Core
	///     warns when <c>ExecuteUpdate()</c> runs against a table with tracked entities of the same
	///     type (it cannot keep them in sync with the database). The caller only needs the id snapshot
	///     to attach a reference, so the detach is safe.
	///     </para>
	///     <para>
	///     Call this only on the ambient-transaction path. On the standalone path, the post-attach
	///     revalidation already observes MARK's UPDATE via the READ COMMITTED isolation level.
	///     </para>
	/// </remarks>
	private async Task<bool> TryAcquirePessimisticDedupLockAsync(
		ResourceEntity    existing,
		string            contentHash,
		int               dedupAttempt,
		CancellationToken cancellationToken)
	{
		ResourceId targetId = existing.Id;
		mDbContext.Entry(existing).State = EntityState.Detached;

		int locked = await mDbContext.Resources
			             .Where(r => r.Id == targetId &&
			                         r.DeletionState == ResourceDeletionState.Active)
			             .ExecuteUpdateAsync(
				             s => s.SetProperty(r => r.DeletionState, r => r.DeletionState),
				             cancellationToken)
			             .ConfigureAwait(false);

		if (locked == 0)
		{
			mLogger.LogInformation(
				"Dedup target {ResourceId} for hash {ContentHash} was promoted before the " +
				"pessimistic lock could be acquired (attempt {Attempt}/{MaxAttempts}); retrying",
				targetId.Value,
				contentHash,
				dedupAttempt,
				MaxDedupAttempts);
			return false;
		}

		return true;
	}

	/// <summary>
	/// Creates a new <see cref="ResourceReferenceEntity"/> and persists it. The <see cref="Guid.NewGuid"/>
	/// allocated for <see cref="ResourceReferenceEntity.PublicId"/> is treated as unique by construction
	/// (~5.3 × 10^36 possible values); a real collision indicates a compromised RNG and is surfaced as
	/// <see cref="InvalidOperationException"/> rather than silently retried.
	/// </summary>
	/// <param name="resourceId">The identifier of the resource to reference.</param>
	/// <param name="ownerKind">The kind of the owning entity.</param>
	/// <param name="ownerId">The polymorphic identifier of the owning entity.</param>
	/// <param name="contentType">The MIME content type for this reference.</param>
	/// <param name="originalFileName">The original file name, or <see langword="null"/>.</param>
	/// <param name="utcNow">The UTC creation timestamp.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The persisted <see cref="ResourceReferenceEntity"/>.</returns>
	/// <exception cref="InvalidOperationException">
	/// A <see cref="DbUpdateException"/> was raised by a uniqueness collision on
	/// <see cref="ResourceReferenceEntity.PublicId"/>. This is statistically impossible with
	/// <see cref="Guid.NewGuid"/> and indicates a compromised random number generator.
	/// </exception>
	/// <exception cref="DbUpdateException">
	/// The database rejected the insert for a reason other than a <see cref="ResourceReferenceEntity.PublicId"/>
	/// collision (e.g. FK violation, connection error). Caller is expected to classify and react.
	/// </exception>
	private async Task<ResourceReferenceEntity> AttachReferenceAsync(
		ResourceId        resourceId,
		ResourceOwnerKind ownerKind,
		ResourceOwnerId   ownerId,
		string            contentType,
		string?           originalFileName,
		DateTime          utcNow,
		CancellationToken cancellationToken)
	{
		var reference = new ResourceReferenceEntity
		{
			PublicId = Guid.NewGuid(),
			ResourceId = resourceId,
			OwnerKind = ownerKind,
			OwnerId = ownerId,
			ContentType = contentType,
			OriginalFileName = originalFileName,
			CreatedAtUtc = utcNow
		};

		mDbContext.ResourceReferences.Add(reference);

		try
		{
			await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			return reference;
		}
		catch (DbUpdateException ex)
		{
			// Detach so the change tracker stays clean regardless of how the caller reacts.
			mDbContext.Entry(reference).State = EntityState.Detached;

			// Provider-agnostic check: did the just-attempted PublicId already exist? If so, the only
			// possible cause is a Guid.NewGuid() collision — statistically impossible with ~5.3 × 10^36
			// possible values. Re-throw as InvalidOperationException with a clear hint instead of letting
			// the raw DbUpdateException bubble up.
			bool publicIdCollision = await mDbContext.ResourceReferences
				                         .AnyAsync(rr => rr.PublicId == reference.PublicId, cancellationToken)
				                         .ConfigureAwait(false);

			if (publicIdCollision)
			{
				mLogger.LogError(
					ex,
					"GUID collision on ResourceReference.PublicId {PublicId}. This is statistically " +
					"impossible with Guid.NewGuid (~5.3e36 values) and indicates a compromised random " +
					"number generator",
					reference.PublicId);

				throw new InvalidOperationException(
					$"GUID collision on ResourceReference.PublicId '{reference.PublicId}'. The random " +
					"number generator may be compromised. Investigate the host's entropy source.",
					ex);
			}

			// Anything else (FK violation, connection error, ...) — surface the original failure so the
			// UploadAsync() caller can classify it (hash race winner, unrecognised error, ...).
			throw;
		}
	}

	/// <summary>
	/// Validates an optional <c>originalFileName</c> against the contract documented on
	/// <see cref="IResourceService.UploadAsync"/>: non-empty, length-bounded, and free of path separators
	/// and NUL characters.
	/// </summary>
	/// <param name="originalFileName">The candidate file name, or <see langword="null"/> to skip validation.</param>
	/// <exception cref="ArgumentException">
	/// <paramref name="originalFileName"/> is non-<see langword="null"/> but empty/whitespace, exceeds
	/// <see cref="EntityLimits.ResourceOriginalFileNameMaxLength"/> characters, or contains a path
	/// separator (<c>/</c>, <c>\</c>) or NUL character.
	/// </exception>
	private static void ValidateOriginalFileName(string? originalFileName)
	{
		// Null is the documented "not available" sentinel — accept and skip the rest.
		if (originalFileName is null)
		{
			return;
		}

		// Whitespace-only is almost certainly a caller bug (a stripped UI string slipping through);
		// treat it the same as contentType to keep the parameter contracts symmetric.
		if (string.IsNullOrWhiteSpace(originalFileName))
		{
			throw new ArgumentException(
				"Original file name must not be empty or whitespace.",
				nameof(originalFileName));
		}

		if (originalFileName.Length > EntityLimits.ResourceOriginalFileNameMaxLength)
		{
			throw new ArgumentException(
				$"Original file name must not exceed {EntityLimits.ResourceOriginalFileNameMaxLength} " +
				$"characters (was {originalFileName.Length}).",
				nameof(originalFileName));
		}

		// Path separators and NUL are rejected because the value is descriptive metadata only — it is
		// never used to construct a filesystem path. Allowing them would invite confusion (does the
		// stored "original" name carry directory components?) and creates the kind of subtle attack
		// surface that the resource pipeline avoids by design (storage paths use freshly generated
		// GUIDs, not user input).
		if (originalFileName.AsSpan().IndexOfAny('/', '\\', '\0') >= 0)
		{
			throw new ArgumentException(
				"Original file name must not contain path separators ('/', '\\') or NUL characters.",
				nameof(originalFileName));
		}
	}

	/// <summary>
	/// Returns <paramref name="utcNow"/> when supplied, otherwise the current UTC timestamp from the configured
	/// <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="utcNow">An explicit UTC timestamp, or <see langword="null"/> to fall back to the service clock.</param>
	/// <returns>The effective UTC timestamp to use for persisted entities.</returns>
	private DateTime ResolveUtcNow(DateTime? utcNow) => utcNow ?? mTimeProvider.GetUtcNow().UtcDateTime;
}
