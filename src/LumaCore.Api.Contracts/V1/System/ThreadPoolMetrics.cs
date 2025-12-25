// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Contracts.V1.System;

/// <summary>
/// Thread pool state metrics.
/// </summary>
/// <remarks>
///     <para>
///     The thread pool manages worker threads (for CPU-bound operations) and I/O completion port (IOCP) threads
///     (for async I/O callbacks from file/network operations).
///     </para>
///     <para>
///     "Available" = remaining capacity (<c>Max − busy</c>), not pre-allocated idle threads. The pool creates
///     threads on demand up to Min instantly, then throttles creation above that threshold.
///     </para>
/// </remarks>
/// <param name="AvailableWorkerThreads">Remaining worker thread capacity.</param>
/// <param name="AvailableCompletionPortThreads">Remaining I/O completion port thread capacity.</param>
/// <param name="MinWorkerThreads">Minimum worker threads before throttled creation.</param>
/// <param name="MinCompletionPortThreads">Minimum I/O completion port threads.</param>
/// <param name="MaxWorkerThreads">Maximum worker threads the pool can create.</param>
/// <param name="MaxCompletionPortThreads">Maximum I/O completion port threads the pool can create.</param>
/// <param name="PendingWorkItemCount">Work items queued and waiting for a thread to become available.</param>
public sealed record ThreadPoolMetrics(
	int  AvailableWorkerThreads,
	int  AvailableCompletionPortThreads,
	int  MinWorkerThreads,
	int  MinCompletionPortThreads,
	int  MaxWorkerThreads,
	int  MaxCompletionPortThreads,
	long PendingWorkItemCount);
