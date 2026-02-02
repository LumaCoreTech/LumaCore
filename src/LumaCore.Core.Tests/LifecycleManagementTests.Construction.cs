// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Core.Tests;

public partial class LifecycleManagementTests
{
	#region Constructor(ILoggerFactory)

	/// <summary>
	/// Verifies that the constructor successfully creates an instance when valid arguments are provided.
	/// </summary>
	[Fact]
	public void Constructor_WithValidLoggerFactory_CreatesInstance()
	{
		// Arrange
		var loggerFactory = NullLoggerFactory.Instance;

		// Act
		var sut = new TestableLifecycleManagement(loggerFactory);

		// Assert
		AssertFreshlyConstructedState(sut);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/>
	/// when <c>loggerFactory</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerFactoryIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		ILoggerFactory loggerFactory = null!;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new TestableLifecycleManagement(loggerFactory));
		Assert.Equal("loggerFactory", ex.ParamName);
	}

	#endregion

	#region Constructor(ILoggerFactory, object)

	/// <summary>
	/// Verifies that the constructor successfully creates an instance when valid arguments are provided,
	/// and uses the provided sync object.
	/// </summary>
	[Fact]
	public void Constructor_WithValidLoggerFactoryAndSync_CreatesInstanceWithProvidedSync()
	{
		// Arrange
		var loggerFactory = NullLoggerFactory.Instance;
		object sync = new();

		// Act
		var sut = new TestableLifecycleManagement(loggerFactory, sync);

		// Assert
		AssertFreshlyConstructedState(sut, expectedSync: sync);
	}

	/// <summary>
	/// Verifies that the constructor with sync object throws <see cref="ArgumentNullException"/>
	/// when <c>loggerFactory</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithSyncWhenLoggerFactoryIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		ILoggerFactory loggerFactory = null!;
		object sync = new();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new TestableLifecycleManagement(loggerFactory, sync));
		Assert.Equal("loggerFactory", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor with sync object throws <see cref="ArgumentNullException"/>
	/// when <c>sync</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WithSyncWhenSyncIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var loggerFactory = NullLoggerFactory.Instance;
		object sync = null!;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new TestableLifecycleManagement(loggerFactory, sync));
		Assert.Equal("sync", ex.ParamName);
	}

	#endregion
}
