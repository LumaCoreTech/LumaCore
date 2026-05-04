// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data;

public sealed partial class LumaCoreDbContext
{
	/// <summary>
	/// Compensating actions to execute if the current logical unit of work is rolled back, keyed by an
	/// opaque handle so individual entries can be cancelled when the side effect they undo becomes
	/// permanent (e.g. a nested own-transaction commits successfully).
	/// </summary>
	/// <remarks>
	/// Owned by the context (and therefore tied to the scoped lifetime). Mutated only on the thread that
	/// owns the context — concurrent use of a single <see cref="LumaCoreDbContext"/> instance is not
	/// supported, so no synchronization is required. A <see cref="List{T}"/> of (handle, action) pairs
	/// preserves registration order (LIFO execution on rollback) while still allowing O(n) removal by
	/// handle, which is acceptable given the very small expected count per unit of work.
	/// </remarks>
	private readonly List<(object Handle, Func<CancellationToken, Task> Action)> mRollbackCompensations = new();

	/// <summary>
	/// Number of <see cref="CompensatingTransaction"/> wrappers currently alive on this context.
	/// Incremented by <see cref="BeginCompensatingTransactionAsync"/> and decremented when each
	/// wrapper is disposed. Used by <see cref="RegisterRollbackCompensation"/> to detect callers that
	/// registered compensations against a transaction that was started the conventional way (e.g.
	/// <see cref="DatabaseFacade.BeginTransactionAsync(CancellationToken)"/>) instead of through the
	/// compensation-aware entry point — a misuse that would silently swallow registrations on commit
	/// and is therefore considered a fatal programming error.
	/// </summary>
	/// <remarks>
	/// In strict mode (no nesting allowed by <see cref="BeginCompensatingTransactionAsync"/>), the
	/// counter only ever reaches 0 or 1 in normal operation. A counter is used instead of a
	/// <see cref="bool"/> flag because it composes naturally with the parallel
	/// <see cref="mCommittedCompensatingTransactionsAwaitingDispose"/> bookkeeping and keeps the
	/// invariants symmetrical (begin increments, dispose decrements). <see cref="CompensatingTransaction.DisposeAsync"/>
	/// is itself idempotent (guarded by an internal <c>mDisposed</c> flag), so accidental double-dispose
	/// cannot drive this counter into negative territory — the decrement runs at most once per wrapper.
	/// </remarks>
	private int mActiveCompensatingTransactions;

	/// <summary>
	/// Test-only accessor for <see cref="mActiveCompensatingTransactions"/>: exposed to
	/// <c>LumaCore.Data.Tests</c> via <c>InternalsVisibleTo</c> so the compensation tests can directly
	/// assert on the counter invariant (incremented on begin, decremented on dispose, resilient against
	/// exceptions during rollback). Production code must continue to use the private field.
	/// </summary>
	internal int ActiveCompensatingTransactionCountForTests => mActiveCompensatingTransactions;

	/// <summary>
	/// Number of <see cref="CompensatingTransaction"/> wrappers on this context that have already
	/// had <see cref="ICompensatingTransaction.CommitAsync"/> called on them but have not yet been
	/// disposed. Used by <see cref="RegisterRollbackCompensation"/> to detect the narrow window
	/// between <c>CommitAsync</c> and the enclosing <c>await using</c> actually running the wrapper's
	/// <c>DisposeAsync</c>: in that window the pending-compensation list has already been cleared on
	/// commit, so a new registration would leak onto the next
	/// <see cref="BeginCompensatingTransactionAsync"/> call on the same scoped context and fire
	/// against an unrelated unit of work.
	/// </summary>
	/// <remarks>
	/// Kept separate from <see cref="mActiveCompensatingTransactions"/> (which counts wrappers that
	/// have been begun but not yet disposed) because the two lifecycles need to be tracked
	/// independently: a <c>Begin</c> increments the active counter, a <c>CommitAsync</c> promotes
	/// the wrapper into "awaiting dispose" without touching the active counter, and <c>DisposeAsync</c>
	/// decrements whichever counter the wrapper currently belongs to. This preserves the long-standing
	/// "Register before Begin is fine" pattern used by <c>ResourceService.UploadAsync</c>'s standalone
	/// path while still plugging the commit-but-not-yet-disposed misuse hole.
	/// </remarks>
	private int mCommittedCompensatingTransactionsAwaitingDispose;

