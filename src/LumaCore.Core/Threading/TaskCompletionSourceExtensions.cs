// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;

namespace LumaCore.Core.Threading;

/// <summary>
/// Provides extension methods for <see cref="TaskCompletionSource{TResult}"/> that simplify
/// propagating task completion states and creating async-friendly task sources.
/// </summary>
/// <remarks>
///     <para>
///     These extensions are particularly useful when building custom async primitives that need to
///     propagate completion (success/exception/cancellation) from one task to another.
///     </para>
///     <para>
///     <b>Thread-pool tuning.</b> Earlier revisions of this class raised the process-wide minimum
///     worker thread count from a static constructor as a workaround for thread-pool starvation on
///     small CI runners. That side-effect has been removed: library code must not mutate the
///     process-wide <see cref="ThreadPool"/>. Applications that target very small CI runners or
///     constrained containers should opt in explicitly via
///     <see cref="ThreadPoolBootstrap.EnsureMinWorkerThreads"/> in their bootstrap, or set the
///     environment variable <c>DOTNET_ThreadPool_MinWorkerThreads</c> before the runtime starts.
///     </para>
/// </remarks>
public static class TaskCompletionSourceExtensions
{
	/// <summary>
	/// Attempts to complete a <see cref="TaskCompletionSource{TResult}"/> by propagating the completion state
	/// (success/exception/cancellation) of another task.
	/// </summary>
	/// <typeparam name="TResult">The result type of the target task completion source.</typeparam>
	/// <typeparam name="TSourceResult">
	/// The result type of the source task. Must be assignable to <typeparamref name="TResult"/>.
	/// </typeparam>
	/// <param name="this">The task completion source to complete.</param>
	/// <param name="task">The completed source task whose state should be propagated.</param>
	/// <returns>
	/// <see langword="true"/> if the task completion source was successfully completed by this call;
	/// <see langword="false"/> if it was already completed.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="this"/> or <paramref name="task"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method copies the final state of <paramref name="task"/> to the task completion source:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>If <paramref name="task"/> completed successfully, sets the result.</description>
	///         </item>
	///         <item>
	///             <description>If <paramref name="task"/> faulted, sets the exception(s).</description>
	///         </item>
	///         <item>
	///             <description>
	///             If <paramref name="task"/> was canceled, sets the cancellation state (preserving the token if
	///             available).
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     <b>Typical usage:</b> Implementing custom async combinators or synchronization primitives.
	///     </para>
	/// </remarks>
	public static bool TryCompleteFromCompletedTask<TResult, TSourceResult>(
		this TaskCompletionSource<TResult> @this,
		Task<TSourceResult>                task) where TSourceResult : TResult
	{
		ArgumentNullException.ThrowIfNull(@this);
		ArgumentNullException.ThrowIfNull(task);

		if (task.IsFaulted)
		{
			Debug.Assert(task.Exception != null);
			return @this.TrySetException(task.Exception.InnerExceptions);
		}

		if (task.IsCanceled)
		{
			try
			{
				task.WaitAndUnwrapException();
				throw new UnreachableException(
					"IsCanceled task did not throw OperationCanceledException."); // unreachable
			}
			catch (OperationCanceledException exception)
			{
				CancellationToken token = exception.CancellationToken;
				return token.IsCancellationRequested ? @this.TrySetCanceled(token) : @this.TrySetCanceled();
			}
		}

		return @this.TrySetResult(task.Result);
	}

	/// <summary>
	/// Attempts to complete a <see cref="TaskCompletionSource{TResult}"/> by propagating the completion state
	/// of a non-generic task, using a custom result value for successful completion.
	/// </summary>
	/// <typeparam name="TResult">The result type of the target task completion source.</typeparam>
	/// <param name="this">The task completion source to complete.</param>
	/// <param name="task">The completed source task whose state (exception/cancellation) should be propagated.</param>
	/// <param name="resultFunc">
	/// A delegate that returns the result value to use if <paramref name="task"/> completed successfully.
	/// This delegate is only invoked if the task succeeded.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the task completion source was successfully completed by this call;
	/// <see langword="false"/> if it was already completed.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="this"/>, <paramref name="task"/>, or <paramref name="resultFunc"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This overload is useful when propagating the state of a non-generic <see cref="Task"/> to a
	///     <see cref="TaskCompletionSource{TResult}"/> that requires a result value. Since <see cref="Task"/>
	///     has no result, you must provide a <paramref name="resultFunc"/> to supply the result on success.
	///     </para>
	///     <para>
	///     Exceptions and cancellations are propagated directly from <paramref name="task"/>;
	///     <paramref name="resultFunc"/> is only called for successful completion.
	///     </para>
	/// </remarks>
	public static bool TryCompleteFromCompletedTask<TResult>(
		this TaskCompletionSource<TResult> @this,
		Task                               task,
		Func<TResult>                      resultFunc)
	{
		ArgumentNullException.ThrowIfNull(@this);
		ArgumentNullException.ThrowIfNull(task);
		ArgumentNullException.ThrowIfNull(resultFunc);

		if (task.IsFaulted)
		{
			Debug.Assert(task.Exception != null);
			return @this.TrySetException(task.Exception.InnerExceptions);
		}

		if (task.IsCanceled)
		{
			try
			{
				task.WaitAndUnwrapException();
				throw new UnreachableException(
					"IsCanceled task did not throw OperationCanceledException."); // unreachable
			}
			catch (OperationCanceledException exception)
			{
				CancellationToken token = exception.CancellationToken;
				return token.IsCancellationRequested ? @this.TrySetCanceled(token) : @this.TrySetCanceled();
			}
		}

		return @this.TrySetResult(resultFunc());
	}

	/// <summary>
	/// Creates a new <see cref="TaskCompletionSource{TResult}"/> configured for async/await patterns.
	/// </summary>
	/// <typeparam name="TResult">The result type of the task completion source.</typeparam>
	/// <returns>
	/// A new <see cref="TaskCompletionSource{TResult}"/> with <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>
	/// set, ensuring continuations don't run synchronously on the completing thread.
	/// </returns>
	/// <remarks>
	///     <para>
	///     The <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/> option prevents a subtle deadlock/performance
	///     issue where continuations run synchronously on the thread that calls
	///     <see cref="TaskCompletionSource{TResult}.SetResult"/>,
	///     potentially blocking that thread or causing re-entrancy problems.
	///     </para>
	///     <para>
	///     <b>Best practice:</b> Always use this method instead of <c>new TaskCompletionSource&lt;T&gt;()</c>
	///     when building async primitives.
	///     </para>
	/// </remarks>
	public static TaskCompletionSource<TResult> CreateAsyncTaskSource<TResult>()
	{
		return new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
	}
}
