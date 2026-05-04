// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data;

/// <summary>
/// A database transaction whose commit/rollback semantics also process compensating actions registered
/// on the owning <see cref="LumaCoreDbContext"/> via <see cref="LumaCoreDbContext.RegisterRollbackCompensation"/>.
/// </summary>
/// <remarks>
/// Obtain an instance via <see cref="LumaCoreDbContext.BeginCompensatingTransactionAsync"/>. Disposing
/// without calling <see cref="CommitAsync"/> is treated as an implicit rollback and triggers
/// compensation execution.
/// </remarks>
public interface ICompensatingTransaction : IAsyncDisposable
{
	/// <summary>
	/// Commits the underlying database transaction and discards all pending compensations.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// Cancellation is honoured only as a pre-flight check: once the commit is in flight it cannot be
	/// aborted, even if <paramref name="cancellationToken"/> is cancelled mid-operation.
	/// </remarks>
	/// <exception cref="OperationCanceledException">
	/// <paramref name="cancellationToken"/> was already cancelled before the commit was initiated.
	/// </exception>
	Task CommitAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Rolls back the underlying database transaction and executes all pending compensations in LIFO
	/// order.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// Cancellation tokens are not honoured for the rollback path: both the database rollback and the
	/// compensation execution always run to completion, even if <paramref name="cancellationToken"/>
	/// is already cancelled. The parameter exists only to mirror the
	/// <see cref="IAsyncDisposable"/>-shaped async contract.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// The transaction has already been committed via <see cref="CommitAsync"/>.
	/// </exception>
	Task RollbackAsync(CancellationToken cancellationToken = default);
}