	/// <summary>
	/// Test-only accessor for <see cref="mCommittedCompensatingTransactionsAwaitingDispose"/>: exposed
	/// to <c>LumaCore.Data.Tests</c> via <c>InternalsVisibleTo</c> so the compensation tests can
	/// directly assert on the commit-window invariant.
	/// </summary>
	internal int CommittedCompensatingTransactionsAwaitingDisposeCountForTests =>
		mCommittedCompensatingTransactionsAwaitingDispose;

	/// <summary>
	/// Registers a compensating action that will be executed if the current (or pending) transaction is
	/// rolled back via <see cref="BeginCompensatingTransactionAsync"/>.
	/// </summary>
	/// <param name="compensation">
	/// An asynchronous action that undoes a side effect performed outside the database (typically deleting
	/// a file written to disk before the corresponding row was committed). It receives a
	/// <see cref="CancellationToken"/> for cooperative cancellation; implementations should generally
	/// honour it but may choose to ignore it for critical cleanup.
	/// </param>
	/// <returns>
	/// An opaque handle that can be passed to <see cref="UnregisterRollbackCompensation"/> to cancel the
	/// registration once the side effect has become permanent (and therefore must not be undone).
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="compensation"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <para>
	///     Compensations are invoked <b>in reverse registration order</b> on rollback (LIFO), mirroring
	///     how nested resources are released. They are <b>discarded</b> on a successful commit.
	///     </para>
	///     <para>
	///     Calling this method without an active <see cref="ICompensatingTransaction"/> is supported: the
	///     registration will fire when the next compensating transaction wrapping this work rolls back.
	///     Callers that complete a side effect outside any compensating transaction <b>must</b> call
	///     <see cref="UnregisterRollbackCompensation"/> after the side effect becomes permanent, otherwise
	///     a later unrelated rollback would incorrectly undo it.
	///     </para>
	///     <para>
	///     <b>Misuse contract:</b> this method <b>aborts the process immediately</b> if the registration
	///     would otherwise be silently lost — specifically:
	///     <list type="bullet">
	///         <item>
	///         a database transaction is currently active that was <i>not</i> started via
	///         <see cref="BeginCompensatingTransactionAsync"/>, or
	///         </item>
	///         <item>
	///         the method is called after <see cref="ICompensatingTransaction.CommitAsync"/>
	///         has succeeded but before the wrapper has been disposed.
	///         </item>
	///     </list>
	///     In both cases the caller must restructure the code to register inside an
	///     <see cref="ICompensatingTransaction"/>.
	///     </para>
	/// </remarks>
	public object RegisterRollbackCompensation(Func<CancellationToken, Task> compensation)
	{
		ArgumentNullException.ThrowIfNull(compensation);

		// Misuse guard (A): a transaction is in flight, but it was started the conventional way (e.g.
		// Database.BeginTransactionAsync()) instead of through BeginCompensatingTransactionAsync(). The
		// underlying EF transaction has no awareness of our compensation list, so on commit/rollback
		// the registration would be silently leaked: the compensation either never runs (commit — leaks
		// the side effect) or runs against a future, unrelated CompensatingTransaction (rollback —
		// undoes a permanent side effect). Both outcomes corrupt data invisibly. FailFast loudly so
		// the misuse cannot ship.
		if (Database.CurrentTransaction is not null && mActiveCompensatingTransactions == 0)
		{
			FailFast.TerminateApplication(
				"RegisterRollbackCompensation was called while a database transaction is active that was " +
				"NOT started via LumaCoreDbContext.BeginCompensatingTransactionAsync. The registration would " +
				"be silently dropped on commit/rollback, leading to leaked side effects or incorrect undo of " +
				"permanent state. Wrap the unit of work in BeginCompensatingTransactionAsync (or remove the " +
				"manual transaction) before registering compensations.");
		}

