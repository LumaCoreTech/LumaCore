// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.BackgroundProcessing.Tests;

/// <summary>
/// Unit tests for <see cref="WorkQueueProcessorServiceCollectionExtensions"/>.
/// </summary>
public class WorkQueueProcessorServiceCollectionExtensionsTests
{
	#region Service Resolution

	/// <summary>
	/// Verifies that the processor is configured correctly with custom options.
	/// </summary>
	[Fact]
	public async Task AddWorkQueueProcessor_ProcessorUsesConfiguredOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddWorkQueueProcessor(options =>
		{
			options.MaxQueueSize = 2;
			options.MaxConcurrency = 1;
		});

		ServiceProvider provider = services.BuildServiceProvider();
		var processor = provider.GetRequiredService<WorkQueueProcessor>();

		// Act - initialize and fill queue to verify MaxQueueSize is respected
		await processor.InitializeAsync();

		var blockProcessing = new TaskCompletionSource<bool>();
		processor.QueueWorkItem(async _ => await blockProcessing.Task);
		await Task.Delay(50); // Wait for item to be picked up

		processor.QueueWorkItem(_ => Task.CompletedTask);
		processor.QueueWorkItem(_ => Task.CompletedTask);

		// Queue should be full now (maxSize=2).
		bool thirdQueued = processor.QueueWorkItem(_ => Task.CompletedTask);

		// Assert
		Assert.False(thirdQueued);

