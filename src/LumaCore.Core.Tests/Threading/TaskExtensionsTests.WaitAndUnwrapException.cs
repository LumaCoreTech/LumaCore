// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using static LumaCore.TestUtilities.Async.AsyncTestHelpers;

using TaskExtensions = LumaCore.Core.Threading.TaskExtensions;

// ReSharper disable MethodSupportsCancellation
// ReSharper disable MethodHasAsyncOverload
// ReSharper disable AccessToDisposedClosure

namespace LumaCore.Core.Tests.Threading;

public partial class TaskExtensionsTests
{
	#region WaitAndUnwrapException(Task)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException(Task)"/>
	/// completes without exception for a successfully completed task.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_WhenTaskSucceeds_Completes()
	{
		// Arrange
		var task = Task.CompletedTask;

		// Act + Assert (no exception)
		TaskExtensions.WaitAndUnwrapException(task);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException(Task)"/>
	/// throws the original exception, not <see cref="AggregateException"/>.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_WhenTaskFaults_ThrowsOriginalException()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");
		Task task = Task.FromException(exception);

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() =>
			TaskExtensions.WaitAndUnwrapException(task));
		Assert.Equal("Test exception", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException(Task)"/>
	/// throws <see cref="OperationCanceledException"/> for a canceled task.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_WhenTaskCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Task task = Task.FromCanceled(cts.Token);

		// Act + Assert
		Assert.ThrowsAny<OperationCanceledException>(() => TaskExtensions.WaitAndUnwrapException(task));
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException(Task)"/>
	/// throws <see cref="ArgumentNullException"/> when the task is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_WhenTaskIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		Task? task = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
			TaskExtensions.WaitAndUnwrapException(task!));
		Assert.Equal("task", ex.ParamName);
	}

	#endregion

	#region WaitAndUnwrapException(Task, CancellationToken)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException(Task, CancellationToken)"/>
	/// completes without exception for a successfully completed task.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_WithCancellationToken_WhenTaskSucceeds_Completes()
	{
		// Arrange
		var task = Task.CompletedTask;
		using var cts = new CancellationTokenSource();

		// Act + Assert (no exception)
		TaskExtensions.WaitAndUnwrapException(task, cts.Token);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException(Task, CancellationToken)"/>
	/// unwraps and throws the original exception when the task faults.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_WithCancellationToken_WhenTaskFaults_ThrowsOriginalException()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");
		Task task = Task.FromException(exception);
		using var cts = new CancellationTokenSource();

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() =>
			TaskExtensions.WaitAndUnwrapException(task, cts.Token));
		Assert.Equal("Test exception", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException(Task, CancellationToken)"/>
	/// throws <see cref="OperationCanceledException"/> when the token is canceled.
	/// </summary>
	[Fact]
	public async Task WaitAndUnwrapException_WithCancellationToken_WhenTokenCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		var tcs = new TaskCompletionSource();
		using var cts = new CancellationTokenSource();

		// Schedule cancellation
		Task cancelTask = Task.Run(async () =>
		{
			await Task.Delay(50);
			cts.Cancel();
		});

		// Act + Assert
		Assert.ThrowsAny<OperationCanceledException>(() =>
			TaskExtensions.WaitAndUnwrapException(tcs.Task, cts.Token));
		await AwaitWithTimeoutAsync(cancelTask, "Cancel task did not complete");
	}

	#endregion

	#region WaitAndUnwrapException<TResult>(Task<TResult>)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException{TResult}(Task{TResult})"/>
	/// returns the result for a successfully completed task.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_Generic_WhenTaskSucceeds_ReturnsResult()
	{
		// Arrange
		Task<int> task = Task.FromResult(42);

		// Act
		int result = TaskExtensions.WaitAndUnwrapException(task);

		// Assert
		Assert.Equal(42, result);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException{TResult}(Task{TResult})"/>
	/// throws the original exception, not <see cref="AggregateException"/>.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_Generic_WhenTaskFaults_ThrowsOriginalException()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");
		Task<int> task = Task.FromException<int>(exception);

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() =>
			TaskExtensions.WaitAndUnwrapException(task));
		Assert.Equal("Test exception", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException{TResult}(Task{TResult})"/>
	/// throws <see cref="ArgumentNullException"/> when the task is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_Generic_WhenTaskIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		Task<int>? task = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
			TaskExtensions.WaitAndUnwrapException(task!));
		Assert.Equal("task", ex.ParamName);
	}

	#endregion

	#region WaitAndUnwrapException<TResult>(Task<TResult>, CancellationToken)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException{TResult}(Task{TResult}, CancellationToken)"/>
	/// returns the result for a successfully completed task.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_Generic_WithCancellationToken_WhenTaskSucceeds_ReturnsResult()
	{
		// Arrange
		Task<int> task = Task.FromResult(42);
		using var cts = new CancellationTokenSource();

		// Act
		int result = TaskExtensions.WaitAndUnwrapException(task, cts.Token);

		// Assert
		Assert.Equal(42, result);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException{TResult}(Task{TResult}, CancellationToken)"/>
	/// throws the original exception when the task faults.
	/// </summary>
	[Fact]
	public void WaitAndUnwrapException_Generic_WithCancellationToken_WhenTaskFaults_ThrowsOriginalException()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");
		Task<int> task = Task.FromException<int>(exception);
		using var cts = new CancellationTokenSource();

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() =>
			TaskExtensions.WaitAndUnwrapException(task, cts.Token));
		Assert.Equal("Test exception", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WaitAndUnwrapException{TResult}(Task{TResult}, CancellationToken)"/>
	/// throws <see cref="OperationCanceledException"/> when the token is canceled.
	/// </summary>
	[Fact]
	public async Task
		WaitAndUnwrapException_Generic_WithCancellationToken_WhenTokenCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		using var cts = new CancellationTokenSource();

		// Schedule cancellation
		Task cancelTask = Task.Run(async () =>
		{
			await Task.Delay(50);
			cts.Cancel();
		});

		// Act + Assert
		Assert.ThrowsAny<OperationCanceledException>(() =>
			TaskExtensions.WaitAndUnwrapException(tcs.Task, cts.Token));
		await AwaitWithTimeoutAsync(cancelTask, "Cancel task did not complete");
	}

	#endregion
}
