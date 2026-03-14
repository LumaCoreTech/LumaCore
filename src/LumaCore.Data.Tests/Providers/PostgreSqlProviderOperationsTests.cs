// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="PostgreSqlProviderOperations"/>.
/// </summary>
[Trait("Category", "Providers")]
public sealed partial class PostgreSqlProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="PostgreSqlProviderOperations.ProviderName"/> returns
	/// <see cref="DatabaseProviders.PostgreSql"/>.
	/// </summary>
	[Fact]
	public void ProviderName_Always_ReturnsPostgreSql()
	{
		// Arrange
		var sut = new PostgreSqlProviderOperations();

		// Act + Assert
		Assert.Equal(DatabaseProviders.PostgreSql, sut.ProviderName);
	}
}
