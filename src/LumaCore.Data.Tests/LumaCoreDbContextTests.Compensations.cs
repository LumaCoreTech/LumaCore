// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Core;
using LumaCore.Data.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Xunit;

namespace LumaCore.Data.Tests;

// Compensation lifecycle: from registration through rollback, commit, and dispose.
//
// These tests follow the compensation API from its simplest use (register + rollback fires)
// through its harder edges: execution order, error isolation, the misuse FailFast, and the
// invariants that keep the active-wrapper counter honest across exceptions.
//
//   1. RegisterRollbackCompensation
//        * null compensation → ArgumentNullException (null guard).
//        * no transaction active → returns a unique handle, registration succeeds.
//        * conventional transaction active → FailFast (observable via BeforeTermination).
//
//   2. UnregisterRollbackCompensation
//        * unknown handle → no-op (registered compensation still fires).
//        * null handle → no-op (no throw).
//        * valid handle → compensation is removed and does NOT fire on rollback.
//
//   3. BeginCompensatingTransactionAsync
//        * increments the active-wrapper counter.
//        * opens a real EF transaction (Database.CurrentTransaction is non-null afterwards).
//
//   4. CommitAsync / RollbackAsync
//        * RollbackAsync executes compensations in LIFO order.
//        * A failing compensation does not prevent later compensations from running.
//        * If the inner DB rollback throws, compensations still run (finally semantics).
//        * CommitAsync clears the pending compensations so they DO NOT fire on dispose.
//
//   5. DisposeAsync
//        * Without a prior Commit → acts as implicit rollback and fires compensations.
//        * After a Commit → no compensation fires, but the counter is still decremented.
//        * Decrements the counter even when the inner disposal throws.

public sealed partial class LumaCoreDbContextTests
{
	#region RegisterRollbackCompensation

