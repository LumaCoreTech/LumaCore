// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Runtime.InteropServices;

using LumaCore.Core.Diagnostics;

using Xunit;

namespace LumaCore.Core.Tests.Diagnostics;

/// <summary>
/// Cross-platform smoke tests for the various <c>*MetricsFactory.Create()</c> entry points.
/// </summary>
/// <remarks>
/// These tests intentionally avoid asserting concrete numeric values because the host environment cannot be
/// controlled from a unit test. They instead assert on invariants that <b>must</b> hold for the values to be
/// usable as diagnostic telemetry (non-null instances, sane ranges, monotonic relationships, OS-specific
/// nullability rules).
/// </remarks>
[Trait("Category", "Diagnostics")]
public sealed class MetricsFactorySmokeTests
{
	#region GcMetricsFactory

	/// <summary>
	/// Verifies that <see cref="GcMetricsFactory.Create"/> returns a snapshot whose counters are non-negative
	/// and whose generation counters are monotonic (Gen2 ≤ Gen1 ≤ Gen0).
	/// </summary>
	[Fact]
	public void GcMetricsFactory_Create_ProducesUsableSnapshot()
	{
		// Act
		GcMetrics gc = GcMetricsFactory.Create();

		// Assert
		Assert.True(gc.Gen0Collections >= 0, "Gen0 collections must be non-negative.");
		Assert.True(gc.Gen1Collections >= 0, "Gen1 collections must be non-negative.");
		Assert.True(gc.Gen2Collections >= 0, "Gen2 collections must be non-negative.");
		Assert.True(gc.TotalAllocatedBytes >= 0, "Total allocated bytes must be non-negative.");

		// A Gen2 collection forces Gen1 and Gen0 too — therefore Gen2 ≤ Gen1 ≤ Gen0.
		Assert.True(gc.Gen2Collections <= gc.Gen1Collections);
		Assert.True(gc.Gen1Collections <= gc.Gen0Collections);
	}

	#endregion

	#region ThreadPoolMetricsFactory

	/// <summary>
	/// Verifies that <see cref="ThreadPoolMetricsFactory.Create"/> returns sensible thread-pool limits
	/// (Min ≤ Max, capacity values within range, non-negative pending count).
	/// </summary>
	[Fact]
	public void ThreadPoolMetricsFactory_Create_ProducesUsableSnapshot()
	{
		// Act
		ThreadPoolMetrics tp = ThreadPoolMetricsFactory.Create();

		// Assert
		Assert.True(tp.MinWorkerThreads > 0);
		Assert.True(tp.MinCompletionPortThreads > 0);
		Assert.True(tp.MaxWorkerThreads >= tp.MinWorkerThreads);
		Assert.True(tp.MaxCompletionPortThreads >= tp.MinCompletionPortThreads);
		Assert.InRange(tp.AvailableWorkerThreads, 0, tp.MaxWorkerThreads);
		Assert.InRange(tp.AvailableCompletionPortThreads, 0, tp.MaxCompletionPortThreads);
		Assert.True(tp.PendingWorkItemCount >= 0);
	}

	#endregion

	#region ProcessMetricsFactory

	/// <summary>
	/// Verifies that <see cref="ProcessMetricsFactory.Create"/> reports a process that started in the past
	/// and a non-negative uptime.
	/// </summary>
	[Fact]
	public void ProcessMetricsFactory_Create_ProducesUsableSnapshot()
	{
		// Act
		ProcessMetrics process = ProcessMetricsFactory.Create();

		// Assert
		Assert.True(process.ThreadCount > 0, "The current process always has at least one thread.");
		Assert.True(process.HandleCount >= 0);
		Assert.True(process.StartTimeUtc <= DateTime.UtcNow);
		Assert.True(process.Uptime >= TimeSpan.Zero);
		Assert.Equal(DateTimeKind.Utc, process.StartTimeUtc.Kind);
	}

