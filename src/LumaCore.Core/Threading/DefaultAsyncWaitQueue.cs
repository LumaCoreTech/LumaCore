// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using LumaCore.Core.Collections;

namespace LumaCore.Core.Threading;

/// <summary>
/// The default wait queue implementation, which uses a double-ended queue.
/// </summary>
/// <typeparam name="T">The type of the results. If this isn't needed, use <see cref="object"/>.</typeparam>
[DebuggerDisplay("Count = {" + nameof(Count) + "}")]
[DebuggerTypeProxy(typeof(DefaultAsyncWaitQueue<>.DebugView))]
sealed class DefaultAsyncWaitQueue<T> : IAsyncWaitQueue<T>
{
	private readonly Deque<TaskCompletionSource<T>> mQueue = [];

	/// <inheritdoc/>
	public bool IsEmpty => Count == 0;

	private int Count => mQueue.Count;

	/// <inheritdoc/>
	public Task<T> Enqueue()
	{
		TaskCompletionSource<T> tcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<T>();
		mQueue.AddToBack(tcs);
		return tcs.Task;
	}

	/// <inheritdoc/>
	public void Dequeue(T? result = default)
	{
		mQueue.RemoveFromFront().TrySetResult(result!);
	}

	/// <inheritdoc/>
	public void DequeueAll(T? result = default)
	{
		foreach (TaskCompletionSource<T> source in mQueue)
		{
			source.TrySetResult(result!);
		}

		mQueue.Clear();
	}

	/// <inheritdoc/>
	public bool TryCancel(Task task, CancellationToken cancellationToken)
	{
		for (int i = 0; i != mQueue.Count; ++i)
		{
			if (mQueue[i].Task != task) continue;
			mQueue[i].TrySetCanceled(cancellationToken);
			mQueue.RemoveAt(i);
			return true;
		}

		return false;
	}

	/// <inheritdoc/>
	public void CancelAll(CancellationToken cancellationToken)
	{
		foreach (TaskCompletionSource<T> source in mQueue)
		{
			source.TrySetCanceled(cancellationToken);
		}

		mQueue.Clear();
	}

	[DebuggerNonUserCode]
	[ExcludeFromCodeCoverage]
	internal sealed class DebugView(DefaultAsyncWaitQueue<T> queue)
	{
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public Task<T>[] Tasks
		{
			get
			{
				var result = new List<Task<T>>(queue.mQueue.Count);
				result.AddRange(queue.mQueue.Select(entry => entry.Task));
				return [.. result];
			}
		}
	}
}
