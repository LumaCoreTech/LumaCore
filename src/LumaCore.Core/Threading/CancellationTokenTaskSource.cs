// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore
// Portions derived from Nito.AsyncEx (MIT, Copyright (c) 2014 StephenCleary) — see THIRD-PARTY-NOTICES.md

namespace LumaCore.Core.Threading;

/// <summary>
/// Wraps a <see cref="CancellationToken"/> in a <see cref="Task{TResult}"/> that completes (with a canceled state)
/// when the token is canceled.
/// </summary>
/// <typeparam name="T">
/// The result type of the task. Since the task always completes in the canceled state, this type is never actually
/// produced; it exists solely to allow the task to be used in generic contexts (e.g., <c>Task.WhenAny</c>).
/// </typeparam>
/// <remarks>
///     <para>
///     This class is useful when you need to combine cancellation with other async operations using
///     <c>Task.WhenAny(...)</c> or similar combinators. Instead of polling
///     <see cref="CancellationToken.IsCancellationRequested"/>, you can await the <see cref="Task"/> property
///     alongside other tasks.
///     </para>
///     <para>
///     <b>Important:</b> Disposing this instance unregisters the callback from the <see cref="CancellationToken"/>.
///     If the token has not yet been canceled, the <see cref="Task"/> will <b>never complete</b>. Only dispose
///     after you no longer need to observe the task.
///     </para>
///     <para>
///         <b>Example usage:</b>
///     </para>
///     <code>
///     using var cts = new CancellationTokenTaskSource&lt;bool&gt;(cancellationToken);
///     var completedTask = await Task.WhenAny(actualWorkTask, cts.Task);
///     if (completedTask == cts.Task)
///     {
///         // Cancellation was requested
///         throw new OperationCanceledException(cancellationToken);
///     }
///     </code>
/// </remarks>
/// <seealso cref="CancellationToken"/>
/// <seealso cref="TaskCompletionSource{TResult}"/>
public sealed class CancellationTokenTaskSource<T> : IDisposable
{
	/// <summary>
	/// The cancellation token registration that triggers task completion when the token is canceled.
	/// </summary>
	/// <remarks>
	/// This field is <see langword="null"/> if the token was already canceled at construction time
	/// (in which case a pre-canceled task is returned immediately without needing a registration).
	/// Disposing this registration is necessary to prevent the callback (and its closure over the
	/// <see cref="TaskCompletionSource{TResult}"/>) from being held alive by the source
	/// <see cref="CancellationTokenSource"/>.
	/// </remarks>
	private readonly CancellationTokenRegistration? mRegistration;

	/// <summary>
	/// Initializes a new instance of the <see cref="CancellationTokenTaskSource{T}"/> class
	/// that observes the specified cancellation token.
	/// </summary>
	/// <param name="cancellationToken">
	/// The cancellation token to observe. When this token is canceled, <see cref="Task"/> transitions
	/// to the <see cref="TaskStatus.Canceled"/> state.
	/// </param>
	/// <remarks>
	///     <para>
	///     If <paramref name="cancellationToken"/> is already canceled at the time of construction,
	///     <see cref="Task"/> is immediately set to a pre-canceled state and no registration is created.
	///     </para>
	///     <para>
	///     If the token cannot be canceled (<see cref="CancellationToken.CanBeCanceled"/> is <see langword="false"/>),
	///     the <see cref="Task"/> will never complete. Consider checking this condition before creating an instance.
	///     </para>
	/// </remarks>
	public CancellationTokenTaskSource(CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			Task = System.Threading.Tasks.Task.FromCanceled<T>(cancellationToken);
			return;
		}

		var tcs = new TaskCompletionSource<T>();
		mRegistration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken), false);
		Task = tcs.Task;
	}

	/// <summary>
	/// Unregisters the callback from the cancellation token and releases associated resources.
	/// </summary>
	/// <remarks>
	///     <para>
	///     After calling this method, if the cancellation token has not yet been canceled, the <see cref="Task"/>
	///     will <b>never complete</b>. Only dispose this instance after you no longer need to observe the task.
	///     </para>
	///     <para>
	///     This method is safe to call multiple times; subsequent calls have no effect.
	///     </para>
	/// </remarks>
	public void Dispose()
	{
		mRegistration?.Dispose();
	}

	/// <summary>
	/// Gets the task that completes when the source cancellation token is canceled.
	/// </summary>
	/// <value>
	/// A <see cref="Task{TResult}"/> that transitions to <see cref="TaskStatus.Canceled"/> when the
	/// <see cref="CancellationToken"/> passed to the constructor is canceled.
	/// </value>
	/// <remarks>
	/// The task never completes successfully or with an exception; it only ever cancels (or remains incomplete
	/// indefinitely if the token is never canceled or this instance is disposed prematurely).
	/// </remarks>
	public Task<T> Task { get; }
}