	#endregion

	#region MemoryMetricsFactory

	/// <summary>
	/// Verifies that <see cref="MemoryMetricsFactory.Create"/> returns a fully composed snapshot whose
	/// nested DTOs honour their nullability rules (system info may be null on unsupported platforms;
	/// container info is null outside containers).
	/// </summary>
	[Fact]
	public void MemoryMetricsFactory_Create_ProducesUsableSnapshot()
	{
		// Act
		MemoryMetrics memory = MemoryMetricsFactory.Create();

		// Assert
		Assert.NotNull(memory.Managed);
		Assert.NotNull(memory.Process);
		Assert.NotNull(memory.System);
		Assert.NotNull(memory.Effective);

		Assert.True(memory.Managed.LiveBytes >= 0);
		Assert.True(memory.Managed.HeapSizeBytes >= 0);
		Assert.True(memory.Managed.FragmentedBytes >= 0);
		Assert.True(memory.Managed.PinnedObjectsCount >= 0);
		Assert.True(memory.Process.WorkingSetBytes > 0, "Process working set must be > 0 for a running process.");
		Assert.True(memory.Process.PrivateMemoryBytes > 0);

		// Effective.UsageBytes is process working set when no container is reported; either way it must
		// be positive for a running process.
		Assert.True(memory.Effective.UsageBytes > 0);

		// On Windows and Linux the system view is filled in. On macOS it depends on host APIs and may be
		// null — we only assert on the platforms where the implementation is deterministic.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
		    || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			Assert.NotNull(memory.System.TotalPhysicalBytes);
			Assert.NotNull(memory.System.AvailablePhysicalBytes);
			Assert.True(memory.System.TotalPhysicalBytes!.Value > 0);
			Assert.True(memory.System.AvailablePhysicalBytes!.Value >= 0);
			Assert.True(memory.System.AvailablePhysicalBytes <= memory.System.TotalPhysicalBytes);
		}
	}

	#endregion

	#region SystemMetricsFactory

	/// <summary>
	/// Verifies that <see cref="SystemMetricsFactory.Create"/> orchestrates the individual factories into a
	/// single coherent snapshot whose components are non-null and whose timestamp is sane.
	/// </summary>
	[Fact]
	public void SystemMetricsFactory_Create_WhenCalled_AggregatesAllSubFactories()
	{
		// Arrange
		DateTime before = DateTime.UtcNow;

		// Act
		SystemMetrics system = SystemMetricsFactory.Create();
		DateTime after = DateTime.UtcNow;

		// Assert
		Assert.NotNull(system);
		Assert.NotNull(system.Memory);
		Assert.NotNull(system.Gc);
		Assert.NotNull(system.Process);
		Assert.NotNull(system.ThreadPool);

		// Snapshot timestamp must lie between Now-before and Now-after measured around the call.
		Assert.InRange(system.Timestamp, before.AddSeconds(-1), after.AddSeconds(1));
		Assert.Equal(DateTimeKind.Utc, system.Timestamp.Kind);
	}

	/// <summary>
	/// Verifies that <see cref="SystemMetricsFactory.Create"/> can be called repeatedly without throwing
	/// and that every iteration produces a fully populated snapshot — the underlying P/Invoke and BCL
	/// calls must be re-entrant and must not return partial/null sub-snapshots on subsequent calls.
	/// </summary>
	[Fact]
	public void SystemMetricsFactory_Create_WhenCalledRepeatedly_DoesNotThrow()
	{
		// Arrange + Act + Assert
		for (int i = 0; i < 10; i++)
		{
			SystemMetrics system = SystemMetricsFactory.Create();

			Assert.NotNull(system);
			Assert.NotNull(system.Memory);
			Assert.NotNull(system.Gc);
			Assert.NotNull(system.Process);
			Assert.NotNull(system.ThreadPool);
		}
	}

	#endregion
}
