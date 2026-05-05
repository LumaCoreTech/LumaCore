// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using TaskExtensions = LumaCore.Core.Threading.TaskExtensions;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable MethodSupportsCancellation
// ReSharper disable ReplaceAsyncWithTaskReturn

namespace LumaCore.Core.Tests.Threading;

public partial class TaskExtensionsTests
{
	#region Ignore()

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore(Task)"/> does not throw
	/// when the task completes successfully.
	/// </summary>
	[Fact]
	public void Ignore_WhenTaskSucceeds_DoesNotThrow()
	{
		// Arrange - complete the task before calling Ignore() so the async void continuation runs
		// synchronously on the calling thread; no Task.Delay synchronization needed.
		var tcs = new TaskCompletionSource();
		tcs.SetResult();

		// Act + Assert - no exception thrown
		TaskExtensions.Ignore(tcs.Task);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore(Task)"/> swallows exceptions
	/// when the task faults.
	/// </summary>
	[Fact]
	public void Ignore_WhenTaskFaults_SwallowsException()
	{
		// Arrange - complete the task before calling Ignore() so the async void continuation runs
		// synchronously on the calling thread; no Task.Delay synchronization needed.
		var tcs = new TaskCompletionSource();
		tcs.SetException(new InvalidOperationException("Test error"));

		// Act + Assert - exception was swallowed, no throw
		TaskExtensions.Ignore(tcs.Task);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore(Task)"/> swallows cancellation
	/// when the task is canceled.
	/// </summary>
	[Fact]
	public void Ignore_WhenTaskCanceled_SwallowsCancellation()
	{
		// Arrange - complete the task before calling Ignore() so the async void continuation runs
		// synchronously on the calling thread; no Task.Delay synchronization needed.
		var tcs = new TaskCompletionSource();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		tcs.SetCanceled(cts.Token);

		// Act + Assert - cancellation was swallowed, no throw
		TaskExtensions.Ignore(tcs.Task);
	}

	#endregion

	#region Ignore<T>()

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore{T}(Task{T})"/> does not throw
	/// when the task completes successfully.
	/// </summary>
	[Fact]
	public void Ignore_Generic_WhenTaskSucceeds_DoesNotThrow()
	{
		// Arrange - complete the task before calling Ignore() so the async void continuation runs
		// synchronously on the calling thread; no Task.Delay synchronization needed.
		var tcs = new TaskCompletionSource<int>();
		tcs.SetResult(42);

		// Act + Assert - no exception thrown
		TaskExtensions.Ignore(tcs.Task);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore{T}(Task{T})"/> swallows exceptions
	/// when the task faults.
	/// </summary>
	[Fact]
	public void Ignore_Generic_WhenTaskFaults_SwallowsException()
	{
		// Arrange - complete the task before calling Ignore() so the async void continuation runs
		// synchronously on the calling thread; no Task.Delay synchronization needed.
		var tcs = new TaskCompletionSource<int>();
		tcs.SetException(new InvalidOperationException("Test error"));

		// Act + Assert - exception was swallowed, no throw
		TaskExtensions.Ignore(tcs.Task);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore{T}(Task{T})"/> swallows cancellation
	/// when the task is canceled.
	/// </summary>
	[Fact]
	public void Ignore_Generic_WhenTaskCanceled_SwallowsCancellation()
	{
		// Arrange - complete the task before calling Ignore() so the async void continuation runs
		// synchronously on the calling thread; no Task.Delay synchronization needed.
		var tcs = new TaskCompletionSource<int>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		tcs.SetCanceled(cts.Token);

		// Act + Assert - cancellation was swallowed, no throw
		TaskExtensions.Ignore(tcs.Task);
	}

	#endregion
}