		// Misuse guard (B): a CompensatingTransaction on this context has already been committed but
		// not yet disposed. CommitAsync() cleared the pending-compensations list, so a new registration
		// here would sit unattached until the next BeginCompensatingTransactionAsync() wraps it up —
		// and then fire on that unrelated wrapper's rollback, undoing permanent state from the current
		// logical work unit. Callers in this window must begin a new compensating transaction first.
		if (mCommittedCompensatingTransactionsAwaitingDispose > 0)
		{
			FailFast.TerminateApplication(
				"RegisterRollbackCompensation was called after CompensatingTransaction.CommitAsync " +
				"succeeded but before the wrapper was disposed. The registration would leak onto the " +
				"next BeginCompensatingTransactionAsync call and fire against an unrelated unit of " +
				"work. Begin a new compensating transaction before registering further compensations.");
		}

		object handle = new();
		mRollbackCompensations.Add((handle, compensation));
		return handle;
	}

	/// <summary>
	/// Cancels a previously registered rollback compensation. Safe to call with an unknown or stale
	/// handle (no-op).
	/// </summary>
	/// <param name="handle">
	/// The handle returned by <see cref="RegisterRollbackCompensation"/>. <see langword="null"/> is
	/// accepted and treated as a no-op so callers can pass a possibly-null field without an extra
	/// guard at every call site (mirrors the <see cref="IDisposable"/> tolerance pattern).
	/// </param>
	public void UnregisterRollbackCompensation(object? handle)
	{
		if (handle is null)
			return;

		for (int i = mRollbackCompensations.Count - 1; i >= 0; i--)
		{
			if (ReferenceEquals(mRollbackCompensations[i].Handle, handle))
			{
				mRollbackCompensations.RemoveAt(i);
				return;
			}
		}
	}

