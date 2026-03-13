// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel;

namespace LumaCore.Core.Threading;

/// <summary>
/// Provides extension methods for <see cref="Task"/> and <see cref="Task{TResult}"/> that simplify common
/// async patterns such as synchronous waiting with exception unwrapping, task combinators, and
/// fire-and-forget scenarios.
/// </summary>
/// <remarks>
///     <para>
///     <b>Synchronous waiting:</b> The <c>WaitAndUnwrapException</c> methods use <c>GetAwaiter().GetResult()</c>
///     to unwrap <see cref="AggregateException"/> and throw the original exception directly.
///     </para>
///     <para>
///     <b>Task combinators:</b> Extension methods like <c>WhenAny</c>, <c>WhenAll</c>, and <c>OrderByCompletion</c>
///     provide fluent syntax for composing tasks from <see cref="IEnumerable{T}"/> sequences.
///     </para>
/// </remarks>
public static class TaskExtensions
{
	#region Waiting for Task (Synchronous)

	/// <summary>
	/// Synchronously waits for the task to complete, unwrapping any exceptions.
	/// </summary>
	/// <param name="task">The task to wait for.</param>
	/// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <para>
	///     This method uses <c>GetAwaiter().GetResult()</c> instead of <c>.Result</c> or <c>.Wait()</c> to ensure
	///     that exceptions are thrown directly without being wrapped in an <see cref="AggregateException"/>.
	///     </para>
	///     <para>
	///     <b>Warning:</b> Synchronously waiting on async code can cause deadlocks in single-threaded synchronization
	///     contexts (e.g., UI threads, legacy ASP.NET). Use with caution.
	///     </para>
	/// </remarks>
	public static void WaitAndUnwrapException(this Task task)
	{
		ArgumentNullException.ThrowIfNull(task);
		task.GetAwaiter().GetResult();
	}

	/// <summary>
	/// Synchronously waits for the task to complete, unwrapping any exceptions, while observing a cancellation token.
	/// </summary>
	/// <param name="task">The task to wait for.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
	/// <exception cref="OperationCanceledException">
	/// <paramref name="cancellationToken"/> is canceled before the task completes,
	/// or if the task itself throws an <see cref="OperationCanceledException"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     Unlike <see cref="WaitAndUnwrapException(Task)"/>, this overload uses <see cref="Task.Wait(CancellationToken)"/>
	///     which can be interrupted by the cancellation token.
	///     </para>
	/// </remarks>
	public static void WaitAndUnwrapException(this Task task, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(task);

		try
		{
			task.Wait(cancellationToken);
		}
		// Defensive: while Task.Wait() should never throw AggregateException with null InnerException,
		// the when-filter prevents NullReferenceException and allows the original exception to propagate
		// if this invariant is violated by a runtime bug.
		catch (AggregateException ex) when (ex.InnerException is not null)
		{
			throw ExceptionHelpers.PrepareForRethrow(ex.InnerException);
		}
	}

	/// <summary>
	/// Synchronously waits for the task to complete, unwrapping any exceptions and returning the result.
	/// </summary>
	/// <typeparam name="TResult">The type of the task result.</typeparam>
	/// <param name="task">The task to wait for.</param>
	/// <returns>The result of the completed task.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <inheritdoc cref="WaitAndUnwrapException(Task)"
	///                 path="/remarks"/>
	/// </remarks>
	public static TResult WaitAndUnwrapException<TResult>(this Task<TResult> task)
	{
		ArgumentNullException.ThrowIfNull(task);
		return task.GetAwaiter().GetResult();
	}

	/// <summary>
	/// Synchronously waits for the task to complete, unwrapping any exceptions, while observing a cancellation token.
	/// </summary>
	/// <typeparam name="TResult">The type of the task result.</typeparam>
	/// <param name="task">The task to wait for.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>The result of the completed task.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
	/// <exception cref="OperationCanceledException">
	/// <paramref name="cancellationToken"/> is canceled before the task completes,
	/// or if the task itself throws an <see cref="OperationCanceledException"/>.
	/// </exception>
	public static TResult WaitAndUnwrapException<TResult>(this Task<TResult> task, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(task);

		try
		{
			task.Wait(cancellationToken);
			return task.Result;
		}
		// Defensive: while Task.Wait() should never throw AggregateException with null InnerException,
		// the when-filter prevents NullReferenceException and allows the original exception to propagate
		// if this invariant is violated by a runtime bug.
		catch (AggregateException ex) when (ex.InnerException is not null)
		{
			throw ExceptionHelpers.PrepareForRethrow(ex.InnerException);
		}
	}

