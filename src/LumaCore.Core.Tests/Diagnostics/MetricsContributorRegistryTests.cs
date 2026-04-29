// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

using Xunit;

namespace LumaCore.Core.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="MetricsContributorRegistry"/>.
/// </summary>
/// <remarks>
/// The tests are split across multiple partial files:
/// <list type="number">
///     <item>
///     <see cref="MetricsContributorRegistryTests"/> (this file) — descriptor query and
///     concurrency behavior, plus the shared test fixtures.
///     </item>
///     <item>
///     <c>MetricsContributorRegistryTests.Register.cs</c> — both <c>Register</c> overloads,
///     ordered from valid registration through argument violations, domain violations, and
///     duplicate detection.
///     </item>
/// </list>
/// </remarks>
[Trait("Category", "Diagnostics")]
public sealed partial class MetricsContributorRegistryTests
{
	#region Descriptors

	/// <summary>
	/// Verifies that <see cref="MetricsContributorRegistry.Descriptors"/> returns descriptors sorted
	/// alphabetically by section name (case-insensitive), regardless of registration order, and that
	/// each descriptor still carries the correct <see cref="MetricsContributorDescriptor.ImplementationType"/>.
	/// </summary>
	[Fact]
	public void Descriptors_WhenMultipleRegistered_AreReturnedAlphabeticallySorted()
	{
		// Arrange
		var registry = new MetricsContributorRegistry();
		registry.Register("zeta", typeof(SampleContributor));
		registry.Register("alpha", typeof(SecondContributor));
		registry.Register("Mike", typeof(ThirdContributor));

		// Act
		IReadOnlyList<MetricsContributorDescriptor> descriptors = registry.Descriptors;

		// Assert
		Assert.Equal(
			[
				new MetricsContributorDescriptor("alpha", typeof(SecondContributor)),
				new MetricsContributorDescriptor("Mike", typeof(ThirdContributor)),
				new MetricsContributorDescriptor("zeta", typeof(SampleContributor))
			],
			descriptors);
	}

	/// <summary>
	/// Verifies that <see cref="MetricsContributorRegistry.Descriptors"/> returns a snapshot — adding new
	/// registrations afterwards does not mutate the previously retrieved snapshot.
	/// </summary>
	[Fact]
	public void Descriptors_WhenAccessedBeforeFurtherRegistration_PreviousSnapshotUnchanged()
	{
		// Arrange
		var registry = new MetricsContributorRegistry();
		registry.Register("alpha", typeof(SampleContributor));

		IReadOnlyList<MetricsContributorDescriptor> snapshot = registry.Descriptors;

		// Act
		registry.Register("beta", typeof(SecondContributor));

		// Assert: the previously retrieved snapshot still contains exactly the original entry; a new
		// query on the registry now reflects both registrations in alphabetical order.
		Assert.Equal(["alpha"], snapshot.Select(d => d.SectionName));
		Assert.Equal(["alpha", "beta"], registry.Descriptors.Select(d => d.SectionName));
	}

	#endregion

	#region Concurrency

	/// <summary>
	/// Verifies that concurrent registrations of <b>disjoint</b> section names from many threads all
	/// succeed and produce the complete expected set. A <see cref="Barrier"/> forces the threads to start
	/// the actual <c>Register</c> call simultaneously, so the registry's internal lock is genuinely
	/// exercised. Dedicated <see cref="Thread"/> instances are used instead of
	/// <see cref="Parallel.For(int, int, Action{int})"/>: with <c>Barrier(N)</c> waiting for N participants
	/// and <c>Parallel.For</c> sourcing workers from the <see cref="ThreadPool"/>, on machines where
	/// <c>N</c> exceeds <see cref="ThreadPool.GetMinThreads(out int, out int)"/> the test would deadlock
	/// the pool until hill-climbing added more workers (~500 ms each), starving any other timing-sensitive
	/// test running concurrently in the suite.
	/// </summary>
	[Fact]
	public void Register_WhenCalledFromMultipleThreadsConcurrently_AllSucceed()
	{
		// Arrange
		var registry = new MetricsContributorRegistry();
		const int threadCount = 32;
		using var barrier = new Barrier(threadCount);
		var threads = new Thread[threadCount];

		// Act
		for (int i = 0; i < threadCount; i++)
		{
			int capturedIndex = i;
			threads[i] = new Thread(() =>
			{
				// ReSharper disable once AccessToDisposedClosure
				barrier.SignalAndWait();
				registry.Register($"feature{capturedIndex}", typeof(SampleContributor));
			})
			{
				IsBackground = true,
				Name         = $"RegistryConcurrencyTest-{i}"
			};
			threads[i].Start();
		}

		foreach (Thread thread in threads)
			thread.Join();

		// Assert
		IEnumerable<string> expected = Enumerable
			.Range(0, threadCount)
			.Select(i => $"feature{i}")
			.OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
		Assert.Equal(expected, registry.Descriptors.Select(d => d.SectionName));
		Assert.All(registry.Descriptors, d => Assert.Equal(typeof(SampleContributor), d.ImplementationType));
	}

