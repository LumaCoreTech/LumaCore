// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Threading;

/// <summary>
/// Provides explicit, opt-in helpers for tuning the process-wide
/// <see cref="ThreadPool"/> configuration on behalf of an application that consumes
/// <c>LumaCore.Core</c> async primitives.
/// </summary>
/// <remarks>
///     <para>
///     <b>Why this exists.</b> The async primitives in <c>LumaCore.Core</c> (e.g. <see cref="AsyncManualResetEvent"/>,
///     <see cref="AsyncAutoResetEvent"/>, <see cref="DefaultAsyncWaitQueue{T}"/>) build on
///     <see cref="TaskCompletionSource{T}"/> with <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>.
///     Each <see langword="await"/> on such a primitive schedules its continuation as a thread-pool work item.
///     On environments with very few logical processors (1- or 2-vCPU CI runners, lean containers) the default
///     <see cref="ThreadPool.GetMinThreads"/> floor can be too low, in which case the pool's hill-climbing
///     algorithm gates new threads at roughly 500 ms intervals — long enough to look like a hang under
///     synchronous wait patterns.
///     </para>
///     <para>
///     <b>Scope.</b> This is an <em>application-level</em> concern, not a library concern. Therefore
///     <c>LumaCore.Core</c> never mutates the thread pool from a static initializer or module initializer;
///     callers must opt in explicitly when they know their target environment is at risk.
///     </para>
///     <para>
///     <b>Preferred alternative.</b> If you control the deployment environment, prefer setting the
///     environment variable <c>DOTNET_ThreadPool_MinWorkerThreads</c> (and/or
///     <c>DOTNET_ThreadPool_MinIOCompletionThreads</c>) before the runtime starts. That keeps the
///     concern entirely outside application code.
///     </para>
///     <para>
///     <b>Typical usage</b> in an application bootstrap (<c>Program.Main</c>, test fixtures, …):
///     <code>
///     ThreadPoolBootstrap.EnsureMinWorkerThreads(4);
///     </code>
///     </para>
/// </remarks>
public static class ThreadPoolBootstrap
{
	/// <summary>
	/// Raises the process-wide minimum number of worker threads in the
	/// <see cref="ThreadPool"/> if it is currently below <paramref name="minWorkerThreads"/>.
	/// Leaves the minimum I/O completion thread count unchanged.
	/// </summary>
	/// <param name="minWorkerThreads">
	/// The desired floor for the worker thread minimum. Must be greater than zero. The historical default
	/// of <c>4</c> is appropriate for most LumaCore async primitives on small CI runners; tune upward only
	/// after measuring.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the minimum was raised, <see langword="false"/> if the existing minimum
	/// was already at or above <paramref name="minWorkerThreads"/> (no-op) or if
	/// <see cref="ThreadPool.SetMinThreads(int, int)"/>
	/// rejected the request.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="minWorkerThreads"/> is less than or equal to zero.
	/// </exception>
	/// <remarks>
	/// This method is idempotent: calling it repeatedly with the same or smaller value is harmless.
	/// It does <em>not</em> lower the minimum — applications wishing to reduce the floor must call
	/// <see cref="ThreadPool.SetMinThreads(int, int)"/> directly.
	/// </remarks>
	public static bool EnsureMinWorkerThreads(int minWorkerThreads = 4)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minWorkerThreads);

		ThreadPool.GetMinThreads(out int currentMinWorkerThreads, out int currentMinIoThreads);

		if (currentMinWorkerThreads >= minWorkerThreads)
			return false;

		return ThreadPool.SetMinThreads(minWorkerThreads, currentMinIoThreads);
	}
}
