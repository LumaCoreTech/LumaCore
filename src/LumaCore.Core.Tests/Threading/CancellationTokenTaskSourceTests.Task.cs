// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable MethodSupportsCancellation

namespace LumaCore.Core.Tests.Threading;

public partial class CancellationTokenTaskSourceTests
{
	/// <summary>
	/// Verifies that <see cref="CancellationTokenTaskSource{T}.Task"/> transitions to canceled
	/// when the cancellation token is canceled.
	/// </summary>
	[Fact]
	public async Task Task_WhenTokenIsCanceled_TransitionsToCanceled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		using var source = new CancellationTokenTaskSource<int>(cts.Token);

		// Act
		cts.Cancel();

		// Assert
		await Assert.ThrowsAsync<TaskCanceledException>(() => source.Task);
		Assert.True(source.Task.IsCanceled);
	}

	/// <summary>
	/// Verifies that the task's cancellation token matches the source token.
	/// </summary>
	[Fact]
	public async Task Task_WhenCanceled_ContainsCorrectCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		using var source = new CancellationTokenTaskSource<int>(cts.Token);

		// Act
		cts.Cancel();

		// Assert
		OperationCanceledException ex = await Assert.ThrowsAsync<TaskCanceledException>(() => source.Task);
		Assert.Equal(cts.Token, ex.CancellationToken);
	}

	/// <summary>
	/// Verifies that the task can be used with <see cref="Task.WhenAny(Task[])"/>
	/// to detect cancellation.
	/// </summary>
	[Fact]
	public async Task Task_UsedWithWhenAny_DetectsCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		using var source = new CancellationTokenTaskSource<bool>(cts.Token);
		Task<bool> neverCompletingTask = new TaskCompletionSource<bool>().Task;

		// Act
		cts.Cancel();
		Task completedTask = await Task.WhenAny(source.Task, neverCompletingTask);

		// Assert
		Assert.Same(source.Task, completedTask);
	}
}