	/// <summary>
	/// Synchronously waits for the task to complete without throwing task exceptions.
	/// </summary>
	/// <param name="task">The task to wait for.</param>
	/// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <para>
	///     Any exception thrown by the task is silently swallowed. This is useful in cleanup scenarios where you
	///     need to ensure a task has completed but don't care about its outcome.
	///     </para>
	///     <para>
	///     <b>Warning:</b> Swallowing exceptions can hide bugs. Use this method sparingly and only when you
	///     explicitly intend to ignore failures.
	///     </para>
	/// </remarks>
	public static void WaitWithoutException(this Task task)
	{
		ArgumentNullException.ThrowIfNull(task);

		try
		{
			task.Wait();
		}
		catch (AggregateException)
		{
			// Intentionally swallowed
		}
	}

	/// <summary>
	/// Synchronously waits for the task to complete without throwing task exceptions, while observing a cancellation token.
	/// </summary>
	/// <param name="task">The task to wait for.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
	/// <exception cref="OperationCanceledException">
	/// <paramref name="cancellationToken"/> is canceled before the task completes.
	/// Note that cancellation of the <em>task itself</em> is <b>not</b> rethrown.
	/// </exception>
	/// <remarks>
	///     <inheritdoc cref="WaitWithoutException(Task)"
	///                 path="/remarks"/>
	/// </remarks>
	public static void WaitWithoutException(this Task task, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(task);

		try
		{
			task.Wait(cancellationToken);
		}
		catch (AggregateException)
		{
			cancellationToken.ThrowIfCancellationRequested();
		}
	}

	#endregion

	#region Task Combinators

	/// <summary>
	/// Asynchronously waits for any of the source tasks to complete, or for the cancellation token to be canceled.
	/// </summary>
	/// <param name="this">The tasks to wait for.</param>
	/// <param name="cancellationToken">The cancellation token that cancels the wait.</param>
	/// <returns>The first task that completed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
	/// <exception cref="OperationCanceledException">The cancellation token is canceled before any task completes.</exception>
	public static Task<Task> WhenAny(this IEnumerable<Task> @this, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(@this);
		return Task.WhenAny(@this).WaitAsync(cancellationToken);
	}

	/// <summary>
	/// Asynchronously waits for any of the source tasks to complete.
	/// </summary>
	/// <param name="this">The tasks to wait for.</param>
	/// <returns>The first task that completed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
	public static Task<Task> WhenAny(this IEnumerable<Task> @this)
	{
		ArgumentNullException.ThrowIfNull(@this);
		return Task.WhenAny(@this);
	}

	/// <summary>
	/// Asynchronously waits for any of the source tasks to complete, or for the cancellation token to be canceled.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token that cancels the wait.</param>
	/// <param name="this">The tasks to wait for.</param>
	/// <typeparam name="TResult">The type of the task results.</typeparam>
	/// <returns>The first task that completed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
	/// <exception cref="OperationCanceledException">The cancellation token is canceled before any task completes.</exception>
	public static Task<Task<TResult>> WhenAny<TResult>(
		this IEnumerable<Task<TResult>> @this,
		CancellationToken               cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(@this);
		return Task.WhenAny(@this).WaitAsync(cancellationToken);
	}

	/// <summary>
	/// Asynchronously waits for any of the source tasks to complete.
	/// </summary>
	/// <param name="this">The tasks to wait for.</param>
	/// <typeparam name="TResult">The type of the task results.</typeparam>
	/// <returns>The first task that completed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <inheritdoc cref="WhenAny(IEnumerable{Task})"
	///                 path="/remarks"/>
	/// </remarks>
	public static Task<Task<TResult>> WhenAny<TResult>(this IEnumerable<Task<TResult>> @this)
	{
		ArgumentNullException.ThrowIfNull(@this);
		return Task.WhenAny(@this);
	}

	/// <summary>
	/// Asynchronously waits for all the source tasks to complete.
	/// </summary>
	/// <param name="this">The tasks to wait for.</param>
	/// <returns>A task that completes when all source tasks have completed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// This is a convenience wrapper around <see cref="Task.WhenAll(IEnumerable{Task})"/> that allows
	/// fluent chaining on <see cref="IEnumerable{T}"/> of tasks.
	/// </remarks>
	public static Task WhenAll(this IEnumerable<Task> @this)
	{
		ArgumentNullException.ThrowIfNull(@this);
		return Task.WhenAll(@this);
	}

