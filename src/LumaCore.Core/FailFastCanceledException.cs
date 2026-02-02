// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core;

/// <summary>
/// Thrown when <see cref="FailFast.TerminateApplication(string)"/> is canceled via
/// <see cref="FailFast.BeforeTermination"/>.
/// </summary>
/// <remarks>
/// Ensures that <see cref="FailFast.TerminateApplication(string)"/> never returns normally.
/// It either kills the process or throws this exception. Intended for unit testing only.
/// </remarks>
public sealed class FailFastCanceledException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FailFastCanceledException"/> class.
	/// </summary>
	/// <param name="message">The termination message.</param>
	/// <param name="innerException">The exception that triggered termination, or <see langword="null"/>.</param>
	public FailFastCanceledException(string message, Exception? innerException)
		: base(message, innerException) { }
}
