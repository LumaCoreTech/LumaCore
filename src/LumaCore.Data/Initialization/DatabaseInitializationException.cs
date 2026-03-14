// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Data.Initialization;

/// <summary>
/// Exception thrown during database initialization that includes failure categorization.
/// </summary>
/// <remarks>
///     <para>
///     This exception is used internally by <see cref="DatabaseInitializer"/> to propagate both the
///     error details and the <see cref="DatabaseFailureCategory"/> to the top-level error handler.
///     </para>
///     <para>
///     The <see cref="Category"/> determines whether the <see cref="DatabaseConnectionMonitorService"/> should
///     attempt automatic recovery or give up and wait for manual intervention.
///     </para>
/// </remarks>
public sealed class DatabaseInitializationException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DatabaseInitializationException"/> class.
	/// </summary>
	/// <param name="message">A human-readable message describing the failure, suitable for UI display.</param>
	/// <param name="category">The category of failure, indicating whether automatic recovery is possible.</param>
	/// <param name="innerException">The underlying exception that caused this failure.</param>
	public DatabaseInitializationException(
		string                  message,
		DatabaseFailureCategory category,
		Exception?              innerException = null)
		: base(message, innerException)
	{
		Category = category;
	}

	/// <summary>
	/// Gets the category of this failure, indicating whether automatic recovery is possible.
	/// </summary>
	/// <value>The <see cref="DatabaseFailureCategory"/> for this failure.</value>
	public DatabaseFailureCategory Category { get; }
}
