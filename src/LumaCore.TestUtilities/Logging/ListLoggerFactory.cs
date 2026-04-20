// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Logging;

namespace LumaCore.TestUtilities.Logging;

/// <summary>
/// In-memory <see cref="ILoggerFactory"/> that funnels every log entry produced by every category into a
/// single shared <see cref="Entries"/> list. Use this when the system under test resolves
/// <see cref="ILoggerFactory"/> (rather than a typed <see cref="ILogger{TCategoryName}"/>) and the test
/// needs to observe entries from multiple categories — and especially their relative order.
/// </summary>
/// <remarks>
///     <para>
///     Each emitted <see cref="LogEntry"/> carries the category name in <see cref="LogEntry.Category"/>,
///     allowing tests to filter or assert per category:
///     <c>logger.Entries.Where(e =&gt; e.Category == typeof(MyService).FullName)</c>.
///     </para>
///     <para>
///     <b>Provider pipeline:</b> <see cref="AddProvider"/> is an intentional no-op. The factory is its own
///     sink — registering external <see cref="ILoggerProvider"/> instances would defeat the purpose of
///     in-memory capture and is never useful for tests. <see cref="Dispose"/> is likewise a no-op because
///     the factory owns no unmanaged resources.
///     </para>
///     <para>
///     <b>Thread-safety:</b> All members are safe to call from multiple threads concurrently. Loggers
///     returned by <see cref="CreateLogger(string)"/> share the same lock as the factory itself, so log
///     ordering across categories reflects the actual happens-before relationship of the calls.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// var factory = new ListLoggerFactory();
/// var sut     = new Orchestrator(factory); // SUT internally creates loggers per sub-component
/// 
/// sut.Run();
/// 
/// // Assert overall ordering across categories.
/// Assert.Collection(factory.Entries,
///     e =&gt; Assert.Equal("Started", e.Message),
///     e =&gt; Assert.Equal("Finished", e.Message));
/// 
/// // Assert per-category output.
/// LogEntry warning = Assert.Single(
///     factory.Entries,
///     e =&gt; e.Category == typeof(Worker).FullName &amp;&amp; e.Level == LogLevel.Warning);
/// </code>
/// </example>
public sealed class ListLoggerFactory : ILoggerFactory
{
	private readonly Lock           mGate    = new();
	private readonly List<LogEntry> mEntries = [];

	/// <summary>
	/// No-op: the factory owns no unmanaged resources.
	/// </summary>
	public void Dispose()
	{
		// Intentional no-op — see class remarks.
	}

	/// <summary>
	/// Gets a snapshot of every log entry produced by every logger created from this factory, in the
	/// order they were emitted.
	/// </summary>
	/// <remarks>
	/// Each access returns a fresh, independent copy. See <see cref="ListLogger{T}.Entries"/> for the
	/// rationale.
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
	/// Removes all captured entries from the shared sink.
	/// </summary>
	public void Clear()
	{
		lock (mGate)
		{
			mEntries.Clear();
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The returned logger writes into the factory's shared <see cref="Entries"/> list with
	/// <see cref="LogEntry.Category"/> set to <paramref name="categoryName"/>.
	/// </remarks>
	public ILogger CreateLogger(string categoryName)
	{
		ArgumentNullException.ThrowIfNull(categoryName);

		return new CategoryLogger(this, categoryName);
	}

	/// <summary>
	/// No-op: this factory is its own sink and does not delegate to external providers.
	/// </summary>
	/// <param name="provider">Ignored.</param>
	public void AddProvider(ILoggerProvider provider)
	{
		// Intentional no-op — see class remarks.
	}

	/// <summary>
	/// Appends an entry to the shared sink under the factory's lock.
	/// </summary>
	/// <param name="entry">The entry to append.</param>
	/// <remarks>
	/// Called by <see cref="CategoryLogger.Log{TState}"/>. Centralizing the lock here keeps the locking
	/// strategy in one place and ensures every category writes through the same critical section, so
	/// the captured order matches the actual happens-before relationship of the calls.
	/// </remarks>
	private void Append(LogEntry entry)
	{
		lock (mGate)
		{
			mEntries.Add(entry);
		}
	}

	/// <summary>
	/// Per-category <see cref="ILogger"/> returned by <see cref="CreateLogger"/>. Forwards every entry
	/// to the owning <see cref="ListLoggerFactory"/>'s shared sink, tagged with the category name.
	/// </summary>
	/// <param name="owner">The factory that owns the shared <see cref="LogEntry"/> sink.</param>
	/// <param name="categoryName">
	/// The category name to stamp onto every <see cref="LogEntry"/> produced by this logger.
	/// </param>
	private sealed class CategoryLogger(ListLoggerFactory owner, string categoryName) : ILogger
	{
		/// <inheritdoc/>
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		/// <inheritdoc/>
		/// <remarks>
		/// Always returns <see langword="true"/> so tests can assert the absence of entries at a given
		/// level. See <see cref="ListLogger{T}"/> remarks for the full rationale.
		/// </remarks>
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

			owner.Append(new LogEntry(logLevel, formatter(state, exception), exception, categoryName));
		}
	}
}
