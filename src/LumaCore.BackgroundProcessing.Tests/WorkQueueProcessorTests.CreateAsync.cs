// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.BackgroundProcessing.Tests;

// Unit tests for the WorkQueueProcessor.CreateAsync factory method.
public partial class WorkQueueProcessorTests
{
	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.CreateAsync"/> with default parameters
	/// creates and initializes a service successfully.
	/// </summary>
	[Fact]
	public async Task CreateAsync_WithDefaultParameters_ReturnsInitializedService()
	{
		// Arrange & Act
		await using var service = await WorkQueueProcessor.CreateAsync(LoggerFactory);

		// Assert
		Assert.NotNull(service);
		Assert.True(service.IsInitialized);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.CreateAsync"/> with custom parameters
	/// creates and initializes a service successfully.
	/// </summary>
	[Fact]
	public async Task CreateAsync_WithCustomParameters_ReturnsInitializedService()
	{
		// Arrange
		const int maxQueueSize = 500;
		TimeSpan shutdownTimeout = TimeSpan.FromSeconds(15);
		const int maxConcurrency = 2;

		// Act
		await using var service = await WorkQueueProcessor.CreateAsync(
			                          loggerFactory: LoggerFactory,
			                          maxQueueSize,
			                          shutdownTimeout,
			                          maxConcurrency);

		// Assert
		Assert.NotNull(service);
		Assert.True(service.IsInitialized);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.CreateAsync"/> with parallel concurrency setting
	/// creates and initializes a service successfully.
	/// </summary>
	[Fact]
	public async Task CreateAsync_WithParallelConcurrency_ReturnsInitializedService()
	{
		// Arrange & Act
		await using var service = await WorkQueueProcessor.CreateAsync(loggerFactory: LoggerFactory, maxConcurrency: 4);

		// Assert
		Assert.NotNull(service);
		Assert.True(service.IsInitialized);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.CreateAsync"/> with invalid queue size
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public async Task CreateAsync_WithInvalidQueueSize_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act
		var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
			         WorkQueueProcessor.CreateAsync(loggerFactory: LoggerFactory, maxQueueSize: 0));

		// Assert
		Assert.Equal("maxQueueSize", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.CreateAsync"/> with invalid concurrency
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public async Task CreateAsync_WithInvalidConcurrency_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act
		var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
			         WorkQueueProcessor.CreateAsync(loggerFactory: LoggerFactory, maxConcurrency: -1));

		// Assert
		Assert.Equal("maxConcurrency", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessor.CreateAsync"/> with invalid shutdown timeout
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public async Task CreateAsync_WithInvalidShutdownTimeout_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act
		var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
			         WorkQueueProcessor.CreateAsync(loggerFactory: LoggerFactory, shutdownTimeout: TimeSpan.Zero));

		// Assert
		Assert.Equal("shutdownTimeout", ex.ParamName);
	}
}
