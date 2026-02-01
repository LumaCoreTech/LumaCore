// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

public partial class TaskCompletionSourceExtensionsTests
{
	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.CreateAsyncTaskSource{TResult}"/>
	/// creates a new <see cref="TaskCompletionSource{TResult}"/> with an incomplete task.
	/// </summary>
	[Fact]
	public void CreateAsyncTaskSource_ReturnsTaskCompletionSourceWithIncompleteTask()
	{
		// Arrange + Act
		TaskCompletionSource<int> tcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<int>();

		// Assert
		Assert.NotNull(tcs);
		Assert.False(tcs.Task.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.CreateAsyncTaskSource{TResult}"/>
	/// creates a TCS that can be completed with a result.
	/// </summary>
	[Fact]
	public void CreateAsyncTaskSource_CanBeCompletedWithResult()
	{
		// Arrange
		TaskCompletionSource<string> tcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<string>();

		// Act
		tcs.SetResult("test value");

		// Assert
		Assert.True(tcs.Task.IsCompletedSuccessfully);
#pragma warning disable xUnit1031
		Assert.Equal("test value", tcs.Task.Result);
#pragma warning restore xUnit1031
	}

	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.CreateAsyncTaskSource{TResult}"/>
	/// creates a TCS configured with <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>.
	/// </summary>
	[Fact]
	public void CreateAsyncTaskSource_HasRunContinuationsAsynchronouslyOption()
	{
		// Arrange + Act
		TaskCompletionSource<int> tcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<int>();

		// Assert - verify by checking that continuation runs on a different context
		// This is tested indirectly through the library's usage patterns
		Assert.NotNull(tcs.Task);
		Assert.Equal(TaskCreationOptions.RunContinuationsAsynchronously, tcs.Task.CreationOptions);
	}

	/// <summary>
	/// Verifies that <see cref="TaskCompletionSourceExtensions.CreateAsyncTaskSource{TResult}"/>
	/// creates multiple independent instances.
	/// </summary>
	[Fact]
	public void CreateAsyncTaskSource_CreatesIndependentInstances()
	{
		// Arrange + Act
		TaskCompletionSource<int> tcs1 = TaskCompletionSourceExtensions.CreateAsyncTaskSource<int>();
		TaskCompletionSource<int> tcs2 = TaskCompletionSourceExtensions.CreateAsyncTaskSource<int>();

		// Assert
		Assert.NotSame(tcs1, tcs2);
		Assert.NotSame(tcs1.Task, tcs2.Task);
	}
}
