// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core;

partial class LifecycleManagement
{
	/// <summary>
	/// Begins a new operation scope that tracks a pending operation.
	/// The scope must be disposed when the operation completes.
	/// </summary>
	/// <returns>An <see cref="OperationScope"/> that must be disposed when the operation completes.</returns>
	/// <exception cref="InvalidOperationException">The object is not initialized.</exception>
	/// <exception cref="ObjectDisposedException">The object is disposing or already disposed.</exception>
	/// <remarks>
	///     <para>
	///     This method returns a <see langword="ref struct"/> that cannot be used across <see langword="await"/> boundaries.
	///     For async operations, use <see cref="BeginAsyncOperation"/> instead.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// using var _ = BeginOperation();
	/// // ... perform synchronous operation ...
	/// </code>
	/// </example>
	protected OperationScope BeginOperation()
	{
		lock (LifecycleState.Sync)
		{
			LifecycleState.EnsureIsInitialized();
			LifecycleState.IncrementPendingOperationsCount();
		}

		return new OperationScope(LifecycleState);
	}

	/// <summary>
	/// Begins a new async operation scope that tracks a pending operation.
	/// The scope must be disposed when the operation completes.
	/// </summary>
	/// <returns>An <see cref="AsyncOperationScope"/> that must be disposed when the operation completes.</returns>
	/// <exception cref="InvalidOperationException">The object is not initialized.</exception>
	/// <exception cref="ObjectDisposedException">The object is disposing or already disposed.</exception>
	/// <remarks>
	///     <para>
	///     This method returns a class-based scope that can be used across <see langword="await"/> boundaries.
	///     For synchronous operations, prefer <see cref="BeginOperation"/> which uses a stack-allocated scope.
	///     </para>
	///     <para>
	///     The returned scope implements <see cref="IDisposable"/> (not <see cref="IAsyncDisposable"/>)
	///     so you can use <c>using</c> instead of <c>await using</c>, avoiding <c>ConfigureAwait(false)</c> issues.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// using var _ = BeginAsyncOperation();
	/// await SomeAsyncOperation().ConfigureAwait(false);
	/// // ... scope is disposed after the using block ...
	/// </code>
	/// </example>
	protected AsyncOperationScope BeginAsyncOperation()
	{
		lock (LifecycleState.Sync)
		{
			LifecycleState.EnsureIsInitialized();
			LifecycleState.IncrementPendingOperationsCount();
		}

		return new AsyncOperationScope(LifecycleState);
	}

	/// <summary>
	/// Represents an active async operation scope that tracks a pending operation.
	/// Disposing this scope decrements the pending operation counter.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is a class-based scope for use in async methods where the scope spans <see langword="await"/> boundaries.
	///     For synchronous operations, prefer <see cref="OperationScope"/> from <see cref="BeginOperation"/>, which uses
	///     a stack-allocated <see langword="ref struct"/>.
	///     </para>
	///     <para>
	///     This class intentionally only implements <see cref="IDisposable"/> (not <see cref="IAsyncDisposable"/>)
	///     because <see cref="Dispose"/> is synchronous. This avoids <c>ConfigureAwait(false)</c> issues with
	///     <c>await using</c>.
	///     </para>
	///     <para>
	///     While this scope is active, any call to <see cref="LifecycleManagement.DisposeAsync()"/> will wait
	///     asynchronously until all pending operations complete. This ensures graceful shutdown without
	///     interrupting in-flight work.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// public async Task ProcessDataAsync()
	/// {
	///     using var scope = BeginAsyncOperation();
	///     // Parent object cannot be disposed while this scope is active
	///     await FetchDataAsync().ConfigureAwait(false);
	///     await ProcessAsync().ConfigureAwait(false);
	/// } // Scope disposed here, pending operation count decremented
	///     </code>
	/// </example>
	public sealed class AsyncOperationScope : IDisposable
	{
		private readonly LifecycleState mLifecycleState;
		private          int            mDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncOperationScope"/> class.
		/// </summary>
		/// <param name="lifecycleState">The lifecycle state to track.</param>
		internal AsyncOperationScope(LifecycleState lifecycleState)
		{
			mLifecycleState = lifecycleState;
		}

		/// <summary>
		/// Ends the operation scope and decrements the pending operation counter.
		/// </summary>
		public void Dispose()
		{
			if (Interlocked.Exchange(ref mDisposed, 1) != 0) return;

			lock (mLifecycleState.Sync)
			{
				mLifecycleState.DecrementPendingOperationsCount();
			}
		}
	}

	/// <summary>
	/// Represents an active synchronous operation scope that tracks a pending operation.
	/// Disposing this scope decrements the pending operation counter.
	/// </summary>
	/// <remarks>
	///     <para>
	///     This is a <see langword="ref struct"/> to prevent heap allocations and ensure it cannot be used across
	///     <see langword="await"/> boundaries or stored in fields.
	///     </para>
	///     <para>
	///     For async operations, use <see cref="AsyncOperationScope"/> from <see cref="BeginAsyncOperation"/> instead.
	///     </para>
	///     <para>
	///     While this scope is active, any call to <see cref="LifecycleManagement.DisposeAsync()"/> will wait
	///     asynchronously until all pending operations complete. This ensures graceful shutdown without
	///     interrupting in-flight work.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// public void ProcessData()
	/// {
	///     using var scope = BeginOperation();
	///     // Parent object cannot be disposed while this scope is active
	///     FetchData();
	///     Process();
	/// } // Scope disposed here, pending operation count decremented
	///     </code>
	/// </example>
	public ref struct OperationScope
	{
		private readonly LifecycleState mLifecycleState;
		private          bool           mDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="OperationScope"/> struct.
		/// </summary>
		/// <param name="lifecycleState">The lifecycle state to track.</param>
		internal OperationScope(LifecycleState lifecycleState)
		{
			mLifecycleState = lifecycleState;
			mDisposed = false;
		}

		/// <summary>
		/// Ends the operation scope and decrements the pending operation counter.
		/// </summary>
		public void Dispose()
		{
			if (mDisposed) return;
			mDisposed = true;

			lock (mLifecycleState.Sync)
			{
				mLifecycleState.DecrementPendingOperationsCount();
			}
		}
	}
}
