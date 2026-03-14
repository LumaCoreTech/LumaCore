// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="SqliteProviderOperations"/>.
/// </summary>
[Trait("Category", "Providers")]
public sealed partial class SqliteProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.ProviderName"/> returns
	/// <see cref="DatabaseProviders.Sqlite"/>.
	/// </summary>
	[Fact]
	public void ProviderName_Always_ReturnsSqlite()
	{
		// Arrange
		var sut = new SqliteProviderOperations();

		// Act + Assert
		Assert.Equal(DatabaseProviders.Sqlite, sut.ProviderName);
	}
}
