// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using TaskExtensions = LumaCore.Core.Threading.TaskExtensions;

// ReSharper disable MethodHasAsyncOverload

namespace LumaCore.Core.Tests.Threading;

public partial class TaskExtensionsTests
{
	#region OrderByCompletion(IEnumerable<Task>)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.OrderByCompletion(IEnumerable{Task})"/>
	/// returns tasks that complete in the order the source tasks complete.
	/// </summary>
	[Fact]
	public async Task OrderByCompletion_ReturnsTasksInCompletionOrder()
	{
		// Arrange - create 3 tasks that will complete in reverse order
		var tcs1 = new TaskCompletionSource();
		var tcs2 = new TaskCompletionSource();
		var tcs3 = new TaskCompletionSource();
		IEnumerable<Task> tasks = [tcs1.Task, tcs2.Task, tcs3.Task];

		// Act
		List<Task> orderedTasks = TaskExtensions.OrderByCompletion(tasks);

		// Complete in order: 3, 2, 1
		Assert.False(orderedTasks[0].IsCompleted);

		tcs3.SetResult();
		await orderedTasks[0];
		Assert.True(orderedTasks[0].IsCompleted);
		Assert.False(orderedTasks[1].IsCompleted);

		tcs2.SetResult();
		await orderedTasks[1];
		Assert.True(orderedTasks[1].IsCompleted);
		Assert.False(orderedTasks[2].IsCompleted);

		tcs1.SetResult();
		await orderedTasks[2];
		Assert.True(orderedTasks[2].IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.OrderByCompletion(IEnumerable{Task})"/>
	/// propagates exceptions correctly.
	/// </summary>
	[Fact]
	public async Task OrderByCompletion_PropagatesExceptions()
	{
		// Arrange
		var tcs1 = new TaskCompletionSource();
		var tcs2 = new TaskCompletionSource();
		IEnumerable<Task> tasks = [tcs1.Task, tcs2.Task];

		// Act
		List<Task> orderedTasks = TaskExtensions.OrderByCompletion(tasks);
		tcs1.SetException(new InvalidOperationException("Test error"));

		// Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => orderedTasks[0]);
		Assert.Equal("Test error", ex.Message);

		// Cleanup
		tcs2.SetResult();
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.OrderByCompletion(IEnumerable{Task})"/>
	/// returns an empty list for an empty input.
	/// </summary>
	[Fact]
	public void OrderByCompletion_WithEmptyCollection_ReturnsEmptyList()
	{
		// Arrange
		IEnumerable<Task> tasks = [];

		// Act
		List<Task> orderedTasks = TaskExtensions.OrderByCompletion(tasks);

		// Assert
		Assert.Empty(orderedTasks);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.OrderByCompletion(IEnumerable{Task})"/>
	/// throws <see cref="ArgumentNullException"/> when the collection is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void OrderByCompletion_WhenCollectionIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		IEnumerable<Task>? tasks = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => TaskExtensions.OrderByCompletion(tasks!));
		Assert.Equal("@this", ex.ParamName);
	}

	#endregion

	#region OrderByCompletion<T>(IEnumerable<Task<T>>)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.OrderByCompletion{T}(IEnumerable{Task{T}})"/>
	/// returns tasks that complete in the order the source tasks complete.
	/// </summary>
	[Fact]
	public async Task OrderByCompletion_Generic_ReturnsTasksInCompletionOrder()
	{
		// Arrange - create 3 tasks that will complete in reverse order (3, 2, 1)
		var tcs1 = new TaskCompletionSource<int>();
		var tcs2 = new TaskCompletionSource<int>();
		var tcs3 = new TaskCompletionSource<int>();
		IEnumerable<Task<int>> tasks = [tcs1.Task, tcs2.Task, tcs3.Task];

		// Act
		List<Task<int>> orderedTasks = TaskExtensions.OrderByCompletion(tasks);

		// Complete in order: 3, 2, 1
		tcs3.SetResult(3);
		int firstResult = await orderedTasks[0];

		tcs2.SetResult(2);
		int secondResult = await orderedTasks[1];

		tcs1.SetResult(1);
		int thirdResult = await orderedTasks[2];

		// Assert - results should be in completion order (3, 2, 1), not source order (1, 2, 3)
		Assert.Equal(3, firstResult);
		Assert.Equal(2, secondResult);
		Assert.Equal(1, thirdResult);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.OrderByCompletion{T}(IEnumerable{Task{T}})"/>
	/// propagates exceptions correctly.
	/// </summary>
	[Fact]
	public async Task OrderByCompletion_Generic_PropagatesExceptions()
	{
		// Arrange
		var tcs1 = new TaskCompletionSource<int>();
		var tcs2 = new TaskCompletionSource<int>();
		IEnumerable<Task<int>> tasks = [tcs1.Task, tcs2.Task];

		// Act
		List<Task<int>> orderedTasks = TaskExtensions.OrderByCompletion(tasks);
		tcs2.SetException(new InvalidOperationException("Test error"));

		// Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => orderedTasks[0]);
		Assert.Equal("Test error", ex.Message);

		// Cleanup
		tcs1.SetResult(1);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.OrderByCompletion{T}(IEnumerable{Task{T}})"/>
	/// propagates cancellation correctly.
	/// </summary>
	[Fact]
	public async Task OrderByCompletion_Generic_PropagatesCancellation()
	{
		// Arrange
		var tcs1 = new TaskCompletionSource<int>();
		var tcs2 = new TaskCompletionSource<int>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		IEnumerable<Task<int>> tasks = [tcs1.Task, tcs2.Task];

		// Act
		List<Task<int>> orderedTasks = TaskExtensions.OrderByCompletion(tasks);
		tcs2.SetCanceled(cts.Token);

		// Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => orderedTasks[0]);

		// Cleanup
		tcs1.SetResult(1);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.OrderByCompletion{T}(IEnumerable{Task{T}})"/>
	/// returns an empty list for an empty input.
	/// </summary>
	[Fact]
	public void OrderByCompletion_Generic_WithEmptyCollection_ReturnsEmptyList()
	{
		// Arrange
		IEnumerable<Task<int>> tasks = [];

		// Act
		List<Task<int>> orderedTasks = TaskExtensions.OrderByCompletion(tasks);

		// Assert
		Assert.Empty(orderedTasks);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.OrderByCompletion{T}(IEnumerable{Task{T}})"/>
	/// throws <see cref="ArgumentNullException"/> when the collection is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void OrderByCompletion_Generic_WhenCollectionIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		IEnumerable<Task<int>>? tasks = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => TaskExtensions.OrderByCompletion(tasks!));
		Assert.Equal("@this", ex.ParamName);
	}

	#endregion
}
