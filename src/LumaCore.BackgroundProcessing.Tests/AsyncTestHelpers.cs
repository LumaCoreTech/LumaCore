// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.BackgroundProcessing.Tests;

/// <summary>
/// Provides helper methods for async test scenarios to prevent deadlocks and improve test reliability.
/// </summary>
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
	/// <exception cref="Xunit.Sdk.TrueException">Thrown when the task does not complete within the timeout.</exception>
	public static async Task AwaitWithTimeoutAsync(Task task, string? message = null, TimeSpan? timeout = null)
	{
		TimeSpan actualTimeout = timeout ?? DefaultTimeout;
		bool completed = await Task.WhenAny(task, Task.Delay(actualTimeout)) == task;
		Assert.True(completed, message ?? "Operation timed out - possible deadlock");
		await task; // Propagate any exceptions
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
	/// <exception cref="Xunit.Sdk.TrueException">Thrown when the task does not complete within the timeout.</exception>
	public static async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task, string? message = null, TimeSpan? timeout = null)
	{
		TimeSpan actualTimeout = timeout ?? DefaultTimeout;
		bool completed = await Task.WhenAny(task, Task.Delay(actualTimeout)) == task;
		Assert.True(completed, message ?? "Operation timed out - possible deadlock");
		return await task; // Propagate any exceptions and return result
	}
}
