// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Logging;

namespace LumaCore.TestUtilities.Logging;

/// <summary>
/// In-memory <see cref="ILogger{TCategoryName}"/> that captures every log entry so tests can assert on
/// level, formatted message, and the attached exception. Intended exclusively for test code — never
/// register this in production composition roots.
/// </summary>
/// <typeparam name="T">The logger category type. Used as the <see cref="LogEntry.Category"/> value.</typeparam>
/// <remarks>
///     <para>
///     <see cref="IsEnabled(LogLevel)"/> always returns <see langword="true"/> so tests can also assert that
///     nothing is logged at or above a given level
///     (e.g. <c>Assert.DoesNotContain(logger.Entries, e =&gt; e.Level &gt;= LogLevel.Warning)</c>)
///     without the production filter mask interfering.
///     </para>
///     <para>
///     Thread-safe: <see cref="Log{TState}"/>, <see cref="Entries"/>, and <see cref="Clear"/> may be called
///     concurrently from multiple threads. <see cref="Entries"/> returns a point-in-time snapshot, so
///     enumerating it is safe even while other threads continue to log.
///     </para>
///     <para>
///     To <i>mutate</i> the captured entries from a test, use <see cref="Clear"/> — the snapshot returned by
///     <see cref="Entries"/> is intentionally read-only.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// var logger = new ListLogger&lt;MyService&gt;();
/// var sut    = new MyService(logger);
/// 
/// sut.DoWork();
/// 
/// LogEntry warning = Assert.Single(logger.Entries, e =&gt; e.Level == LogLevel.Warning);
/// Assert.Equal("Expected message", warning.Message);
/// </code>
/// </example>
public sealed class ListLogger<T> : ILogger<T>
{
	private static readonly string sCategoryName = typeof(T).FullName ?? typeof(T).Name;

	private readonly Lock           mGate    = new();
	private readonly List<LogEntry> mEntries = [];

	/// <summary>
	/// Gets a snapshot of the captured log entries in the order they were emitted.
	/// </summary>
	/// <remarks>
	/// Each access returns a fresh, independent copy. This decouples enumeration from concurrent
	/// <see cref="Log{TState}"/> calls and prevents <see cref="InvalidOperationException"/> from a list
	/// mutated mid-iteration.
	/// </remarks>
	public IReadOnlyList<LogEntry> Entries
	{
		get
		{
			lock (mGate)
			{
				return mEntries.ToArray();
			}
		}
	}

	/// <summary>
	/// Removes all captured entries.
	/// </summary>
	/// <remarks>
	/// Useful for isolating the output of a specific call from constructor-time or setup-time entries
	/// (e.g. <c>logger.Clear();</c> right before the Act phase).
	/// </remarks>
	public void Clear()
	{
		lock (mGate)
		{
			mEntries.Clear();
		}
	}

	/// <inheritdoc/>
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	/// <inheritdoc/>
	public bool IsEnabled(LogLevel logLevel) => true;

	/// <inheritdoc/>
	public void Log<TState>(
		LogLevel                         logLevel,
		EventId                          eventId,
		TState                           state,
		Exception?                       exception,
		Func<TState, Exception?, string> formatter)
	{
		ArgumentNullException.ThrowIfNull(formatter);

		LogEntry entry = new(logLevel, formatter(state, exception), exception, sCategoryName);

		lock (mGate)
		{
			mEntries.Add(entry);
		}
	}
}
