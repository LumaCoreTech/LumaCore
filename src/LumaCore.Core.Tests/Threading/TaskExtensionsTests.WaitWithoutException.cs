// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using static LumaCore.Core.Tests.AsyncTestHelpers;

using TaskExtensions = LumaCore.Core.Threading.TaskExtensions;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable MethodHasAsyncOverload
// ReSharper disable MethodSupportsCancellation

namespace LumaCore.Core.Tests.Threading;

public partial class TaskExtensionsTests
{
	#region WaitWithoutException(Task)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitWithoutException(Task)"/>
	/// completes without exception for a successfully completed task.
	/// </summary>
	[Fact]
	public void WaitWithoutException_WhenTaskSucceeds_Completes()
	{
		// Arrange
		var task = Task.CompletedTask;

		// Act + Assert (no exception)
		TaskExtensions.WaitWithoutException(task);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitWithoutException(Task)"/>
	/// swallows exceptions for a faulted task.
	/// </summary>
	[Fact]
	public void WaitWithoutException_WhenTaskFaults_SwallowsException()
	{
		// Arrange
		Task task = Task.FromException(new InvalidOperationException("Test"));

		// Act + Assert (no exception thrown)
		TaskExtensions.WaitWithoutException(task);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitWithoutException(Task)"/>
	/// swallows cancellation for a canceled task.
	/// </summary>
	[Fact]
	public void WaitWithoutException_WhenTaskCanceled_SwallowsCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Task task = Task.FromCanceled(cts.Token);

		// Act + Assert (no exception thrown)
		TaskExtensions.WaitWithoutException(task);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitWithoutException(Task)"/>
	/// throws <see cref="ArgumentNullException"/> when the task is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void WaitWithoutException_WhenTaskIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		Task? task = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => TaskExtensions.WaitWithoutException(task!));
		Assert.Equal("task", ex.ParamName);
	}

	#endregion

	#region WaitWithoutException(Task, CancellationToken)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitWithoutException(Task, CancellationToken)"/>
	/// completes without exception for a successfully completed task.
	/// </summary>
	[Fact]
	public void WaitWithoutException_WithCancellationToken_WhenTaskSucceeds_Completes()
	{
		// Arrange
		var task = Task.CompletedTask;
		using var cts = new CancellationTokenSource();

		// Act + Assert (no exception)
		TaskExtensions.WaitWithoutException(task, cts.Token);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitWithoutException(Task, CancellationToken)"/>
	/// swallows task cancellation but not token cancellation.
	/// </summary>
	[Fact]
	public void WaitWithoutException_WithCancellationToken_WhenTaskCanceled_SwallowsTaskCancellation()
	{
		// Arrange
		using var taskCts = new CancellationTokenSource();
		taskCts.Cancel();
		Task task = Task.FromCanceled(taskCts.Token);
		using var waitCts = new CancellationTokenSource();

		// Act + Assert (no exception - task cancellation is swallowed)
		TaskExtensions.WaitWithoutException(task, waitCts.Token);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitWithoutException(Task, CancellationToken)"/>
	/// swallows task exceptions but throws when the cancellation token is canceled.
	/// </summary>
	[Fact]
	public async Task WaitWithoutException_WithCancellationToken_WhenTokenCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		var tcs = new TaskCompletionSource();
		using var cts = new CancellationTokenSource();

		// Schedule cancellation
		Task cancelTask = Task.Run(async () =>
		{
			await Task.Delay(50).ConfigureAwait(false);
			cts.Cancel();
		});

		// Act + Assert
		Assert.ThrowsAny<OperationCanceledException>(() =>
			TaskExtensions.WaitWithoutException(tcs.Task, cts.Token));
		await AwaitWithTimeoutAsync(cancelTask, "Cancel task did not complete");
	}

	#endregion
}
