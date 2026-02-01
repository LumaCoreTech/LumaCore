// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Threading;

using Xunit;

using static LumaCore.Core.Tests.AsyncTestHelpers;

// ReSharper disable AccessToDisposedClosure
// ReSharper disable MethodHasAsyncOverload
// ReSharper disable MethodSupportsCancellation

namespace LumaCore.Core.Tests.Threading;

public partial class AsyncManualResetEventTests
{
	#region Wait()

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Wait()"/> returns immediately when the event is already
	/// set.
	/// </summary>
	[Fact]
	public async Task Wait_WhenEventIsSet_ReturnsImmediately()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(true);

		// Act - wrap in Task.Run with timeout to prevent deadlock if implementation is broken
		Task waitTask = Task.Run(() => mre.Wait());
		await AwaitWithTimeoutAsync(waitTask, "Wait() did not return immediately on set event");

		// Assert
		Assert.True(mre.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Wait()"/> blocks until the event is set.
	/// </summary>
	[Fact]
	public async Task Wait_WhenEventIsNotSet_BlocksUntilSet()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);
		bool waitCompleted = false;

		// Act - wrap synchronous Wait in Task.Run to prevent blocking test runner
		Task setTask = Task.Run(async () =>
		{
			await Task.Delay(50).ConfigureAwait(false);
			mre.Set();
		});

		Task waitTask = Task.Run(() =>
		{
			mre.Wait();
			waitCompleted = true;
		});

		await AwaitWithTimeoutAsync(waitTask, "Wait() did not complete after Set() was called");
		await AwaitWithTimeoutAsync(setTask, "Set task timed out");

		// Assert
		Assert.True(waitCompleted);
	}

	#endregion

	#region Wait(CancellationToken)

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Wait(CancellationToken)"/> returns immediately when the
	/// event is already set, ignoring the cancellation token.
	/// </summary>
	[Fact]
	public async Task Wait_WithCancellationToken_WhenEventIsSet_ReturnsImmediately()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(true);
		using var cts = new CancellationTokenSource();

		// Act - wrap in Task.Run with timeout to prevent deadlock if implementation is broken
		Task waitTask = Task.Run(() => mre.Wait(cts.Token));
		await AwaitWithTimeoutAsync(waitTask, "Wait() did not return immediately on set event");

		// Assert
		Assert.True(mre.IsSet);
	}

	/// <summary>
	/// Verifies that <see cref="AsyncManualResetEvent.Wait(CancellationToken)"/> throws
	/// <see cref="OperationCanceledException"/> when the token is canceled before the event is set.
	/// </summary>
	[Fact]
	public async Task Wait_WithCancellationToken_WhenCanceled_ThrowsOperationCanceledException()
	{
		// Arrange
		var mre = new AsyncManualResetEvent(false);
		using var cts = new CancellationTokenSource();

		// Schedule cancellation
		Task cancelTask = Task.Run(async () =>
		{
			await Task.Delay(50).ConfigureAwait(false);
			cts.Cancel();
		});

		// Act + Assert - wrap synchronous Wait in Task.Run to prevent blocking test runner
		Task waitTask = Task.Run(() => mre.Wait(cts.Token));
		Task assertion = Assert.ThrowsAsync<OperationCanceledException>(() => waitTask);
		await AwaitWithTimeoutAsync(assertion, "Cancellation did not throw OperationCanceledException");
		await AwaitWithTimeoutAsync(cancelTask, "Cancellation task timed out");
	}

	#endregion
}
