// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="SqlServerProviderOperations"/>.
/// </summary>
[Trait("Category", "Providers")]
public sealed partial class SqlServerProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="SqlServerProviderOperations.ProviderName"/> returns
	/// <see cref="DatabaseProviders.SqlServer"/>.
	/// </summary>
	[Fact]
	public void ProviderName_Always_ReturnsSqlServer()
	{
		// Arrange
		var sut = new SqlServerProviderOperations();

		// Act + Assert
		Assert.Equal(DatabaseProviders.SqlServer, sut.ProviderName);
	}
}