		// Cleanup
		blockProcessing.SetResult(true);
		await processor.DisposeAsync();
	}

	#endregion

	#region AddWorkQueueProcessor() - Parameterless

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorServiceCollectionExtensions.AddWorkQueueProcessor(IServiceCollection)"/>
	/// registers all required services.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_Parameterless_RegistersAllServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddWorkQueueProcessor();

		// Assert
		ServiceProvider provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetService<WorkQueueProcessor>());
		Assert.NotNull(provider.GetService<IWorkQueueProcessor>());
		Assert.NotNull(provider.GetService<IOptions<WorkQueueProcessorOptions>>());
	}

	/// <summary>
	/// Verifies that <see cref="IWorkQueueProcessor"/> resolves to the same instance as <see cref="WorkQueueProcessor"/>.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_Parameterless_InterfaceResolvesToSameInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddWorkQueueProcessor();

		// Act
		ServiceProvider provider = services.BuildServiceProvider();
		var processor = provider.GetRequiredService<WorkQueueProcessor>();
		var interfaceProcessor = provider.GetRequiredService<IWorkQueueProcessor>();

		// Assert
		Assert.Same(processor, interfaceProcessor);
	}

	/// <summary>
	/// Verifies that default options are used when no configuration is provided.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_Parameterless_UsesDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddWorkQueueProcessor();

		// Act
		ServiceProvider provider = services.BuildServiceProvider();
		WorkQueueProcessorOptions options = provider.GetRequiredService<IOptions<WorkQueueProcessorOptions>>().Value;

		// Assert
		Assert.Equal(WorkQueueProcessorOptions.DefaultMaxQueueSize, options.MaxQueueSize);
		Assert.Equal(WorkQueueProcessorOptions.DefaultMaxConcurrency, options.MaxConcurrency);
		Assert.Equal(WorkQueueProcessorOptions.DefaultShutdownTimeout, options.ShutdownTimeout);
	}

	/// <summary>
	/// Verifies that the hosted service is registered.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_Parameterless_RegistersHostedService()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddWorkQueueProcessor();

		// Act
		ServiceProvider provider = services.BuildServiceProvider();
		IEnumerable<IHostedService> hostedServices = provider.GetServices<IHostedService>();

		// Assert
		Assert.Contains(hostedServices, s => s is WorkQueueProcessorHostedService);
	}

	#endregion

	#region AddWorkQueueProcessor(Action<WorkQueueProcessorOptions>)

	/// <summary>
	/// Verifies that custom options are applied when using the configure delegate.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_WithConfigureAction_AppliesCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddWorkQueueProcessor(options =>
		{
			options.MaxQueueSize = 5000;
			options.MaxConcurrency = 4;
			options.ShutdownTimeout = TimeSpan.FromMinutes(2);
		});

		ServiceProvider provider = services.BuildServiceProvider();
		WorkQueueProcessorOptions options = provider.GetRequiredService<IOptions<WorkQueueProcessorOptions>>().Value;

		// Assert
		Assert.Equal(5000, options.MaxQueueSize);
		Assert.Equal(4, options.MaxConcurrency);
		Assert.Equal(TimeSpan.FromMinutes(2), options.ShutdownTimeout);
	}

	/// <summary>
	/// Verifies that <see cref="ArgumentNullException"/> is thrown when configure action is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_WithNullConfigureAction_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
			services.AddWorkQueueProcessor((Action<WorkQueueProcessorOptions>)null!));
		Assert.Equal("configure", ex.ParamName);
	}

	#endregion

	#region AddWorkQueueProcessor(IConfiguration, string)

	/// <summary>
	/// Verifies that options are loaded from configuration.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_WithConfiguration_LoadsOptionsFromConfig()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		var configData = new Dictionary<string, string?>
		{
			["WorkQueue:MaxQueueSize"] = "2000",
			["WorkQueue:MaxConcurrency"] = "8",
			["WorkQueue:ShutdownTimeout"] = "00:02:00"
		};
		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		// Act
		services.AddWorkQueueProcessor(configuration);

		ServiceProvider provider = services.BuildServiceProvider();
		WorkQueueProcessorOptions options = provider.GetRequiredService<IOptions<WorkQueueProcessorOptions>>().Value;

		// Assert
		Assert.Equal(2000, options.MaxQueueSize);
		Assert.Equal(8, options.MaxConcurrency);
		Assert.Equal(TimeSpan.FromMinutes(2), options.ShutdownTimeout);
	}

	/// <summary>
	/// Verifies that a custom section name can be used.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_WithCustomSectionName_LoadsFromCorrectSection()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		var configData = new Dictionary<string, string?>
		{
			["CustomSection:MaxQueueSize"] = "3000",
			["CustomSection:MaxConcurrency"] = "2"
		};
		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		// Act
		services.AddWorkQueueProcessor(configuration, "CustomSection");

		ServiceProvider provider = services.BuildServiceProvider();
		WorkQueueProcessorOptions options = provider.GetRequiredService<IOptions<WorkQueueProcessorOptions>>().Value;

		// Assert
		Assert.Equal(3000, options.MaxQueueSize);
		Assert.Equal(2, options.MaxConcurrency);
	}

	/// <summary>
	/// Verifies that <see cref="ArgumentNullException"/> is thrown when configuration is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_WithNullConfiguration_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
			services.AddWorkQueueProcessor((IConfiguration)null!));
		Assert.Equal("configuration", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ArgumentException"/> is thrown when section name is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_WithNullSectionName_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		IConfigurationRoot configuration = new ConfigurationBuilder().Build();

		// Act + Assert
		// ThrowIfNullOrWhiteSpace throws ArgumentNullException for null values
		var ex = Assert.Throws<ArgumentNullException>(() =>
			services.AddWorkQueueProcessor(configuration, null!));
		Assert.Equal("sectionName", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ArgumentException"/> is thrown when section name is empty.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_WithEmptySectionName_ThrowsArgumentException()
	{
		// Arrange
		var services = new ServiceCollection();
		IConfigurationRoot configuration = new ConfigurationBuilder().Build();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() =>
			services.AddWorkQueueProcessor(configuration, ""));
		Assert.Equal("sectionName", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ArgumentException"/> is thrown when section name is whitespace.
	/// </summary>
	[Fact]
	public void AddWorkQueueProcessor_WithWhitespaceSectionName_ThrowsArgumentException()
	{
		// Arrange
		var services = new ServiceCollection();
		IConfigurationRoot configuration = new ConfigurationBuilder().Build();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() =>
			services.AddWorkQueueProcessor(configuration, "   "));
		Assert.Equal("sectionName", ex.ParamName);
	}

	#endregion
}
