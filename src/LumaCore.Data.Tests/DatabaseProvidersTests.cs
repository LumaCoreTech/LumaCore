// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Data.Tests;

/// <summary>
/// Unit tests for <see cref="DatabaseProviders"/>.
/// </summary>
public sealed class DatabaseProvidersTests
{
	/// <summary>
	/// Verifies that <see cref="DatabaseProviders.GetSupportedProviders"/> returns a comma-separated list
	/// containing only the currently enabled provider identifiers (MySQL is temporarily excluded).
	/// </summary>
	[Fact]
	public void GetSupportedProviders_Always_ReturnsEnabledProviders()
	{
		// Act
		string result = DatabaseProviders.GetSupportedProviders();

		// Assert
		Assert.Equal("sqlite, postgresql, sqlserver", result);
	}

	/// <summary>
	/// Verifies that the provider constants match their expected string values.
	/// These constants are referenced throughout the codebase and must remain stable.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="actual">The constant value to verify.</param>
	/// <param name="expected">The expected string value.</param>
	[Theory]
	[MemberData(nameof(ProviderConstants_TestData))]
	public void ProviderConstants_Always_MatchExpectedValues(string scenario, string actual, string expected)
	{
		_ = scenario;

		// Act + Assert
		Assert.Equal(expected, actual);
	}

	/// <summary>
	/// Test data for <see cref="ProviderConstants_Always_MatchExpectedValues"/>.
	/// </summary>
	public static TheoryData<string, string, string> ProviderConstants_TestData() => new()
	{
		// SQLite provider identifier
		{ "Sqlite", DatabaseProviders.Sqlite, "sqlite" },

		// PostgreSQL provider identifier
		{ "PostgreSql", DatabaseProviders.PostgreSql, "postgresql" },

		// SQL Server provider identifier
		{ "SqlServer", DatabaseProviders.SqlServer, "sqlserver" },

		// MySQL provider identifier
		{ "MySql", DatabaseProviders.MySql, "mysql" }
	};
}
