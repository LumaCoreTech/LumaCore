// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Represents a snapshot of .NET thread pool state.
/// </summary>
/// <param name="AvailableWorkerThreads">
/// Remaining worker thread capacity: <see cref="MaxWorkerThreads"/> minus threads currently executing work.
/// </param>
/// <param name="AvailableCompletionPortThreads">
/// Remaining I/O completion port thread capacity for async I/O callbacks.
/// </param>
/// <param name="MinWorkerThreads">
/// Minimum worker threads. Threads up to this count are created instantly; above this, creation is throttled.
/// Configurable via <c>ThreadPool.SetMinThreads()</c>.
/// </param>
/// <param name="MinCompletionPortThreads">
/// Minimum I/O completion port threads. Same throttling behavior as <see cref="MinWorkerThreads"/>.
/// </param>
/// <param name="MaxWorkerThreads">
/// Maximum worker threads the pool can create. Configurable via <c>ThreadPool.SetMaxThreads()</c>.
/// </param>
/// <param name="MaxCompletionPortThreads">
/// Maximum I/O completion port threads the pool can create.
/// </param>
/// <param name="PendingWorkItemCount">
/// Work items queued and waiting for a thread to become available.
/// </param>
/// <remarks>
///     <para>
///     The thread pool manages two types of threads: worker threads for CPU-bound operations (Task.Run,
///     Parallel.For, etc.) and I/O completion port (IOCP) threads for async I/O callbacks (file/network operations).
///     </para>
///     <para>
///     IOCP is a Windows kernel mechanism. On Linux/macOS, .NET uses epoll/kqueue respectively, but the API
///     uses the same "completion port" terminology for cross-platform consistency.
///     </para>
///     <para>
///     "Available" = remaining capacity (<c>Max − busy</c>), not pre-allocated idle threads. The pool creates
///     threads on demand up to Min instantly, then throttles to one new thread per 500ms until Max.
///     </para>
/// </remarks>
public sealed record ThreadPoolMetrics(
	int  AvailableWorkerThreads,
	int  AvailableCompletionPortThreads,
	int  MinWorkerThreads,
	int  MinCompletionPortThreads,
	int  MaxWorkerThreads,
	int  MaxCompletionPortThreads,
	long PendingWorkItemCount);
