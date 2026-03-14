// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class ProviderOperationsIntegrationTests
{
	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations.TableExistsAsync"/> returns
	/// <see langword="false"/> for a table that does not exist in the database.
	/// </summary>
	[Fact]
	public async Task TableExistsAsync_WhenTableDoesNotExist_ReturnsFalse()
	{
		// Arrange
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			DbConnection connection = await harness.GetOpenConnectionAsync();

			// Act
			bool result = await harness.Sut.TableExistsAsync(
				              connection,
				              "NonExistentTable_12345",
				              CancellationToken.None);

			// Assert
			Assert.False(result);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations.TableExistsAsync"/> returns
	/// <see langword="true"/> for an entity table created by <c>EnsureCreatedAsync</c>.
	/// </summary>
	[Fact]
	public async Task TableExistsAsync_WhenTableExists_ReturnsTrue()
	{
		// Arrange
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			DbConnection connection = await harness.GetOpenConnectionAsync();

			// Act — "Users" is an entity table created by EnsureCreatedAsync().
			bool result = await harness.Sut.TableExistsAsync(connection, "Users", CancellationToken.None);

			// Assert
			Assert.True(result);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
