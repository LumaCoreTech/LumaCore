// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="DatabaseProviderFactory"/>.
/// </summary>
[Trait("Category", "Providers")]
public sealed class DatabaseProviderFactoryTests
{
	/// <summary>
	/// Verifies that <see cref="DatabaseProviderFactory.GetProvider"/> returns the correct
	/// <see cref="IDatabaseProviderOperations"/> implementation for each supported provider, including
	/// case-insensitive and whitespace-trimmed input.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The provider name to resolve.</param>
	/// <param name="expectedType">The expected concrete type of the returned operations instance.</param>
	/// <param name="expectedProviderName">The expected <see cref="IDatabaseProviderOperations.ProviderName"/>.</param>
	[Theory]
	[MemberData(nameof(GetProvider_ValidProviders_TestData))]
	public void GetProvider_WhenValidProvider_ReturnsCorrectOperationsType(
		string scenario,
		string input,
		Type   expectedType,
		string expectedProviderName)
	{
		_ = scenario;

		// Act
		IDatabaseProviderOperations result = DatabaseProviderFactory.GetProvider(input);

		// Assert
		Assert.IsType(expectedType, result);
		Assert.Equal(expectedProviderName, result.ProviderName);
	}

	/// <summary>
	/// Test data for <see cref="GetProvider_WhenValidProvider_ReturnsCorrectOperationsType"/>.
	/// Covers all supported providers, case-insensitivity, and whitespace trimming.
	/// </summary>
	public static TheoryData<string, string, Type, string> GetProvider_ValidProviders_TestData() => new()
	{
		// SQLite: lowercase
		{
			"SQLite lowercase",
			"sqlite",
			typeof(SqliteProviderOperations),
			DatabaseProviders.Sqlite
		},

		// SQLite: uppercase (case-insensitive)
		{
			"SQLite uppercase",
			"SQLITE",
			typeof(SqliteProviderOperations),
			DatabaseProviders.Sqlite
		},

		// SQLite: with leading/trailing whitespace (trimmed)
		{
			"SQLite with whitespace",
			" sqlite ",
			typeof(SqliteProviderOperations),
			DatabaseProviders.Sqlite
		},

		// PostgreSQL
		{
			"PostgreSQL",
			"postgresql",
			typeof(PostgreSqlProviderOperations),
			DatabaseProviders.PostgreSql
		},

		// SQL Server
		{
			"SQL Server",
			"sqlserver",
			typeof(SqlServerProviderOperations),
			DatabaseProviders.SqlServer
		},

		// MySQL
		{
			"MySQL",
			"mysql",
			typeof(MySqlProviderOperations),
			DatabaseProviders.MySql
		}
	};

	/// <summary>
	/// Verifies that <see cref="DatabaseProviderFactory.GetProvider"/> throws
	/// <see cref="ArgumentNullException"/> when <c>providerName</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void GetProvider_WhenProviderNameIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => DatabaseProviderFactory.GetProvider(null!));
		Assert.Equal("providerName", ex.ParamName);
	}

	/// <summary>
	/// Test data for <see cref="GetProvider_WhenUnsupportedProvider_ThrowsInvalidOperationException"/>.
	/// Covers unknown provider names, empty strings, and whitespace-only strings.
	/// </summary>
	public static TheoryData<string, string, string> GetProvider_UnsupportedProviders_TestData() => new()
	{
		// Unknown provider name
		{
			"Unknown provider",
			"oracle",
			"Unsupported database provider: 'oracle'. Supported providers: sqlite, postgresql, sqlserver."
		},

		// Empty string (after Trim().ToLowerInvariant() → "", falls through to default case)
		{
			"Empty string",
			"",
			"Unsupported database provider: ''. Supported providers: sqlite, postgresql, sqlserver."
		},

		// Whitespace-only (after Trim() → "", but original input is preserved in error message)
		{
			"Whitespace-only",
			"   ",
			"Unsupported database provider: '   '. Supported providers: sqlite, postgresql, sqlserver."
		}
	};

	/// <summary>
	/// Verifies that <see cref="DatabaseProviderFactory.GetProvider"/> throws
	/// <see cref="InvalidOperationException"/> for an unsupported provider name, and the message includes
	/// both the invalid name and the list of supported providers.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The unsupported provider name.</param>
	/// <param name="expectedMessage">The expected exception message.</param>
	[Theory]
	[MemberData(nameof(GetProvider_UnsupportedProviders_TestData))]
	public void GetProvider_WhenUnsupportedProvider_ThrowsInvalidOperationException(
		string scenario,
		string input,
		string expectedMessage)
	{
		_ = scenario;

		// Act + Assert
		var ex = Assert.Throws<InvalidOperationException>(() => DatabaseProviderFactory.GetProvider(input));
		Assert.Equal(expectedMessage, ex.Message);
	}
}
