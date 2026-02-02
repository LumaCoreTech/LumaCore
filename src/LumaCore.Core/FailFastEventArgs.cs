// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core;

/// <summary>
/// Event arguments for <see cref="FailFast.BeforeTermination"/>.
/// </summary>
public sealed class FailFastEventArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FailFastEventArgs"/> class.
	/// </summary>
	/// <param name="message">The termination message.</param>
	/// <param name="exception">The exception that triggered termination, or <see langword="null"/>.</param>
	public FailFastEventArgs(string message, Exception? exception)
	{
		Message = message;
		Exception = exception;
	}

	/// <summary>
	/// Gets the termination message.
	/// </summary>
	public string Message { get; }

	/// <summary>
	/// Gets the exception that triggered termination, or <see langword="null"/>.
	/// </summary>
	public Exception? Exception { get; }

	/// <summary>
	/// Gets or sets whether to throw <see cref="FailFastCanceledException"/> instead of terminating. For testing only.
	/// </summary>
	public bool Cancel { get; set; }
}
