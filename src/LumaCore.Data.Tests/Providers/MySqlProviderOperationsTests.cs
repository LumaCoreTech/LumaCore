// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="MySqlProviderOperations"/>.
/// </summary>
[Trait("Category", "Providers")]
public sealed partial class MySqlProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="MySqlProviderOperations.ProviderName"/> returns
	/// <see cref="DatabaseProviders.MySql"/>.
	/// </summary>
	[Fact]
	public void ProviderName_Always_ReturnsMySql()
	{
		// Arrange
		var sut = new MySqlProviderOperations();

		// Act + Assert
		Assert.Equal(DatabaseProviders.MySql, sut.ProviderName);
	}
}
