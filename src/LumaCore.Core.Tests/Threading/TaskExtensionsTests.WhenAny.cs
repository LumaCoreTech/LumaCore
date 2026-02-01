// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using TaskExtensions = LumaCore.Core.Threading.TaskExtensions;

// ReSharper disable MethodHasAsyncOverload

namespace LumaCore.Core.Tests.Threading;

public partial class TaskExtensionsTests
{
	#region WhenAny<TResult>(IEnumerable<Task<TResult>>)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAny{TResult}(IEnumerable{Task{TResult}})"/>
	/// returns the first completed task.
	/// </summary>
	[Fact]
	public async Task WhenAny_Generic_ReturnsFirstCompletedTask()
	{
		// Arrange
		var tcs1 = new TaskCompletionSource<int>();
		var tcs2 = new TaskCompletionSource<int>();
		IEnumerable<Task<int>> tasks = [tcs1.Task, tcs2.Task];

		// Act
		tcs1.SetResult(42);
		Task<int> completedTask = await TaskExtensions.WhenAny(tasks);

		// Assert
		Assert.Same(tcs1.Task, completedTask);
		Assert.Equal(42, await completedTask);

		// Cleanup
		tcs2.SetResult(0);
	}

	#endregion

	#region WhenAny(IEnumerable<Task>)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAny(IEnumerable{Task})"/>
	/// returns the first completed task.
	/// </summary>
	[Fact]
	public async Task WhenAny_ReturnsFirstCompletedTask()
	{
		// Arrange
		var tcs1 = new TaskCompletionSource();
		var tcs2 = new TaskCompletionSource();
		IEnumerable<Task> tasks = [tcs1.Task, tcs2.Task];

		// Act
		tcs1.SetResult();
		Task completedTask = await TaskExtensions.WhenAny(tasks);

		// Assert
		Assert.Same(tcs1.Task, completedTask);

		// Cleanup
		tcs2.SetResult();
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAny(IEnumerable{Task})"/>
	/// throws <see cref="ArgumentNullException"/> when the collection is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void WhenAny_WhenCollectionIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		IEnumerable<Task>? tasks = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => { _ = TaskExtensions.WhenAny(tasks!); });
		Assert.Equal("@this", ex.ParamName);
	}

	#endregion

	#region WhenAny(IEnumerable<Task>, CancellationToken)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAny(IEnumerable{Task}, CancellationToken)"/>
	/// returns the first completed task before cancellation.
	/// </summary>
	[Fact]
	public async Task WhenAny_WithCancellationToken_ReturnsFirstCompletedTask()
	{
		// Arrange
		var tcs = new TaskCompletionSource();
		using var cts = new CancellationTokenSource();
		IEnumerable<Task> tasks = [tcs.Task];

		// Act
		tcs.SetResult();
		Task completedTask = await TaskExtensions.WhenAny(tasks, cts.Token);

		// Assert
		Assert.Same(tcs.Task, completedTask);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAny(IEnumerable{Task}, CancellationToken)"/>
	/// throws <see cref="OperationCanceledException"/> when the token is canceled.
	/// </summary>
	[Fact]
	public async Task WhenAny_WithCancellationToken_WhenCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		var tcs = new TaskCompletionSource();
		using var cts = new CancellationTokenSource();
		IEnumerable<Task> tasks = [tcs.Task];

		// Act
		cts.Cancel();

		// Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			TaskExtensions.WhenAny(tasks, cts.Token));

		// Cleanup
		tcs.SetResult();
	}

	#endregion

	#region WhenAny<TResult>(IEnumerable<Task<TResult>>, CancellationToken)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAny{TResult}(IEnumerable{Task{TResult}}, CancellationToken)"/>
	/// returns the first completed task before cancellation.
	/// </summary>
	[Fact]
	public async Task WhenAny_Generic_WithCancellationToken_ReturnsFirstCompletedTask()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		using var cts = new CancellationTokenSource();
		IEnumerable<Task<int>> tasks = [tcs.Task];

		// Act
		tcs.SetResult(42);
		Task<int> completedTask = await TaskExtensions.WhenAny(tasks, cts.Token);

		// Assert
		Assert.Same(tcs.Task, completedTask);
		Assert.Equal(42, await completedTask);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAny{TResult}(IEnumerable{Task{TResult}}, CancellationToken)"/>
	/// throws <see cref="OperationCanceledException"/> when the token is canceled.
	/// </summary>
	[Fact]
	public async Task WhenAny_Generic_WithCancellationToken_WhenCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		using var cts = new CancellationTokenSource();
		IEnumerable<Task<int>> tasks = [tcs.Task];

		// Act
		cts.Cancel();

		// Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			TaskExtensions.WhenAny(tasks, cts.Token));

		// Cleanup
		tcs.SetResult(0);
	}

	#endregion
}
