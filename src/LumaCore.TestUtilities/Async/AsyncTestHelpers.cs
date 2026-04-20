// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.TestUtilities.Async;

/// <summary>
/// Provides helper methods for async test scenarios to prevent deadlocks and improve test reliability.
/// </summary>
/// <remarks>
/// Use these helpers for <i>explicit waits</i> — operations where the test intentionally waits for
/// something (e.g. <c>WaitAsync</c> on an async primitive, awaiting a task that should throw on
/// cancellation). Routine awaits (property access, queue enqueues, signal sets) are covered by the
/// global xUnit timeout configured in <c>xunit.runner.json</c> and do not need this wrapper.
/// </remarks>
public static class AsyncTestHelpers
{
	/// <summary>
	/// Default timeout for async operations in tests. Prevents tests from hanging indefinitely if the
	/// implementation is broken.
	/// </summary>
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Awaits a task with a timeout to prevent deadlocks in tests. If the task does not complete within
	/// the timeout, the assertion fails with a descriptive message.
	/// </summary>
	/// <param name="task">The task to await.</param>
	/// <param name="message">Optional message to display on timeout.</param>
	/// <param name="timeout">
	/// Optional custom timeout. If not specified, <see cref="DefaultTimeout"/> is used.
	/// </param>
	/// <exception cref="Xunit.Sdk.XunitException"><paramref name="task"/> does not complete within the timeout.</exception>
	public static async Task AwaitWithTimeoutAsync(Task task, string? message = null, TimeSpan? timeout = null)
	{
		TimeSpan actualTimeout = timeout ?? DefaultTimeout;
		bool completed = await Task.WhenAny(task, Task.Delay(actualTimeout)).ConfigureAwait(false) == task;
		if (!completed) Assert.Fail(message ?? "Operation timed out - possible deadlock");
		await task.ConfigureAwait(false); // Propagate any exceptions
	}

	/// <summary>
	/// Awaits a task with a timeout and returns the result. If the task does not complete within the timeout,
	/// the assertion fails with a descriptive message.
	/// </summary>
	/// <typeparam name="T">The result type of the task.</typeparam>
	/// <param name="task">The task to await.</param>
	/// <param name="message">Optional message to display on timeout.</param>
	/// <param name="timeout">
	/// Optional custom timeout. If not specified, <see cref="DefaultTimeout"/> is used.
	/// </param>
	/// <returns>The result of the completed task.</returns>
	/// <exception cref="Xunit.Sdk.XunitException"><paramref name="task"/> does not complete within the timeout.</exception>
	public static async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task, string? message = null, TimeSpan? timeout = null)
	{
		TimeSpan actualTimeout = timeout ?? DefaultTimeout;
		bool completed = await Task.WhenAny(task, Task.Delay(actualTimeout)).ConfigureAwait(false) == task;
		if (!completed) Assert.Fail(message ?? "Operation timed out - possible deadlock");
		return await task.ConfigureAwait(false); // Propagate any exceptions and return result
	}
}
