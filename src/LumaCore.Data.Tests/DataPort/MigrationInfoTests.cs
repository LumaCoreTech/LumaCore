// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Unit tests for <see cref="MigrationInfo"/>.
/// </summary>
[Trait("Category", "DataPort")]
public sealed class MigrationInfoTests
{
	/// <summary>
	/// Verifies that the constructor stores <see cref="MigrationInfo.MigrationId"/> and
	/// <see cref="MigrationInfo.ProductVersion"/> correctly.
	/// </summary>
	[Fact]
	public void Constructor_WhenValid_SetsProperties()
	{
		// Arrange + Act
		var sut = new MigrationInfo("20260126214435_InitialCreate", "10.0.0");

		// Assert
		Assert.Equal("20260126214435_InitialCreate", sut.MigrationId);
		Assert.Equal("10.0.0", sut.ProductVersion);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when
	/// <see cref="MigrationInfo.MigrationId"/> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenMigrationIdIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new MigrationInfo(null!, "10.0.0"));
		Assert.Equal("MigrationId", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when
	/// <see cref="MigrationInfo.ProductVersion"/> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenProductVersionIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new MigrationInfo("20260126_Init", null!));
		Assert.Equal("ProductVersion", ex.ParamName);
	}
}
