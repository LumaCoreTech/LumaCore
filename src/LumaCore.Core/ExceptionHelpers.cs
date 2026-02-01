// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace LumaCore.Core;

/// <summary>
/// Provides helper methods for exception handling, particularly for preserving stack traces when rethrowing exceptions.
/// </summary>
/// <remarks>
///     <para>
///     When catching and rethrowing exceptions in .NET, a simple <c>throw;</c> preserves the original stack trace,
///     but only if the exception was caught in the same method. If you need to rethrow an exception from a different
///     context (e.g., after unwrapping an <see cref="AggregateException"/>), use <see cref="PrepareForRethrow"/>
///     to preserve the original stack trace.
///     </para>
/// </remarks>
public static class ExceptionHelpers
{
	/// <summary>
	/// Rethrows the specified exception while preserving its original stack trace.
	/// </summary>
	/// <param name="exception">The exception to rethrow. Must not be <see langword="null"/>.</param>
	/// <returns>
	/// This method never returns; it always throws. The return type exists only to enable the
	/// <c>throw PrepareForRethrow(ex)</c> pattern, which helps the compiler understand control flow.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="exception"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	///     <para>
	///     This method uses <see cref="ExceptionDispatchInfo.Capture(Exception)"/> and
	///     <see cref="ExceptionDispatchInfo.Throw()"/>
	///     to rethrow the exception with its original stack trace intact. This is useful when unwrapping exceptions
	///     (e.g., from <see cref="AggregateException.InnerException"/>) or rethrowing from a different call stack.
	///     </para>
	///     <para>
	///         <b>Usage pattern:</b>
	///     </para>
	///     <code>
	///     catch (AggregateException ex)
	///     {
	///         // Unwrap and rethrow the inner exception with preserved stack trace
	///         throw ExceptionHelpers.PrepareForRethrow(ex.InnerException!);
	///     }
	///     </code>
	///     <para>
	///     The <see langword="throw"/> keyword before the method call is optional but recommended: it signals to the compiler
	///     that the code path terminates, enabling better flow analysis and avoiding "not all code paths return a value"
	///     warnings.
	///     </para>
	/// </remarks>
	[DoesNotReturn]
	public static Exception PrepareForRethrow(Exception exception)
	{
		ExceptionDispatchInfo.Capture(exception).Throw();

		// Unreachable: ExceptionDispatchInfo.Throw() always throws and never returns.
		throw new UnreachableException();
	}
}
