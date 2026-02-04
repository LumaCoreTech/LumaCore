// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.BackgroundProcessing.Tests;

// Unit tests for the constructor of WorkQueueProcessor.
public partial class WorkQueueProcessorTests
{
	/// <summary>
	/// Verifies that constructing a <see cref="WorkQueueProcessor"/> with default parameters
	/// succeeds without throwing.
	/// </summary>
	[Fact]
	public void Constructor_WithDefaultParameters_Succeeds()
	{
		// Arrange & Act
		var service = new WorkQueueProcessor(LoggerFactory);

		// Assert
		Assert.NotNull(service);
		Assert.False(service.IsInitialized);
	}

	/// <summary>
	/// Verifies that constructing a <see cref="WorkQueueProcessor"/> with custom valid parameters
	/// succeeds without throwing.
	/// </summary>
	[Fact]
	public void Constructor_WithCustomValidParameters_Succeeds()
	{
		// Arrange
		const int maxQueueSize = 500;
		TimeSpan shutdownTimeout = TimeSpan.FromSeconds(60);
		const int maxConcurrency = 4;

		// Act
		var service = new WorkQueueProcessor(LoggerFactory, maxQueueSize, shutdownTimeout, maxConcurrency);

		// Assert
		Assert.NotNull(service);
		Assert.False(service.IsInitialized);
	}

	/// <summary>
	/// Verifies that constructing a <see cref="WorkQueueProcessor"/> with <see langword="null"/>
	/// shutdown timeout uses the default timeout without throwing.
	/// </summary>
	[Fact]
	public void Constructor_WithNullShutdownTimeout_UsesDefaultTimeout()
	{
		// Arrange & Act
		var service = new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			maxQueueSize: 100,
			shutdownTimeout: null,
			maxConcurrency: 1);

		// Assert
		Assert.NotNull(service);
		Assert.False(service.IsInitialized);
	}

	/// <summary>
	/// Verifies that constructing a <see cref="WorkQueueProcessor"/> with zero queue size
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithZeroQueueSize_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			maxQueueSize: 0));

		// Assert
		Assert.Equal("maxQueueSize", ex.ParamName);
		Assert.Contains("Queue size must be positive", ex.Message);
	}

	/// <summary>
	/// Verifies that constructing a <see cref="WorkQueueProcessor"/> with negative queue size
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithNegativeQueueSize_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			maxQueueSize: -1));

		// Assert
		Assert.Equal("maxQueueSize", ex.ParamName);
		Assert.Contains("Queue size must be positive", ex.Message);
	}

	/// <summary>
	/// Verifies that constructing a <see cref="WorkQueueProcessor"/> with zero concurrency
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithZeroConcurrency_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			maxConcurrency: 0));

		// Assert
		Assert.Equal("maxConcurrency", ex.ParamName);
		Assert.Contains("Concurrency level must be positive", ex.Message);
	}

	/// <summary>
	/// Verifies that constructing a <see cref="WorkQueueProcessor"/> with negative concurrency
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithNegativeConcurrency_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkQueueProcessor(
			loggerFactory: LoggerFactory,
			maxConcurrency: -1));

		// Assert
		Assert.Equal("maxConcurrency", ex.ParamName);
		Assert.Contains("Concurrency level must be positive", ex.Message);
	}

	/// <summary>
	/// Verifies that constructing a <see cref="WorkQueueProcessor"/> with zero shutdown timeout
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithZeroShutdownTimeout_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act
		var ex =
			Assert.Throws<ArgumentOutOfRangeException>(() => new WorkQueueProcessor(
				loggerFactory: LoggerFactory,
				shutdownTimeout: TimeSpan.Zero));

		// Assert
		Assert.Equal("shutdownTimeout", ex.ParamName);
		Assert.Contains("Shutdown timeout must be positive", ex.Message);
	}

	/// <summary>
	/// Verifies that constructing a <see cref="WorkQueueProcessor"/> with negative shutdown timeout
	/// throws <see cref="ArgumentOutOfRangeException"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithNegativeShutdownTimeout_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act
		var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
			new WorkQueueProcessor(loggerFactory: LoggerFactory, shutdownTimeout: TimeSpan.FromSeconds(-5)));

		// Assert
		Assert.Equal("shutdownTimeout", ex.ParamName);
		Assert.Contains("Shutdown timeout must be positive", ex.Message);
	}
}
