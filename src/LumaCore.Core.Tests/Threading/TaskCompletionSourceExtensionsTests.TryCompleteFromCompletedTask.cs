// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

public partial class TaskCompletionSourceExtensionsTests
{
	#region TryCompleteFromCompletedTask<TResult, TSourceResult>

	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult,TSourceResult}"/>
	/// sets the result when the source task completed successfully.
	/// </summary>
	[Fact]
	public void TryCompleteFromCompletedTask_WhenSourceSucceeded_SetsResult()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		Task<int> sourceTask = Task.FromResult(42);

		// Act
		bool result = tcs.TryCompleteFromCompletedTask(sourceTask);

		// Assert
		Assert.True(result);
		Assert.True(tcs.Task.IsCompletedSuccessfully);
#pragma warning disable xUnit1031
		Assert.Equal(42, tcs.Task.Result);
#pragma warning restore xUnit1031
	}

	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult,TSourceResult}"/>
	/// sets the exception when the source task faulted.
	/// </summary>
	[Fact]
	public async Task TryCompleteFromCompletedTask_WhenSourceFaulted_SetsException()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		var exception = new InvalidOperationException("Test exception");
		Task<int> sourceTask = Task.FromException<int>(exception);

		// Act
		bool result = tcs.TryCompleteFromCompletedTask(sourceTask);

		// Assert
		Assert.True(result);
		Assert.True(tcs.Task.IsFaulted);
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tcs.Task);
		Assert.Equal("Test exception", ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult,TSourceResult}"/>
	/// sets the canceled state when the source task was canceled.
	/// </summary>
	[Fact]
	public void TryCompleteFromCompletedTask_WhenSourceCanceled_SetsCanceled()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Task<int> sourceTask = Task.FromCanceled<int>(cts.Token);

		// Act
		bool result = tcs.TryCompleteFromCompletedTask(sourceTask);

		// Assert
		Assert.True(result);
		Assert.True(tcs.Task.IsCanceled);
	}

	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult,TSourceResult}"/>
	/// returns <see langword="false"/> when the TCS is already completed.
	/// </summary>
	[Fact]
	public void TryCompleteFromCompletedTask_WhenTcsAlreadyCompleted_ReturnsFalse()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		tcs.SetResult(100);
		Task<int> sourceTask = Task.FromResult(42);

		// Act
		bool result = tcs.TryCompleteFromCompletedTask(sourceTask);

		// Assert
		Assert.False(result);
#pragma warning disable xUnit1031
		Assert.Equal(100, tcs.Task.Result); // Original result unchanged
#pragma warning restore xUnit1031
	}

	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult,TSourceResult}"/>
	/// throws <see cref="ArgumentNullException"/> when the TCS is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void TryCompleteFromCompletedTask_WhenTcsIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		TaskCompletionSource<int>? tcs = null;
		Task<int> sourceTask = Task.FromResult(42);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => tcs!.TryCompleteFromCompletedTask(sourceTask));
		Assert.Equal("@this", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult,TSourceResult}"/>
	/// throws <see cref="ArgumentNullException"/> when the source task is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void TryCompleteFromCompletedTask_WhenTaskIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		Task<int>? sourceTask = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => tcs.TryCompleteFromCompletedTask(sourceTask!));
		Assert.Equal("task", ex.ParamName);
	}

	#endregion

	#region TryCompleteFromCompletedTask with resultFunc

	/// <summary>
	/// Verifies that
	/// <see
	///     cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult}(TaskCompletionSource{TResult}, Task, Func{TResult})"/>
	/// calls the result function when the source task completed successfully.
	/// </summary>
	[Fact]
	public void TryCompleteFromCompletedTask_WithResultFunc_WhenSourceSucceeded_CallsResultFunc()
	{
		// Arrange
		var tcs = new TaskCompletionSource<string>();
		var sourceTask = Task.CompletedTask;
		bool funcCalled = false;

		// Act
		bool result = tcs.TryCompleteFromCompletedTask(
			sourceTask,
			() =>
			{
				funcCalled = true;
				return "success";
			});

		// Assert
		Assert.True(result);
		Assert.True(funcCalled);
#pragma warning disable xUnit1031
		Assert.Equal("success", tcs.Task.Result);
#pragma warning restore xUnit1031
	}

	/// <summary>
	/// Verifies that
	/// <see
	///     cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult}(TaskCompletionSource{TResult}, Task, Func{TResult})"/>
	/// does not call the result function when the source task faulted.
	/// </summary>
	[Fact]
	public async Task TryCompleteFromCompletedTask_WithResultFunc_WhenSourceFaulted_DoesNotCallResultFunc()
	{
		// Arrange
		var tcs = new TaskCompletionSource<string>();
		Task sourceTask = Task.FromException(new InvalidOperationException("Test"));
		bool funcCalled = false;

		// Act
		bool result = tcs.TryCompleteFromCompletedTask(
			sourceTask,
			() =>
			{
				funcCalled = true;
				return "should not be called";
			});

		// Assert
		Assert.True(result);
		Assert.False(funcCalled);
		Assert.True(tcs.Task.IsFaulted);
		await Assert.ThrowsAsync<InvalidOperationException>(() => tcs.Task);
	}

	/// <summary>
	/// Verifies that
	/// <see
	///     cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult}(TaskCompletionSource{TResult}, Task, Func{TResult})"/>
	/// propagates cancellation when the source task was canceled.
	/// </summary>
	[Fact]
	public void TryCompleteFromCompletedTask_WithResultFunc_WhenSourceCanceled_SetsCanceled()
	{
		// Arrange
		var tcs = new TaskCompletionSource<string>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Task sourceTask = Task.FromCanceled(cts.Token);
		bool funcCalled = false;

		// Act
		bool result = tcs.TryCompleteFromCompletedTask(
			sourceTask,
			() =>
			{
				funcCalled = true;
				return "should not be called";
			});

		// Assert
		Assert.True(result);
		Assert.False(funcCalled);
		Assert.True(tcs.Task.IsCanceled);
	}

	/// <summary>
	/// Verifies that
	/// <see
	///     cref="TaskCompletionSourceExtensions.TryCompleteFromCompletedTask{TResult}(TaskCompletionSource{TResult}, Task, Func{TResult})"/>
	/// throws <see cref="ArgumentNullException"/> when the result function is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void TryCompleteFromCompletedTask_WithResultFunc_WhenFuncIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var tcs = new TaskCompletionSource<string>();
		var sourceTask = Task.CompletedTask;
		Func<string>? resultFunc = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => tcs.TryCompleteFromCompletedTask(sourceTask, resultFunc!));
		Assert.Equal("resultFunc", ex.ParamName);
	}

	#endregion
}
