// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;
using LumaCore.Data.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Data.Tests;

// Constructor null-guards for ResourceCleanupService.
//
// The service captures six collaborators that it later dereferences across multiple async paths
// (cycle scheduling, DB scope creation, store deletion, log emission). Letting any of them slip
// through as null would surface as a delayed NullReferenceException far from the registration
// site, so each parameter has an explicit ArgumentNullException guard at the constructor entry.
// These tests pin that contract.

public sealed partial class ResourceCleanupServiceTests
{
	#region Constructor

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> rejects a <see langword="null"/>
	/// <see cref="IServiceProvider"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenServiceProviderIsNull_ThrowsArgumentNullException()
	{
		// Arrange + Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceCleanupService(
			serviceProvider: null!,
			store: new RecordingStore(),
			dbStatus: new DatabaseInitializationStatus(),
			options: Options.Create(new ResourceCleanupOptions()),
			timeProvider: TimeProvider.System,
			logger: NullLogger<ResourceCleanupService>.Instance));
		Assert.Equal("serviceProvider", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> rejects a <see langword="null"/>
	/// <see cref="IResourceStore"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenStoreIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceCleanupService(
			serviceProvider: provider,
			store: null!,
			dbStatus: new DatabaseInitializationStatus(),
			options: Options.Create(new ResourceCleanupOptions()),
			timeProvider: TimeProvider.System,
			logger: NullLogger<ResourceCleanupService>.Instance));
		Assert.Equal("store", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> rejects a <see langword="null"/>
	/// <see cref="DatabaseInitializationStatus"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenDbStatusIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceCleanupService(
			serviceProvider: provider,
			store: new RecordingStore(),
			dbStatus: null!,
			options: Options.Create(new ResourceCleanupOptions()),
			timeProvider: TimeProvider.System,
			logger: NullLogger<ResourceCleanupService>.Instance));
		Assert.Equal("dbStatus", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> rejects a <see langword="null"/>
	/// <see cref="IOptions{TOptions}"/> wrapper for <see cref="ResourceCleanupOptions"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceCleanupService(
			serviceProvider: provider,
			store: new RecordingStore(),
			dbStatus: new DatabaseInitializationStatus(),
			options: null!,
			timeProvider: TimeProvider.System,
			logger: NullLogger<ResourceCleanupService>.Instance));
		Assert.Equal("options", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> rejects a <see langword="null"/>
	/// <see cref="TimeProvider"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceCleanupService(
			serviceProvider: provider,
			store: new RecordingStore(),
			dbStatus: new DatabaseInitializationStatus(),
			options: Options.Create(new ResourceCleanupOptions()),
			timeProvider: null!,
			logger: NullLogger<ResourceCleanupService>.Instance));
		Assert.Equal("timeProvider", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceCleanupService"/> rejects a <see langword="null"/>
	/// <see cref="ILogger{TCategoryName}"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new ResourceCleanupService(
			serviceProvider: provider,
			store: new RecordingStore(),
			dbStatus: new DatabaseInitializationStatus(),
			options: Options.Create(new ResourceCleanupOptions()),
			timeProvider: TimeProvider.System,
			logger: null!));
		Assert.Equal("logger", ex.ParamName);
	}

	#endregion
}