	/// <summary>
	/// Verifies that when many threads race to register the <b>same</b> section name, exactly one wins
	/// and all others observe <see cref="InvalidOperationException"/> from the duplicate-detection branch
	/// — and that the registry ends in a consistent state with a single entry. This is the scenario the
	/// internal lock is actually there to protect; the disjoint-name test above only proves that
	/// independent registrations don't corrupt the dictionary.
	/// </summary>
	/// <remarks>
	/// Uses dedicated <see cref="Thread"/> instances rather than
	/// <see cref="Parallel.For(int, int, Action{int})"/> for the same reason as the disjoint-name test:
	/// to avoid <see cref="ThreadPool"/> starvation when the participant count exceeds the configured
	/// minimum worker count.
	/// </remarks>
	[Fact]
	public void Register_WhenManyThreadsRaceForSameName_ExactlyOneWinsOthersThrow()
	{
		// Arrange
		var registry = new MetricsContributorRegistry();
		const int threadCount = 32;
		using var barrier = new Barrier(threadCount);
		int winners = 0;
		int duplicates = 0;
		var threads = new Thread[threadCount];

		// Act
		for (int i = 0; i < threadCount; i++)
		{
			threads[i] = new Thread(() =>
			{
				// ReSharper disable once AccessToDisposedClosure
				barrier.SignalAndWait();
				try
				{
					registry.Register("contended", typeof(SampleContributor));
					Interlocked.Increment(ref winners);
				}
				catch (InvalidOperationException)
				{
					Interlocked.Increment(ref duplicates);
				}
			})
			{
				IsBackground = true,
				Name         = $"RegistryRaceTest-{i}"
			};
			threads[i].Start();
		}

		foreach (Thread thread in threads)
			thread.Join();

		// Assert
		Assert.Equal(1, winners);
		Assert.Equal(threadCount - 1, duplicates);
		MetricsContributorDescriptor descriptor = Assert.Single(registry.Descriptors);
		Assert.Equal(new MetricsContributorDescriptor("contended", typeof(SampleContributor)), descriptor);
	}

	#endregion

	#region Test fixtures

	/// <summary>
	/// Minimal <see cref="IMetricsContributor"/> implementation used in valid-registration scenarios.
	/// </summary>
	private sealed class SampleContributor : IMetricsContributor
	{
		/// <inheritdoc/>
		public Task<object> CollectAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<object>(new { Foo = 1 });
	}

	/// <summary>
	/// Second <see cref="IMetricsContributor"/> implementation used to test duplicate detection
	/// (so the conflicting type name in the error message is distinguishable from
	/// <see cref="SampleContributor"/>).
	/// </summary>
	private sealed class SecondContributor : IMetricsContributor
	{
		/// <inheritdoc/>
		public Task<object> CollectAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<object>(new { Bar = 2 });
	}

	/// <summary>
	/// Third <see cref="IMetricsContributor"/> implementation, used to verify alphabetical ordering with
	/// a name that case-insensitively sorts between <c>alpha</c> and <c>zeta</c>.
	/// </summary>
	private sealed class ThirdContributor : IMetricsContributor
	{
		/// <inheritdoc/>
		public Task<object> CollectAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<object>(new { Baz = 3 });
	}

	#endregion
}
