// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

using TaskExtensions = LumaCore.Core.Threading.TaskExtensions;

namespace LumaCore.Core.Tests.Threading;

public partial class TaskExtensionsTests
{
	#region WhenAll(IEnumerable<Task>)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAll(IEnumerable{Task})"/>
	/// completes when all tasks complete.
	/// </summary>
	[Fact]
	public async Task WhenAll_WhenAllTasksComplete_Completes()
	{
		// Arrange
		var tcs1 = new TaskCompletionSource();
		var tcs2 = new TaskCompletionSource();
		var tcs3 = new TaskCompletionSource();
		IEnumerable<Task> tasks = [tcs1.Task, tcs2.Task, tcs3.Task];

		// Act
		Task whenAllTask = TaskExtensions.WhenAll(tasks);
		tcs1.SetResult();
		tcs2.SetResult();

		// Still not complete
		Assert.False(whenAllTask.IsCompleted);

		tcs3.SetResult();
		await whenAllTask;

		// Assert
		Assert.True(whenAllTask.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAll(IEnumerable{Task})"/>
	/// completes immediately for an empty collection.
	/// </summary>
	[Fact]
	public async Task WhenAll_WithEmptyCollection_CompletesImmediately()
	{
		// Arrange
		IEnumerable<Task> tasks = [];

		// Act
		Task whenAllTask = TaskExtensions.WhenAll(tasks);
		await whenAllTask;

		// Assert
		Assert.True(whenAllTask.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAll(IEnumerable{Task})"/>
	/// throws <see cref="ArgumentNullException"/> when the collection is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void WhenAll_WhenCollectionIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		IEnumerable<Task>? tasks = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => { _ = TaskExtensions.WhenAll(tasks!); });
		Assert.Equal("@this", ex.ParamName);
	}

	#endregion

	#region WhenAll<TResult>(IEnumerable<Task<TResult>>)

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAll{TResult}(IEnumerable{Task{TResult}})"/>
	/// returns all results when all tasks complete.
	/// </summary>
	[Fact]
	public async Task WhenAll_Generic_ReturnsAllResults()
	{
		// Arrange
		var tcs1 = new TaskCompletionSource<int>();
		var tcs2 = new TaskCompletionSource<int>();
		var tcs3 = new TaskCompletionSource<int>();
		IEnumerable<Task<int>> tasks = [tcs1.Task, tcs2.Task, tcs3.Task];

		// Act
		Task<int[]> whenAllTask = TaskExtensions.WhenAll(tasks);
		tcs1.SetResult(1);
		tcs2.SetResult(2);
		tcs3.SetResult(3);
		int[] results = await whenAllTask;

		// Assert
		Assert.Equal([1, 2, 3], results);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAll{TResult}(IEnumerable{Task{TResult}})"/>
	/// returns an empty array for an empty collection.
	/// </summary>
	[Fact]
	public async Task WhenAll_Generic_WithEmptyCollection_ReturnsEmptyArray()
	{
		// Arrange
		IEnumerable<Task<int>> tasks = [];

		// Act
		int[] results = await TaskExtensions.WhenAll(tasks);

		// Assert
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that <see cref="TaskExtensions.WhenAll{TResult}(IEnumerable{Task{TResult}})"/>
	/// throws <see cref="ArgumentNullException"/> when the collection is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void WhenAll_Generic_WhenCollectionIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		IEnumerable<Task<int>>? tasks = null;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => { _ = TaskExtensions.WhenAll(tasks!); });
		Assert.Equal("@this", ex.ParamName);
	}

	#endregion
}
