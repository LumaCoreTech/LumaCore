// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core;

/// <summary>
/// Terminates the application after allowing log queues to flush.
/// </summary>
/// <remarks>
/// Subscribe to <see cref="TerminationRequested"/> to flush logs before termination.
/// Subscribe to <see cref="BeforeTermination"/> and set <see cref="FailFastEventArgs.Cancel"/> for unit testing.
/// </remarks>
public static class FailFast
{
	private static readonly Lock                             sLock = new();
	private static          Action<string, Exception?>?      sTerminationRequested;
	private static          EventHandler<FailFastEventArgs>? sBeforeTermination;

	/// <summary>
	/// Terminates the application with the specified message. Never returns normally.
	/// </summary>
	/// <param name="message">The reason for termination.</param>
	/// <exception cref="FailFastCanceledException">Thrown if <see cref="BeforeTermination"/> cancels termination.</exception>
	/// <remarks>
	/// >
	/// Use this method to terminate the application when continuing execution is unsafe.
	/// </remarks>
	public static void TerminateApplication(string message)
	{
		lock (sLock)
		{
			// Check for cancellation BEFORE calling TerminationRequested
			var beforeArgs = new FailFastEventArgs(message, null);
			sBeforeTermination?.Invoke(null, beforeArgs);

			if (beforeArgs.Cancel)
				throw new FailFastCanceledException(message, null);

			// Not canceled - proceed with actual termination
			sTerminationRequested?.Invoke(message, null);
			Environment.FailFast(message);
		}
	}

	/// <summary>
	/// Terminates the application due to an exception. Never returns normally.
	/// </summary>
	/// <param name="exception">The exception that caused termination.</param>
	/// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
	/// <exception cref="FailFastCanceledException">Thrown if <see cref="BeforeTermination"/> cancels termination.</exception>
	/// <remarks>
	/// >
	/// Use this method to terminate the application when continuing execution is unsafe.
	/// </remarks>
	public static void TerminateApplication(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		lock (sLock)
		{
			// Check for cancellation BEFORE calling TerminationRequested
			var beforeArgs = new FailFastEventArgs(exception.Message, exception);
			sBeforeTermination?.Invoke(null, beforeArgs);

			if (beforeArgs.Cancel)
				throw new FailFastCanceledException(exception.Message, exception);

			// Not canceled - proceed with actual termination
			sTerminationRequested?.Invoke(exception.Message, exception);
			Environment.FailFast(exception.Message, exception);
		}
	}

	/// <summary>
	/// Raised before termination to allow flushing log queues and other cleanup.
	/// </summary>
	public static event Action<string, Exception?>? TerminationRequested
	{
		add
		{
			lock (sLock) { sTerminationRequested += value; }
		}
		remove
		{
			lock (sLock) { sTerminationRequested -= value; }
		}
	}

	/// <summary>
	/// Raised immediately before <see cref="Environment.FailFast(string)"/>. Set <see cref="FailFastEventArgs.Cancel"/>
	/// to <see langword="true"/> to throw <see cref="FailFastCanceledException"/> instead (for testing).
	/// </summary>
	/// <remarks>
	/// <b>Usage:</b> This event is intended for unit testing only. Production code should not subscribe to this event!
	/// Code using <see cref="TerminateApplication(string)"/> or <see cref="TerminateApplication(Exception)"/> expects
	/// that the application will terminate. Usually the application is then in a state where it cannot continue safely.
	/// </remarks>
	public static event EventHandler<FailFastEventArgs>? BeforeTermination
	{
		add
		{
			lock (sLock) { sBeforeTermination += value; }
		}
		remove
		{
			lock (sLock) { sBeforeTermination -= value; }
		}
	}
}
