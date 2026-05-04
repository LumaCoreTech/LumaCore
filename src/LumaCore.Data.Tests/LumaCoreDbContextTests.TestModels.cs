// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections.Concurrent;

namespace LumaCore.Data.Tests;

public sealed partial class LumaCoreDbContextTests
{
	/// <summary>
	/// Records every invocation in registration order so tests can assert execution ordering
	/// (LIFO versus FIFO) deterministically. Invocations are tracked in a thread-safe collection so
	/// the fixture stays robust even under accidental concurrent use.
	/// </summary>
	private sealed class CompensationRecorder
	{
		private readonly ConcurrentQueue<string> mInvocations = new();

		/// <summary>
		/// Gets the tags of every compensation invocation that has fired, in the order they fired.
		/// </summary>
		public IReadOnlyCollection<string> Invocations => mInvocations;

		/// <summary>
		/// Returns a compensation action that records the supplied <paramref name="tag"/> when invoked.
		/// </summary>
		/// <param name="tag">A label identifying this compensation in assertions.</param>
		/// <returns>
		/// A compensation callback suitable for
		/// <see cref="LumaCoreDbContext.RegisterRollbackCompensation"/>.
		/// </returns>
		public Func<CancellationToken, Task> Create(string tag) => _ =>
		{
			mInvocations.Enqueue(tag);
			return Task.CompletedTask;
		};

		/// <summary>
		/// Returns a compensation action that records the supplied <paramref name="tag"/> and then throws
		/// the given <paramref name="exception"/>. Used to verify that a failing compensation does not
		/// prevent the remaining compensations from running.
		/// </summary>
		/// <param name="tag">A label identifying this compensation in assertions.</param>
		/// <param name="exception">The exception to throw after recording the tag.</param>
		/// <returns>A compensation callback that records and then throws.</returns>
		public Func<CancellationToken, Task> CreateThrowing(string tag, Exception exception) => _ =>
		{
			mInvocations.Enqueue(tag);
			throw exception;
		};
	}
}
