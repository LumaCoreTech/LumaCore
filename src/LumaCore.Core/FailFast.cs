// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core;

/// <summary>
/// Provides a mechanism to flush logging queues and terminate the application without losing log messages.
/// </summary>
/// <remarks>
///     <para>
///     This class allows the logging infrastructure to register a termination handler that ensures all buffered log
///     messages are written before the application terminates. The handler is invoked to perform cleanup operations
///     (e.g., flushing logs), and then <see cref="Environment.FailFast(string)"/> is automatically called to terminate
///     the process.
///     </para>
///     <para>
///     To register a termination handler, subscribe to <see cref="TerminationRequested"/> during application startup.
///     The handler should perform cleanup operations such as flushing log writers. The application will be terminated
///     automatically after the handler completes.
///     </para>
/// </remarks>
public static class FailFast
{
	/// <summary>
	/// Requests terminating the application specifying a message describing the reason for termination.
	/// The process is terminated after buffered log messages have been processed by the logging subsystem.
	/// </summary>
	/// <param name="message">The message text describing the reason why application termination is requested.</param>
	public static void TerminateApplication(string message)
	{
		Action<string, Exception?>? handler = TerminationRequested;

		// Invoke handler to perform cleanup (e.g., flush logs)
		handler?.Invoke(message, null);

		// Terminate the application
		Environment.FailFast(message);
	}

	/// <summary>
	/// Requests terminating the application specifying the exception that caused the termination.
	/// The process is terminated after buffered log messages have been processed by the logging subsystem.
	/// </summary>
	/// <param name="exception">The exception that is the reason why application termination is requested.</param>
	public static void TerminateApplication(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		Action<string, Exception?>? handler = TerminationRequested;

		if (handler != null)
		{
			// Invoke handler to perform cleanup (e.g., flush logs)
			handler(exception.Message, exception);
		}

		// Terminate the application
		Environment.FailFast(exception.Message, exception);
	}

	/// <summary>
	/// Occurs when the <see cref="TerminateApplication(string)"/> or <see cref="TerminateApplication(Exception)"/> method
	/// is called. The logging infrastructure should subscribe to this event to flush all log writers before the application
	/// terminates.
	/// </summary>
	/// <remarks>
	/// The handler receives a message and an optional exception. The handler should perform cleanup operations such as
	/// flushing log writers. After the handler completes, <see cref="Environment.FailFast(string)"/> is automatically
	/// called to terminate the application. The handler does <b>not</b> need to terminate the application itself.
	/// </remarks>
	public static event Action<string, Exception?>? TerminationRequested;
}
