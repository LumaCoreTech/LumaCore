// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data;

public sealed partial class LumaCoreDbContext
{
	/// <summary>
	/// A transaction wrapper that triggers compensating actions registered on the owning
	/// <see cref="LumaCoreDbContext"/> when the transaction is rolled back (explicitly or via dispose
	/// without commit) and discards them on a successful commit.
	/// </summary>
	private sealed class CompensatingTransaction : ICompensatingTransaction
	{
		private readonly LumaCoreDbContext     mContext;
		private readonly IDbContextTransaction mInner;
		private          bool                  mCompleted;
		private          bool                  mCommitted;
		private          bool                  mDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="CompensatingTransaction"/> class.
		/// </summary>
		/// <param name="context">The owning context whose compensations will be processed.</param>
		/// <param name="inner">The underlying EF Core transaction.</param>
		public CompensatingTransaction(LumaCoreDbContext context, IDbContextTransaction inner)
		{
			mContext = context;
			mInner = inner;
		}

		/// <inheritdoc/>
		public async Task CommitAsync(CancellationToken cancellationToken = default)
		{
			// Cancellation policy: pre-flight check only. Once mInner.CommitAsync() is in flight, a
			// cancellation that races with a server-side commit can leave the database committed while
			// the client throws OperationCanceledException. Treating that as "commit failed" would cause
			// DisposeAsync() to run an implicit rollback and execute compensations against
			// already-permanent rows — silent data corruption (orphaned compensating side effects against
			// committed state). CancellationToken.None is therefore passed downstream.
			cancellationToken.ThrowIfCancellationRequested();

			await mInner.CommitAsync(CancellationToken.None).ConfigureAwait(false);

			// Bookkeeping below runs only after the commit await has succeeded. If mInner.CommitAsync()
			// throws (e.g. deferred constraint violation), mCompleted stays false and the pending
			// compensations are intentionally NOT cleared — the enclosing await using then runs
			// DisposeAsync(), which observes the un-completed state and triggers an implicit rollback
			// plus compensation execution. A failed commit is treated exactly like an implicit rollback.
			mContext.ClearRollbackCompensations();
			mCompleted = true;
			mCommitted = true;

			// Promote this wrapper into the "awaiting dispose" bucket so RegisterRollbackCompensation()
			// can detect a register-after-commit misuse (guard B on that method). The active-wrapper
			// counter is intentionally NOT decremented here — that still happens in DisposeAsync().
			// Splitting the two phases lets the misuse guard fire even though the underlying EF
			// transaction has already been cleared from Database.CurrentTransaction on commit (which
			// rules out guard A for this window).
			mContext.mCommittedCompensatingTransactionsAwaitingDispose++;
		}

		/// <inheritdoc/>
		public async Task RollbackAsync(CancellationToken cancellationToken = default)
		{
			// Guard: rejecting rollback-after-commit produces a clear diagnostic instead of letting
			// mInner.RollbackAsync() throw a provider-specific "transaction already completed" message.
			// Note: a second RollbackAsync() after a successful first rollback is intentionally NOT
			// guarded here — that path lets the underlying EF exception surface, which is acceptable
			// for an already-rolled-back transaction (the data is safe either way).
			if (mCommitted)
			{
				throw new InvalidOperationException(
					"Cannot rollback a CompensatingTransaction that has already been committed.");
			}

			// Mark completed up front so DisposeAsync()'s implicit-rollback branch is a guaranteed no-op
			// regardless of which step below throws. Without this guard a failure in mInner.RollbackAsync()
			// (or — defensively — in ExecuteRollbackCompensationsAsync()) would leave mCompleted == false
			// and DisposeAsync() would re-run the compensations a second time.
			mCompleted = true;

			// Use CancellationToken.None for both the DB rollback and the compensations: cleanup must
			// always run, even if the caller's token is already cancelled, otherwise we leak files and
			// poison the DbContext. Compensations run in a finally so that a failing inner rollback
			// still triggers cleanup before the exception propagates — orphan files would otherwise
			// leak. The inner-rollback exception is preserved as the observable failure for callers
			// that explicitly await RollbackAsync (e.g. for diagnostic logging).
			try
			{
				await mInner.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
			}
			finally
			{
				await mContext.ExecuteRollbackCompensationsAsync(CancellationToken.None).ConfigureAwait(false);
			}
		}

		/// <inheritdoc/>
		public async ValueTask DisposeAsync()
		{
			// Idempotency guard. IAsyncDisposable.DisposeAsync() must be a no-op on subsequent calls and
			// must not throw. Without this guard the finally block below would re-decrement the counters
			// on a second call and drive them negative.
			if (mDisposed)
				return;

			mDisposed = true;

			try
			{
				if (!mCompleted)
				{
					// Implicit rollback: caller exited the using block (e.g. via exception) without
					// calling CommitAsync(). Roll back the DB FIRST so any compensation that re-queries
					// the context observes the rolled-back state, then run the compensations, then
					// dispose. Wrap the rollback so a failing rollback cannot prevent compensations
					// from running (orphan files would otherwise leak).
					try
					{
						await mInner.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						mContext.ResolveLogger()
							?.LogWarning(
								ex,
								"Implicit rollback in CompensatingTransaction.DisposeAsync() failed; running " +
								"compensations anyway");
					}

					await mContext.ExecuteRollbackCompensationsAsync(CancellationToken.None).ConfigureAwait(false);
				}

				await mInner.DisposeAsync().ConfigureAwait(false);
			}
			finally
			{
				// Always decrement the active-wrapper counter, even if the rollback / disposal above
				// threw; otherwise the counter would drift and a later RegisterRollbackCompensation()
				// call could miss a real misuse (false negative) or fire a false positive against a
				// fresh wrapper. The mDisposed guard at the top of this method ensures this only runs
				// once per wrapper instance, regardless of how many times DisposeAsync() is called.
				mContext.mActiveCompensatingTransactions--;

				// If this wrapper reached Dispose after a successful Commit, also retire its entry
				// from the "awaiting dispose" bucket. Gated on mCommitted (not mCompleted) so an
				// explicit Rollback — which also sets mCompleted — does NOT touch this counter.
				if (mCommitted)
				{
					mContext.mCommittedCompensatingTransactionsAwaitingDispose--;
				}
			}
		}
	}
}
