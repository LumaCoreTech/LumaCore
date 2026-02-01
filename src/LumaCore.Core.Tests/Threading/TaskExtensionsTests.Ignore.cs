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
	public async Task Ignore_WhenTaskSucceeds_DoesNotThrow()
	{
		// Arrange
		var tcs = new TaskCompletionSource();

		// Act
		TaskExtensions.Ignore(tcs.Task);
		tcs.SetResult();

		// Give time for continuation to run
		await Task.Delay(50);

		// Assert - no exception thrown, test passes
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore(Task)"/> swallows exceptions
	/// when the task faults.
	/// </summary>
	[Fact]
	public async Task Ignore_WhenTaskFaults_SwallowsException()
	{
		// Arrange
		var tcs = new TaskCompletionSource();

		// Act
		TaskExtensions.Ignore(tcs.Task);
		tcs.SetException(new InvalidOperationException("Test error"));

		// Give time for continuation to run
		await Task.Delay(50);

		// Assert - exception was swallowed, test passes
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore(Task)"/> swallows cancellation
	/// when the task is canceled.
	/// </summary>
	[Fact]
	public async Task Ignore_WhenTaskCanceled_SwallowsCancellation()
	{
		// Arrange
		var tcs = new TaskCompletionSource();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act
		TaskExtensions.Ignore(tcs.Task);
		tcs.SetCanceled(cts.Token);

		// Give time for continuation to run
		await Task.Delay(50);

		// Assert - cancellation was swallowed, test passes
	}

	#endregion

	#region Ignore<T>()

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore{T}(Task{T})"/> does not throw
	/// when the task completes successfully.
	/// </summary>
	[Fact]
	public async Task Ignore_Generic_WhenTaskSucceeds_DoesNotThrow()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();

		// Act
		TaskExtensions.Ignore(tcs.Task);
		tcs.SetResult(42);

		// Give time for continuation to run
		await Task.Delay(50);

		// Assert - no exception thrown, test passes
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore{T}(Task{T})"/> swallows exceptions
	/// when the task faults.
	/// </summary>
	[Fact]
	public async Task Ignore_Generic_WhenTaskFaults_SwallowsException()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();

		// Act
		TaskExtensions.Ignore(tcs.Task);
		tcs.SetException(new InvalidOperationException("Test error"));

		// Give time for continuation to run
		await Task.Delay(50);

		// Assert - exception was swallowed, test passes
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.Ignore{T}(Task{T})"/> swallows cancellation
	/// when the task is canceled.
	/// </summary>
	[Fact]
	public async Task Ignore_Generic_WhenTaskCanceled_SwallowsCancellation()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act
		TaskExtensions.Ignore(tcs.Task);
		tcs.SetCanceled(cts.Token);

		// Give time for continuation to run
		await Task.Delay(50);

		// Assert - cancellation was swallowed, test passes
	}

	#endregion
}
