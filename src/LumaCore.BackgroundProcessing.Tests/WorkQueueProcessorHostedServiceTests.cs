// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

using static LumaCore.BackgroundProcessing.Tests.AsyncTestHelpers;

namespace LumaCore.BackgroundProcessing.Tests;

/// <summary>
/// Unit tests for <see cref="WorkQueueProcessorHostedService"/>.
/// </summary>
public class WorkQueueProcessorHostedServiceTests
{
	private static ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

	#region Constructor

	/// <summary>
	/// Verifies that the constructor succeeds with valid arguments.
	/// </summary>
	[Fact]
	public void Constructor_WithValidArguments_Succeeds()
	{
		// Arrange
		var processor = new WorkQueueProcessor(LoggerFactory);
		var logger = NullLogger<WorkQueueProcessorHostedService>.Instance;

		// Act
		var hostedService = new WorkQueueProcessorHostedService(processor, logger);

		// Assert
		Assert.NotNull(hostedService);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when processor is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenProcessorIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var logger = NullLogger<WorkQueueProcessorHostedService>.Instance;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
			new WorkQueueProcessorHostedService(null!, logger));
		Assert.Equal("processor", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when logger is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var processor = new WorkQueueProcessor(LoggerFactory);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
			new WorkQueueProcessorHostedService(processor, null!));
		Assert.Equal("logger", ex.ParamName);
	}

	#endregion

	#region StartAsync()

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorHostedService.StartAsync"/> initializes the processor.
	/// </summary>
	[Fact]
	public async Task StartAsync_InitializesProcessor()
	{
		// Arrange
		var processor = new WorkQueueProcessor(LoggerFactory);
		var logger = NullLogger<WorkQueueProcessorHostedService>.Instance;
		var hostedService = new WorkQueueProcessorHostedService(processor, logger);

		Assert.False(processor.IsInitialized);

		// Act
		await hostedService.StartAsync(CancellationToken.None);

		// Assert
		Assert.True(processor.IsInitialized);

		// Cleanup
		await processor.DisposeAsync();
	}

	/// <summary>
	/// Verifies that after <see cref="WorkQueueProcessorHostedService.StartAsync"/>, work items can be queued.
	/// </summary>
	[Fact]
	public async Task StartAsync_AfterStart_ProcessorAcceptsWorkItems()
	{
		// Arrange
		var processor = new WorkQueueProcessor(LoggerFactory);
		var logger = NullLogger<WorkQueueProcessorHostedService>.Instance;
		var hostedService = new WorkQueueProcessorHostedService(processor, logger);
		var executed = new TaskCompletionSource<bool>();

		// Act
		await hostedService.StartAsync(CancellationToken.None);

		bool queued = processor.QueueWorkItem(_ =>
		{
			executed.SetResult(true);
			return Task.CompletedTask;
		});

		// Assert
		Assert.True(queued);
		bool result = await AwaitWithTimeoutAsync(executed.Task, "Work item did not execute");
		Assert.True(result);

		// Cleanup
		await processor.DisposeAsync();
	}

	#endregion

	#region StopAsync

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorHostedService.StopAsync"/> shuts down the processor.
	/// </summary>
	[Fact]
	public async Task StopAsync_ShutsDownProcessor()
	{
		// Arrange
		var processor = new WorkQueueProcessor(LoggerFactory);
		var logger = NullLogger<WorkQueueProcessorHostedService>.Instance;
		var hostedService = new WorkQueueProcessorHostedService(processor, logger);

		await hostedService.StartAsync(CancellationToken.None);
		Assert.True(processor.IsInitialized);

		// Act
		await hostedService.StopAsync(CancellationToken.None);

		// Assert
		Assert.False(processor.IsInitialized);

		// Cleanup
		await processor.DisposeAsync();
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorHostedService.StopAsync"/> waits for pending work items.
	/// </summary>
	[Fact]
	public async Task StopAsync_WithPendingWorkItems_WaitsForCompletion()
	{
		// Arrange
		var processor = new WorkQueueProcessor(loggerFactory: LoggerFactory, shutdownTimeout: TimeSpan.FromSeconds(10));
		var logger = NullLogger<WorkQueueProcessorHostedService>.Instance;
		var hostedService = new WorkQueueProcessorHostedService(processor, logger);

		await hostedService.StartAsync(CancellationToken.None);

		var workItemCompleted = new TaskCompletionSource<bool>();
		processor.QueueWorkItem(async _ =>
		{
			await Task.Delay(100, CancellationToken.None);
			workItemCompleted.SetResult(true);
		});

		// Act
		await AwaitWithTimeoutAsync(
			hostedService.StopAsync(CancellationToken.None),
			"StopAsync() timed out waiting for work items");


		// Assert
		Assert.True(workItemCompleted.Task.IsCompletedSuccessfully);

		// Cleanup
		await processor.DisposeAsync();
	}

	#endregion

	#region Full Lifecycle

	/// <summary>
	/// Verifies that the hosted service can go through a complete start-stop cycle.
	/// </summary>
	[Fact]
	public async Task Lifecycle_StartAndStop_CompletesSuccessfully()
	{
		// Arrange
		var processor = new WorkQueueProcessor(LoggerFactory);
		var logger = NullLogger<WorkQueueProcessorHostedService>.Instance;
		var hostedService = new WorkQueueProcessorHostedService(processor, logger);

		// Act
		await hostedService.StartAsync(CancellationToken.None);
		Assert.True(processor.IsInitialized);

		await hostedService.StopAsync(CancellationToken.None);
		Assert.False(processor.IsInitialized);

		// Cleanup
		await processor.DisposeAsync();
	}

	/// <summary>
	/// Verifies that work items queued during the hosted service lifecycle are executed.
	/// </summary>
	[Fact]
	public async Task Lifecycle_WorkItemsQueuedDuringLifecycle_AreExecuted()
	{
		// Arrange
		var processor = new WorkQueueProcessor(LoggerFactory);
		var logger = NullLogger<WorkQueueProcessorHostedService>.Instance;
		var hostedService = new WorkQueueProcessorHostedService(processor, logger);
		int executedCount = 0;

		// Act
		await hostedService.StartAsync(CancellationToken.None);

		for (int i = 0; i < 5; i++)
		{
			processor.QueueWorkItem(_ =>
			{
				Interlocked.Increment(ref executedCount);
				return Task.CompletedTask;
			});
		}

		// Give some time for execution, then stop.
		await Task.Delay(100);
		await AwaitWithTimeoutAsync(
			hostedService.StopAsync(CancellationToken.None),
			"StopAsync() timed out waiting for work items");

		// Assert
		Assert.Equal(5, executedCount);

		// Cleanup
		await processor.DisposeAsync();
	}

	#endregion
}