	/// <summary>
	/// Verifies that <see cref="LumaCoreDbContext.RegisterRollbackCompensation"/> rejects a
	/// <see langword="null"/> compensation with <see cref="ArgumentNullException"/> identifying the
	/// offending parameter.
	/// </summary>
	[Fact]
	public void RegisterRollbackCompensation_WhenCompensationIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => mFixture.DbContext.RegisterRollbackCompensation(null!));
		Assert.Equal("compensation", ex.ParamName);
	}

	/// <summary>
	/// Verifies that when no transaction is active, <see cref="LumaCoreDbContext.RegisterRollbackCompensation"/>
	/// returns a non-<see langword="null"/> handle and distinct invocations return distinct handles.
	/// Handle identity matters because <see cref="LumaCoreDbContext.UnregisterRollbackCompensation"/> removes
	/// by <see cref="object.ReferenceEquals"/>.
	/// </summary>
	[Fact]
	public void RegisterRollbackCompensation_WhenNoTransactionActive_ReturnsDistinctHandlesPerCall()
	{
		// Arrange
		var recorder = new CompensationRecorder();

		// Act
		object first = mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("a"));
		object second = mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("b"));

		// Assert
		Assert.NotNull(first);
		Assert.NotNull(second);
		Assert.NotSame(first, second);
	}

	/// <summary>
	/// Verifies that <see cref="LumaCoreDbContext.RegisterRollbackCompensation"/> triggers
	/// <see cref="FailFast.TerminateApplication(string)"/> when a conventional (non-compensating) database
	/// transaction is active: without this guard, the registration would be silently dropped on commit
	/// because the plain <see cref="Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction"/> has no
	/// awareness of the compensation list, corrupting data invisibly.
	/// </summary>
	/// <remarks>
	/// The test observes the FailFast via the <see cref="FailFast.BeforeTermination"/> event, setting
	/// <see cref="FailFastEventArgs.Cancel"/> so the process does not actually terminate. The resulting
	/// <see cref="FailFastCanceledException"/> carries the original diagnostic message, which the test
	/// asserts verbatim to pin the misuse contract.
	/// </remarks>
	[Fact]
	public async Task RegisterRollbackCompensation_WhenForeignTransactionActive_TriggersFailFast()
	{
		// Arrange — open a conventional transaction (NOT via BeginCompensatingTransactionAsync).
		await using IDbContextTransaction foreign =
			await mFixture.DbContext.Database.BeginTransactionAsync();

		string? observedMessage = null;
		EventHandler<FailFastEventArgs> subscriber = (_, args) =>
		{
			observedMessage = args.Message;
			args.Cancel = true; // prevent Environment.FailFast during the test
		};
		FailFast.BeforeTermination += subscriber;

		try
		{
			// Act + Assert
			var ex = Assert.Throws<FailFastCanceledException>(() =>
				mFixture.DbContext.RegisterRollbackCompensation(_ => Task.CompletedTask));

			// Exact-match on the diagnostic message — the misuse contract is user-facing documentation
			// that operators rely on when they hit this in development.
			Assert.Equal(
				"RegisterRollbackCompensation was called while a database transaction is active that was " +
				"NOT started via LumaCoreDbContext.BeginCompensatingTransactionAsync. The registration would " +
				"be silently dropped on commit/rollback, leading to leaked side effects or incorrect undo of " +
				"permanent state. Wrap the unit of work in BeginCompensatingTransactionAsync (or remove the " +
				"manual transaction) before registering compensations.",
				ex.Message);
			Assert.Equal(ex.Message, observedMessage);
		}
		finally
		{
			FailFast.BeforeTermination -= subscriber;
		}
	}

	#endregion

	#region UnregisterRollbackCompensation

	/// <summary>
	/// Verifies that <see cref="LumaCoreDbContext.UnregisterRollbackCompensation"/> is a safe no-op for a
	/// <see langword="null"/> handle. Production callers rely on this so cleanup code paths (e.g. a
	/// <c>finally</c> that always unregisters) don't have to null-check the handle themselves.
	/// </summary>
	[Fact]
	public async Task UnregisterRollbackCompensation_WhenHandleIsNull_DoesNotThrowAndLeavesRegistrations()
	{
		// Arrange
		var recorder = new CompensationRecorder();
		mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("survivor"));

		// Act: no throw.
		mFixture.DbContext.UnregisterRollbackCompensation(null!);

		// Assert: the unrelated compensation must still fire on rollback — a null unregister must
		// not silently drop other registrations.
		await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		await tx.RollbackAsync();
		Assert.Equal(["survivor"], recorder.Invocations);
	}

	/// <summary>
	/// Verifies that <see cref="LumaCoreDbContext.UnregisterRollbackCompensation"/> silently ignores an
	/// unknown handle: the pending registrations are unaffected. This matters because callers may pass a
	/// handle from a previous logical unit of work (e.g. a leaked retry token) and must not accidentally
	/// remove a different compensation that happens to match by value semantics.
	/// </summary>
	[Fact]
	public async Task UnregisterRollbackCompensation_WhenHandleIsUnknown_LeavesPendingCompensationsInPlace()
	{
		// Arrange
		var recorder = new CompensationRecorder();
		mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("keep"));
		object foreignHandle = new();

		// Act
		mFixture.DbContext.UnregisterRollbackCompensation(foreignHandle);

		// Assert: the registered compensation survives and fires on rollback.
		await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		await tx.RollbackAsync();
		Assert.Equal(["keep"], recorder.Invocations);
	}

	/// <summary>
	/// Verifies that <see cref="LumaCoreDbContext.UnregisterRollbackCompensation"/> with a valid handle
	/// removes the compensation so it does NOT fire on rollback. This is the happy path for the
	/// "side effect became permanent" case — production code (e.g. after a successful own-transaction
	/// commit) unregisters the cleanup so a later outer rollback cannot undo the permanent state.
	/// </summary>
	[Fact]
	public async Task UnregisterRollbackCompensation_WhenHandleIsValid_RemovesCompensationFromRollback()
	{
		// Arrange: two compensations — we unregister the second.
		var recorder = new CompensationRecorder();
		mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("keep"));
		object removable = mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("remove"));

		// Act
		mFixture.DbContext.UnregisterRollbackCompensation(removable);
		await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		await tx.RollbackAsync();

		// Assert: only the first compensation fired.
		Assert.Equal(["keep"], recorder.Invocations);
	}

	#endregion

	#region BeginCompensatingTransactionAsync

	/// <summary>
	/// Verifies that <see cref="LumaCoreDbContext.BeginCompensatingTransactionAsync"/> both opens a real
	/// database transaction (observable via
	/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CurrentTransaction"/>)
	/// and increments the internal active-wrapper counter, so
	/// <see cref="LumaCoreDbContext.RegisterRollbackCompensation"/> recognises the compensation path and
	/// does NOT fire its misuse FailFast.
	/// </summary>
	[Fact]
	public async Task BeginCompensatingTransactionAsync_IncrementsActiveCounterAndOpensUnderlyingTransaction()
	{
		// Arrange
		Assert.Null(mFixture.DbContext.Database.CurrentTransaction);
		Assert.Equal(0, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);

		// Act
		await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();

		// Assert
		Assert.NotNull(mFixture.DbContext.Database.CurrentTransaction);
		Assert.Equal(1, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);
	}

	#endregion

	#region RollbackAsync

	/// <summary>
	/// Verifies that <see cref="ICompensatingTransaction.RollbackAsync"/> executes pending compensations
	/// in LIFO order (reverse of registration). LIFO mirrors how nested resources are released and is
	/// the contract the production code (<see cref="ResourceService"/>) relies on.
	/// </summary>
	[Fact]
	public async Task RollbackAsync_WhenMultipleCompensationsRegistered_ExecutesThemInLifoOrder()
	{
		// Arrange
		var recorder = new CompensationRecorder();
		await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("first"));
		mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("second"));
		mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("third"));

		// Act
		await tx.RollbackAsync();

		// Assert: reverse registration order (LIFO).
		Assert.Equal(["third", "second", "first"], recorder.Invocations);
	}

	/// <summary>
	/// Verifies that when one compensation throws, <see cref="ICompensatingTransaction.RollbackAsync"/>
	/// continues running the remaining compensations and does NOT let the failure propagate to the caller.
	/// Individual compensation failures are best-effort (logged, then swallowed); a single broken cleanup
	/// must never prevent the others from running.
	/// </summary>
	[Fact]
	public async Task RollbackAsync_WhenOneCompensationThrows_RemainingCompensationsStillRunAndFailureIsSwallowed()
	{
		// Arrange: three compensations, the middle (LIFO-wise: the second to fire) throws.
		var recorder = new CompensationRecorder();
		await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("first"));
		mFixture.DbContext.RegisterRollbackCompensation(
			recorder.CreateThrowing("second-throws", new InvalidOperationException("boom")));
		mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("third"));

		// Act: no exception must escape.
		await tx.RollbackAsync();

		// Assert: all three compensations ran, in LIFO order, despite the middle one throwing.
		Assert.Equal(["third", "second-throws", "first"], recorder.Invocations);
	}

	/// <summary>
	/// Verifies that <see cref="ICompensatingTransaction.RollbackAsync"/> runs compensations even when the
	/// inner database rollback throws: the production code wraps the inner call in a <c>try/finally</c>
	/// specifically so a broken DB rollback cannot orphan the file cleanup.
	/// </summary>
	/// <remarks>
	/// The inner-rollback failure is forced by disposing the underlying <see cref="DbConnection"/>
	/// between Begin and Rollback — SQLite then fails the rollback syscall with an
	/// <see cref="InvalidOperationException"/>. The original exception still propagates to the caller
	/// (so callers that explicitly await <see cref="ICompensatingTransaction.RollbackAsync"/> for
	/// diagnostics continue to see the real failure), but by the time it is thrown the compensation
	/// must already have fired.
	/// </remarks>
	[Fact]
	public async Task RollbackAsync_WhenInnerDbRollbackThrows_CompensationStillFiresAndInnerErrorPropagates()
	{
		// Arrange
		var recorder = new CompensationRecorder();
		ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();
		mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("cleanup"));

		// Act: force the inner rollback to fail by closing the shared SQLite connection under it.
		await mFixture.DbContext.Database.GetDbConnection().CloseAsync();

		// The inner-rollback exception is the observable failure for callers that await explicitly.
		// We accept any exception type — the concrete one is a SQLite detail. The test's point is:
		// (a) something threw, (b) the compensation ran BEFORE the throw.
		await Assert.ThrowsAnyAsync<Exception>(() => tx.RollbackAsync());

		// Assert — the finally clause in RollbackAsync must have run the compensation.
		Assert.Equal(["cleanup"], recorder.Invocations);

		// Clean up: dispose the transaction explicitly. DisposeAsync must not re-run the compensation
		// (mCompleted was set at the top of RollbackAsync). This also pins that mCompleted-up-front
		// guard incidentally.
		await tx.DisposeAsync();
		Assert.Equal(["cleanup"], recorder.Invocations); // unchanged — compensation fired exactly once
	}

	#endregion

	#region CommitAsync + DisposeAsync

	/// <summary>
	/// Verifies that after <see cref="ICompensatingTransaction.CommitAsync"/> the pending compensations
	/// are discarded, so the subsequent implicit dispose does NOT fire them. This is the core contract
	/// for "the side effect is now permanent": a successful commit must never cause the cleanup to run.
	/// </summary>
	[Fact]
	public async Task CommitAsync_WhenCompensationsPending_ClearsThemSoDisposeDoesNotFireThem()
	{
		// Arrange
		var recorder = new CompensationRecorder();
		await using (ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync())
		{
			mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("should-not-fire"));

			// Act
			await tx.CommitAsync();
		} // implicit DisposeAsync — must NOT fire the compensation

		// Assert
		Assert.Empty(recorder.Invocations);
	}

	/// <summary>
	/// Verifies that disposing an <see cref="ICompensatingTransaction"/> without a prior commit is
	/// treated as an implicit rollback and fires the pending compensations. This is the path taken when
	/// a caller exits an <c>await using</c> block via exception — the cleanup must run automatically so
	/// file-system side effects are not leaked.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_WhenCommitWasNeverCalled_ActsAsImplicitRollbackAndFiresCompensations()
	{
		// Arrange
		var recorder = new CompensationRecorder();

		// Act: enter the using block, register a compensation, exit without committing.
		await using (await mFixture.DbContext.BeginCompensatingTransactionAsync())
		{
			mFixture.DbContext.RegisterRollbackCompensation(recorder.Create("implicit"));
		}

		// Assert
		Assert.Equal(["implicit"], recorder.Invocations);
	}

	/// <summary>
	/// Verifies that <see cref="IAsyncDisposable.DisposeAsync"/> decrements the active-wrapper
	/// counter exactly once per wrapper — both for the successful-commit path and for the implicit
	/// rollback path. Without this invariant the counter would drift, and a later legitimate misuse
	/// (conventional
	/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.BeginTransactionAsync(CancellationToken)"/>
	/// + register) would fail to FailFast because the SUT thinks a compensating transaction is still
	/// active.
	/// </summary>
	[Fact]
	public async Task DisposeAsync_DecrementsActiveCounterOnBothCommitAndImplicitRollback()
	{
		// Arrange + Act 1 — commit path
		await using (ICompensatingTransaction committed = await mFixture.DbContext.BeginCompensatingTransactionAsync())
		{
			Assert.Equal(1, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);
			await committed.CommitAsync();
		}
		Assert.Equal(0, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);

		// Arrange + Act 2 — implicit rollback path
		await using (await mFixture.DbContext.BeginCompensatingTransactionAsync())
		{
			Assert.Equal(1, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);
		}
		Assert.Equal(0, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);
	}

	/// <summary>
	/// Verifies that <see cref="LumaCoreDbContext.RegisterRollbackCompensation"/> triggers
	/// <see cref="FailFast.TerminateApplication(string)"/> when a caller attempts to register a new
	/// compensation <em>after</em> commit but before the enclosing <c>await using</c> has actually
	/// disposed the wrapper. Without this hardening the registration would leak onto the next
	/// <see cref="LumaCoreDbContext.BeginCompensatingTransactionAsync"/> call on the same context and
	/// fire against an unrelated unit of work — silently corrupting the next caller's state.
	/// </summary>
	/// <remarks>
	/// The production hardening uses a dedicated <c>mCommittedCompensatingTransactionsAwaitingDispose</c>
	/// counter (incremented on <c>CommitAsync</c>, decremented on <c>DisposeAsync</c>). That counter is
	/// non-zero exactly in the commit-to-dispose window, letting the misuse guard fire precisely for
	/// this case without interfering with the legitimate "Register before Begin" pattern used by the
	/// standalone upload path in <c>ResourceService.UploadAsync</c>.
	/// </remarks>
	[Fact]
	public async Task RegisterRollbackCompensation_WhenCalledAfterCommitButBeforeDispose_TriggersFailFast()
	{
		// Arrange
		string? observedMessage = null;
		EventHandler<FailFastEventArgs> subscriber = (_, args) =>
		{
			observedMessage = args.Message;
			args.Cancel = true; // prevent Environment.FailFast during the test
		};
		FailFast.BeforeTermination += subscriber;

		try
		{
			// Act + Assert — enter the using block, commit successfully, then attempt a new register
			// BEFORE exiting the block. The register must FailFast via the dedicated guard.
			await using ICompensatingTransaction tx = await mFixture.DbContext.BeginCompensatingTransactionAsync();
			await tx.CommitAsync();

			// Sanity: the awaiting-dispose counter is non-zero in this window, while the active
			// counter is still 1 (it is only decremented on dispose).
			Assert.Equal(1, mFixture.DbContext.CommittedCompensatingTransactionsAwaitingDisposeCountForTests);
			Assert.Equal(1, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);

			var ex = Assert.Throws<FailFastCanceledException>(() =>
				mFixture.DbContext.RegisterRollbackCompensation(_ => Task.CompletedTask));

			// Exact-match on the diagnostic — this message is user-facing documentation that
			// developers hitting the misuse in development will see in the stack.
			Assert.Equal(
				"RegisterRollbackCompensation was called after CompensatingTransaction.CommitAsync " +
				"succeeded but before the wrapper was disposed. The registration would leak onto the " +
				"next BeginCompensatingTransactionAsync call and fire against an unrelated unit of " +
				"work. Begin a new compensating transaction before registering further compensations.",
				ex.Message);
			Assert.Equal(ex.Message, observedMessage);
		}
		finally
		{
			FailFast.BeforeTermination -= subscriber;
		}
	}

	/// <summary>
	/// Verifies that a commit → dispose → begin → commit → dispose sequence keeps both the active and
	/// the awaiting-dispose counters balanced across wrappers. The invariant this pins is:
	/// <list type="bullet">
	///     <item>
	///         <description>
	///         <c>CommitAsync</c> leaves the active counter untouched and increments the awaiting-dispose
	///         counter.
	///         </description>
	///     </item>
	///     <item>
	///         <description>
	///         <c>DisposeAsync</c> always decrements the active counter; for committed wrappers it also
	///         decrements the awaiting-dispose counter.
	///         </description>
	///     </item>
	/// </list>
	/// If either decrement were ever missed, a subsequent
	/// <see cref="LumaCoreDbContext.BeginCompensatingTransactionAsync"/> would start from a drifted
	/// baseline and the register-after-commit misuse guard would either fire spuriously or miss real
	/// misuses.
	/// </summary>
	[Fact]
	public async Task CommitAsync_ThenBeginNewTransaction_KeepsCountersBalancedAcrossWrappers()
	{
		// Arrange + Act 1 — first wrapper commits.
		await using (ICompensatingTransaction first = await mFixture.DbContext.BeginCompensatingTransactionAsync())
		{
			Assert.Equal(1, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);
			Assert.Equal(0, mFixture.DbContext.CommittedCompensatingTransactionsAwaitingDisposeCountForTests);
			await first.CommitAsync();
			// Commit promoted the wrapper into the awaiting-dispose bucket; active counter stays at
			// 1 until actual disposal.
			Assert.Equal(1, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);
			Assert.Equal(1, mFixture.DbContext.CommittedCompensatingTransactionsAwaitingDisposeCountForTests);
		}
		// Dispose drained both counters.
		Assert.Equal(0, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);
		Assert.Equal(0, mFixture.DbContext.CommittedCompensatingTransactionsAwaitingDisposeCountForTests);

		// Act 2 — a fresh wrapper must increment correctly from zero.
		await using (ICompensatingTransaction second = await mFixture.DbContext.BeginCompensatingTransactionAsync())
		{
			Assert.Equal(1, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);
			Assert.Equal(0, mFixture.DbContext.CommittedCompensatingTransactionsAwaitingDisposeCountForTests);
			await second.CommitAsync();
		}
		Assert.Equal(0, mFixture.DbContext.ActiveCompensatingTransactionCountForTests);
		Assert.Equal(0, mFixture.DbContext.CommittedCompensatingTransactionsAwaitingDisposeCountForTests);
	}

	#endregion
}