	/// <summary>
	/// Begins a database transaction whose <see cref="ICompensatingTransaction.CommitAsync"/> and
	/// <see cref="ICompensatingTransaction.RollbackAsync"/> also process compensating actions registered
	/// via <see cref="RegisterRollbackCompensation"/>.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A wrapper around the underlying <see cref="IDbContextTransaction"/>.</returns>
	/// <remarks>
	///     <para>
	///     Disposing the returned transaction without calling <see cref="ICompensatingTransaction.CommitAsync"/>
	///     is treated as an implicit rollback and triggers compensation execution.
	///     </para>
	///     <para>
	///     Compensation execution is best-effort: each registered action is awaited individually, and an
	///     action that throws is logged at <see cref="LogLevel.Warning"/> but does not prevent the
	///     remaining compensations from running. The original rollback reason (if any) is always
	///     rethrown to the caller. <b>Observable consequence:</b> if a compensation fails, the side
	///     effect it was meant to undo is left in place (e.g. a file written to disk during the unit of
	///     work remains on disk after the rollback completes). Callers that need stronger guarantees
	///     must arrange their own out-of-band cleanup.
	///     </para>
	///     <para>
	///     <b>Strict mode:</b> nesting is not supported. Calling this method while another
	///     <see cref="ICompensatingTransaction"/> on the same context is still alive (begun but not yet
	///     disposed, or committed but not yet disposed) <b>aborts the process immediately</b> rather
	///     than risk silently corrupting data. Complete the outer wrapper (commit + dispose) before
	///     beginning a new one, or split the work into separate scoped <see cref="DbContext"/>
	///     instances.
	///     </para>
	/// </remarks>
	public async Task<ICompensatingTransaction> BeginCompensatingTransactionAsync(
		CancellationToken cancellationToken = default)
	{
		// Strict-mode nesting guard: the compensation list is owned by the context and shared across all
		// wrappers, so a second active (or committed-but-not-yet-disposed) wrapper would either consume
		// the outer wrapper's pending compensations on commit or fire them on its own rollback. Either
		// behaviour is silent data corruption; FailFast loudly so the misuse cannot ship.
		if (mActiveCompensatingTransactions > 0 || mCommittedCompensatingTransactionsAwaitingDispose > 0)
		{
			FailFast.TerminateApplication(
				"BeginCompensatingTransactionAsync was called while another ICompensatingTransaction " +
				"on this LumaCoreDbContext is still active (or has been committed but not yet disposed). " +
				"Nesting is not supported because the compensation list is shared per context: the inner " +
				"wrapper would either swallow the outer wrapper's pending compensations on commit or fire " +
				"them on its own rollback. Complete the outer wrapper (commit + dispose) before beginning " +
				"a new one, or split the work into separate scoped DbContext instances.");
		}

		IDbContextTransaction inner = await Database
			                              .BeginTransactionAsync(cancellationToken)
			                              .ConfigureAwait(false);

		// F-2: increment the counter BEFORE constructing the wrapper so the active-state is consistent
		// even if construction throws. The CompensatingTransaction constructor only assigns fields and
		// is not expected to throw under normal conditions — the catch block exists purely as a defence
		// against pathological failures (OOM, ThreadAbort, future constructor changes). Without this
		// guard the counter would leak (false-positive misuse guard later) AND the inner transaction
		// would leak (held connection blocked until DbContext disposal). Decrement + dispose-on-failure
		// keeps both invariants intact.
		mActiveCompensatingTransactions++;
		try
		{
			return new CompensatingTransaction(this, inner);
		}
		catch
		{
			mActiveCompensatingTransactions--;
			await inner.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	/// <summary>
	/// Executes all pending compensations in LIFO order, swallowing individual failures (logged but
	/// non-fatal) so a single broken cleanup cannot prevent the others from running. The pending list is
	/// cleared regardless of outcome.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	private async Task ExecuteRollbackCompensationsAsync(CancellationToken cancellationToken)
	{
		if (mRollbackCompensations.Count == 0)
			return;

		// Snapshot + clear up front so registrations made *during* compensation (rare, but possible if a
		// cleanup itself touches the context) do not get processed in this pass.
		List<(object Handle, Func<CancellationToken, Task> Action)> snapshot = new(mRollbackCompensations);
		mRollbackCompensations.Clear();

		ILogger<LumaCoreDbContext>? logger = ResolveLogger();

		for (int i = snapshot.Count - 1; i >= 0; i--)
		{
			try
			{
				await snapshot[i].Action(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// LogWarning (not LogError) on purpose: compensations are best-effort cleanup for
				// side effects that have already been rolled back at the DB level. A failed
				// compensation leaks at worst an orphan file on disk that the next GC cycle will
				// reclaim — it never corrupts state. Using Error here would trip alarm routing in
				// many logging pipelines for what is, by design, a recoverable cleanup miss.
				//
				// Log the 1-based LIFO execution position (not the raw snapshot index `i`) so the
				// number matches the order callers reason about: "the first compensation that ran
				// failed" is #1, regardless of how many were registered.
				int executionOrder = snapshot.Count - i;
				logger?.LogWarning(
					ex,
					"Rollback compensation #{ExecutionOrder} of {Total} (LIFO) failed; " +
					"continuing with remaining compensations",
					executionOrder,
					snapshot.Count);
			}
		}
	}

	/// <summary>
	/// Discards all pending compensations without executing them. Called on successful commit.
	/// </summary>
	private void ClearRollbackCompensations()
	{
		mRollbackCompensations.Clear();
	}

	/// <summary>
	/// Resolves the <see cref="ILogger{TCategoryName}"/> for this context from the EF Core internal
	/// service provider, returning <see langword="null"/> if logging is not registered.
	/// </summary>
	/// <returns>The logger instance, or <see langword="null"/>.</returns>
	private ILogger<LumaCoreDbContext>? ResolveLogger()
	{
		return ((IInfrastructure<IServiceProvider>)this).Instance
		       .GetService(typeof(ILogger<LumaCoreDbContext>)) as ILogger<LumaCoreDbContext>;
	}
}
