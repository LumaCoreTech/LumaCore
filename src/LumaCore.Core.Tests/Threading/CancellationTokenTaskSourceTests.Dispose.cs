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
	/// Verifies that <see cref="CancellationTokenTaskSource{T}.Dispose"/> can be called
	/// without throwing exceptions.
	/// </summary>
	[Fact]
	public void Dispose_WhenCalled_DoesNotThrow()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var source = new CancellationTokenTaskSource<int>(cts.Token);

		// Act + Assert (no exception)
		source.Dispose();
	}

	/// <summary>
	/// Verifies that <see cref="CancellationTokenTaskSource{T}.Dispose"/> can be called
	/// multiple times safely (idempotent).
	/// </summary>
	[Fact]
	public void Dispose_CalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var source = new CancellationTokenTaskSource<int>(cts.Token);

		// Act + Assert (no exception)
		source.Dispose();
		source.Dispose();
		source.Dispose();
	}

	/// <summary>
	/// Verifies that after <see cref="CancellationTokenTaskSource{T}.Dispose"/> is called,
	/// canceling the token does not complete the task.
	/// </summary>
	[Fact]
	public async Task Dispose_ThenCancelToken_TaskRemainsIncomplete()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var source = new CancellationTokenTaskSource<int>(cts.Token);
		Task<int> task = source.Task;

		// Act
		source.Dispose();
		cts.Cancel();

		// Give time for any callbacks to fire
		await Task.Delay(50);

		// Assert - task should remain incomplete because registration was disposed
		Assert.False(task.IsCompleted);
	}

	/// <summary>
	/// Verifies that <see cref="CancellationTokenTaskSource{T}.Dispose"/> has no effect
	/// when the token was already canceled at construction.
	/// </summary>
	[Fact]
	public void Dispose_WhenTokenWasAlreadyCanceled_DoesNotThrow()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		var source = new CancellationTokenTaskSource<int>(cts.Token);

		// Act + Assert (no exception)
		source.Dispose();
		Assert.True(source.Task.IsCanceled);
	}
}