	/// <summary>
	/// Asynchronously waits for all the source tasks to complete.
	/// </summary>
	/// <typeparam name="TResult">The type of the task results.</typeparam>
	/// <param name="this">The tasks to wait for.</param>
	/// <returns>A task containing an array of all task results.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <inheritdoc cref="WhenAll(IEnumerable{Task})"
	///                 path="/remarks"/>
	/// </remarks>
	public static Task<TResult[]> WhenAll<TResult>(this IEnumerable<Task<TResult>> @this)
	{
		ArgumentNullException.ThrowIfNull(@this);
		return Task.WhenAll(@this);
	}

	#endregion

	#region Ignoring Task Completion

	/// <summary>
	/// Fire-and-forget: observes the task's completion and swallows any exceptions.
	/// </summary>
	/// <param name="this">The task to ignore.</param>
	/// <remarks>
	///     <para>
	///     <b>⚠️ DANGEROUS:</b> This method intentionally swallows all exceptions. Use only when you explicitly
	///     intend to discard the task's result and any errors it may produce.
	///     </para>
	///     <para>
	///     This method is hidden from IntelliSense (<see cref="EditorBrowsableState.Never"/>) to discourage
	///     casual use. It exists for legitimate fire-and-forget scenarios where you need to prevent
	///     unobserved task exceptions from crashing the application.
	///     </para>
	///     <para>
	///     The <c>async void</c> signature is intentional; it ensures that unobserved exceptions are caught
	///     within this method rather than propagating to <see cref="TaskScheduler.UnobservedTaskException"/>.
	///     </para>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static async void Ignore(this Task @this)
	{
		try
		{
			await @this.ConfigureAwait(false);
		}
		catch
		{
			// Intentionally swallowed
		}
	}

	/// <summary>
	/// Fire-and-forget: observes the task's completion and swallows any exceptions.
	/// </summary>
	/// <typeparam name="T">The type of the task result (ignored).</typeparam>
	/// <param name="this">The task to ignore.</param>
	/// <remarks>
	///     <inheritdoc cref="Ignore(Task)"
	///                 path="/remarks"/>
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static async void Ignore<T>(this Task<T> @this)
	{
		try
		{
			await @this.ConfigureAwait(false);
		}
		catch
		{
			// Intentionally swallowed
		}
	}

	#endregion

	#region Order by Completion

