// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics.CodeAnalysis;

using LumaCore.Core.Threading;

using Xunit;

namespace LumaCore.Core.Tests.Threading;

/// <summary>
/// Unit tests for <see cref="ThreadPoolBootstrap"/>.
/// </summary>
/// <remarks>
/// Tests in this class mutate the process-wide <see cref="ThreadPool"/> minimum-worker-thread count.
/// They are placed in a dedicated, non-parallelizable collection (<see cref="ThreadPoolBootstrapTestCollection"/>)
/// so that they cannot race against any future test that touches <see cref="ThreadPool"/>.
/// Each individual test that mutates state restores the original minimums in a <c>finally</c> block.
/// </remarks>
[Collection(ThreadPoolBootstrapTestCollection.Name)]
[Trait("Category", "Threading")]
public sealed class ThreadPoolBootstrapTests
{
	#region EnsureMinWorkerThreads()

	/// <summary>
	/// Verifies that <see cref="ThreadPoolBootstrap.EnsureMinWorkerThreads"/> returns
	/// <see langword="false"/> and does not mutate the pool when the current worker thread minimum is
	/// already at or above the requested target.
	/// </summary>
	[Fact]
	public void EnsureMinWorkerThreads_WhenCurrentAlreadyAtOrAboveTarget_ReturnsFalse()
	{
		// Arrange
		ThreadPool.GetMinThreads(out int originalWorker, out int originalIo);
		int target = originalWorker;

		try
		{
			// Act
			bool raised = ThreadPoolBootstrap.EnsureMinWorkerThreads(target);

			// Assert
			Assert.False(raised);
			ThreadPool.GetMinThreads(out int newWorker, out var _);
			Assert.Equal(originalWorker, newWorker);
		}
		finally
		{
			ThreadPool.SetMinThreads(originalWorker, originalIo);
		}
	}

	/// <summary>
	/// Verifies that <see cref="ThreadPoolBootstrap.EnsureMinWorkerThreads"/> raises the worker thread
	/// minimum and returns <see langword="true"/> when the current minimum is below the requested target.
	/// </summary>
	[Fact]
	public void EnsureMinWorkerThreads_WhenCurrentBelowTarget_RaisesAndReturnsTrue()
	{
		// Arrange
		ThreadPool.GetMinThreads(out int originalWorker, out int originalIo);
		int target = originalWorker + 1;

		try
		{
			// Act
			bool raised = ThreadPoolBootstrap.EnsureMinWorkerThreads(target);

			// Assert
			Assert.True(raised);
			ThreadPool.GetMinThreads(out int newWorker, out var _);
			Assert.True(
				newWorker >= target,
				$"Expected new minimum worker threads ({newWorker}) to be >= target ({target}).");
		}
		finally
		{
			ThreadPool.SetMinThreads(originalWorker, originalIo);
		}
	}

	/// <summary>
	/// Verifies that <see cref="ThreadPoolBootstrap.EnsureMinWorkerThreads"/> leaves the I/O completion
	/// thread minimum untouched when raising the worker thread minimum.
	/// </summary>
	[Fact]
	public void EnsureMinWorkerThreads_WhenRaising_LeavesIoMinimumUnchanged()
	{
		// Arrange
		ThreadPool.GetMinThreads(out int originalWorker, out int originalIo);
		int target = originalWorker + 1;

		try
		{
			// Act
			ThreadPoolBootstrap.EnsureMinWorkerThreads(target);

			// Assert
			ThreadPool.GetMinThreads(out var _, out int newIo);
			Assert.Equal(originalIo, newIo);
		}
		finally
		{
			ThreadPool.SetMinThreads(originalWorker, originalIo);
		}
	}

	/// <summary>
	/// Verifies that <see cref="ThreadPoolBootstrap.EnsureMinWorkerThreads"/> throws
	/// <see cref="ArgumentOutOfRangeException"/> when called with a non-positive value.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void EnsureMinWorkerThreads_WhenMinWorkerThreadsIsNotPositive_ThrowsArgumentOutOfRangeException(int value)
	{
		// Act
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ThreadPoolBootstrap.EnsureMinWorkerThreads(value));

		// Assert
		Assert.Equal("minWorkerThreads", ex.ParamName);
	}

	#endregion
}

/// <summary>
/// xUnit collection that serializes execution of <see cref="ThreadPoolBootstrapTests"/> against any
/// other test class that joins the same collection. Prevents process-wide
/// <see cref="ThreadPool"/> mutations from racing each other.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
	"Naming",
	"CA1711:Identifiers should not have incorrect suffix",
	Justification =
		"The 'Collection' suffix matches xUnit's CollectionDefinition / Collection terminology and is the established naming convention for non-parallel test groupings.")]
public sealed class ThreadPoolBootstrapTestCollection
{
	/// <summary>
	/// The xUnit collection identifier referenced by <see cref="CollectionAttribute"/>.
	/// </summary>
	public const string Name = "ThreadPoolBootstrap";
}