	/// <summary>
	/// Returns a new collection of tasks that complete in the order their source tasks complete.
	/// </summary>
	/// <typeparam name="T">The type of the task results.</typeparam>
	/// <param name="this">The tasks to reorder by completion.</param>
	/// <returns>
	/// A list of tasks where the first task completes when the first source task (any) completes,
	/// the second task completes when the second source task completes, and so on.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <para>
	///     This method is useful when you want to process task results in <b>completion order</b> rather than
	///     the order they were started. This enables progressive UI updates and improves perceived performance.
	///     </para>
	///     <para>
	///         <b>Example: Progressive image loading in Blazor</b>
	///     </para>
	///     <code>
	///     // Start multiple downloads in parallel
	///     var downloadTasks = imageUrls.Select(url => DownloadImageAsync(url));
	///     
	///     // Process results as they complete (fastest first)
	///     foreach (var imageTask in downloadTasks.OrderByCompletion())
	///     {
	///         var image = await imageTask;
	///         images.Add(image);
	///         await InvokeAsync(StateHasChanged);  // Update UI immediately
	///     }
	///     
	///     // Without OrderByCompletion, you'd wait for imageUrls[0] to finish
	///     // before showing imageUrls[1], even if imageUrls[1] completes first.
	///     </code>
	///     <para>
	///         <b>Performance comparison:</b>
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>
	///             <b>Without OrderByCompletion:</b> If the first URL takes 5 seconds and the second takes 0.5 seconds,
	///             the user waits 5 seconds before seeing <em>any</em> image.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///             <b>With OrderByCompletion:</b> The user sees the first image after 0.5 seconds, dramatically
	///             improving perceived performance.
	///             </description>
	///         </item>
	///     </list>
	///     <para>
	///     <b>Implementation note:</b> This method uses <see cref="TaskCompletionSource{TResult}"/> and
	///     <see cref="Interlocked.Increment(ref int)"/> to efficiently map completed tasks to output slots
	///     without locking. See inline comments for details.
	///     </para>
	/// </remarks>
	public static List<Task<T>> OrderByCompletion<T>(this IEnumerable<Task<T>> @this)
	{
		ArgumentNullException.ThrowIfNull(@this);

		// Materialize the input sequence into an array so we know the exact count
		// and can create fixed-size arrays for bookkeeping.
		Task<T>[] taskArray = @this.ToArray();
		int numTasks = taskArray.Length;

		// Create an array of TaskCompletionSources that will be completed in order.
		// Each TCS represents one "slot" in the completion order.
		var tcs = new TaskCompletionSource<T>[numTasks];

		// Create the result list of Tasks that will complete in the order their source tasks finish.
		// This is what we return to the caller.
		var ret = new List<Task<T>>(numTasks);

		// Shared counter (accessed atomically via Interlocked.Increment).
		// Each time a task completes, this counter is incremented to claim the next "slot"
		// in the completion order. Starting at -1 because Interlocked.Increment returns
		// the incremented value (so first call returns 0, second returns 1, etc.).
		int lastIndex = -1;

		for (int i = 0; i != numTasks; ++i)
		{
			// Create a TCS for this slot in the completion order.
			tcs[i] = new TaskCompletionSource<T>();

			// Add the TCS's Task to the result list.
			// These tasks will complete in order as source tasks finish.
			ret.Add(tcs[i].Task);

			// Attach a continuation to the source task.
			// When this source task completes (in any order), the Continuation method is called.
			// ExecuteSynchronously: run the continuation immediately on the completing thread (avoid thread-pool overhead).
			// DenyChildAttach: prevent child tasks from attaching to the continuation.
			taskArray[i]
				.ContinueWith(
					Continuation,
					CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.DenyChildAttach,
					TaskScheduler.Default);
		}

		return ret;

		// Local function that runs when a source task completes.
		void Continuation(Task<T> task)
		{
			// Atomically increment lastIndex and get the new value.
			// This "claims" the next slot in the completion order.
			// Thread-safe: multiple tasks can complete simultaneously, but each gets a unique index.
			int index = Interlocked.Increment(ref lastIndex);

			// Complete the TCS at the claimed index with the result/state of the completed task.
			// This propagates the result, exception, or cancellation to the corresponding output task.
			tcs[index].TryCompleteFromCompletedTask(task);
		}
	}

	/// <summary>
	/// Returns a new collection of tasks that complete in the order their source tasks complete.
	/// </summary>
	/// <param name="this">The tasks to reorder by completion.</param>
	/// <returns>
	/// A list of tasks where the first task completes when the first source task (any) completes,
	/// the second task completes when the second source task completes, and so on.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
	/// <remarks>
	///     <inheritdoc cref="OrderByCompletion{T}(IEnumerable{Task{T}})"
	///                 path="/remarks"/>
	/// </remarks>
	public static List<Task> OrderByCompletion(this IEnumerable<Task> @this)
	{
		ArgumentNullException.ThrowIfNull(@this);

		// Materialize the input sequence into an array.
		Task[] taskArray = @this.ToArray();
		int numTasks = taskArray.Length;

		// For non-generic tasks, we use TaskCompletionSource<object?> since we don't have a result type.
		// The null result is provided by NullResultFunc when completing the TCS.
		var tcs = new TaskCompletionSource<object?>[numTasks];
		var ret = new List<Task>(numTasks);

		// Shared counter for claiming completion order slots (accessed via Interlocked.Increment).
		int lastIndex = -1;

		// Local function that runs when a source task completes.
		void Continuation(Task task)
		{
			// Atomically claim the next slot in completion order.
			int index = Interlocked.Increment(ref lastIndex);

			// Complete the TCS with the task's state (success/exception/cancellation).
			// NullResultFunc provides the dummy result (null) since Task (non-generic) has no result.
			tcs[index].TryCompleteFromCompletedTask(task, NullResultFunc);
		}

		for (int i = 0; i != numTasks; ++i)
		{
			// Create a TCS for each completion order slot.
			tcs[i] = new TaskCompletionSource<object?>();

			// Add the TCS's Task to the result list.
			ret.Add(tcs[i].Task);

			// Attach continuation to the source task.
			taskArray[i]
				.ContinueWith(
					Continuation,
					CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.DenyChildAttach,
					TaskScheduler.Default);
		}

		return ret;
	}

	/// <summary>
	/// A cached delegate that returns <see langword="null"/>, used by <see cref="OrderByCompletion(IEnumerable{Task})"/>.
	/// </summary>
	private static Func<object?> NullResultFunc { get; } = static () => null;

	#endregion
}
