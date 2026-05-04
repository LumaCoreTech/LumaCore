// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

/// <summary>
/// Unit tests for <see cref="SqlIdentifierHelper"/>.
/// </summary>
[Trait("Category", "Providers")]
public sealed class SqlIdentifierHelperTests
{
	#region QuoteSqlite()

	/// <summary>
	/// Test data for double-quote escaping (SQLite and PostgreSQL share the same quoting rules).
	/// </summary>
	public static TheoryData<string, string, string> DoubleQuoteEscaping_TestData() => new()
	{
		// Simple identifier without special characters
		{ "Simple identifier", "Users", "\"Users\"" },

		// Identifier containing an embedded double-quote
		{ "Embedded double-quote", "my\"table", "\"my\"\"table\"" },

		// Empty identifier (edge case)
		{ "Empty identifier", "", "\"\"" }
	};

	/// <summary>
	/// Verifies that <see cref="SqlIdentifierHelper.QuoteSqlite"/> wraps the identifier in double-quotes
	/// and escapes embedded double-quotes by doubling them.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The raw identifier to quote.</param>
	/// <param name="expected">The expected quoted identifier.</param>
	[Theory]
	[MemberData(nameof(DoubleQuoteEscaping_TestData))]
	public void QuoteSqlite_WhenValidIdentifier_ReturnsQuotedIdentifier(string scenario, string input, string expected)
	{
		_ = scenario;

		// Act
		string result = SqlIdentifierHelper.QuoteSqlite(input);

		// Assert
		Assert.Equal(expected, result);
	}

	/// <summary>
	/// Verifies that <see cref="SqlIdentifierHelper.QuoteSqlite"/> throws <see cref="ArgumentNullException"/>
	/// when <c>identifier</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void QuoteSqlite_WhenIdentifierIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => SqlIdentifierHelper.QuoteSqlite(null!));
		Assert.Equal("identifier", ex.ParamName);
	}

	#endregion

	#region QuotePostgres()

	/// <summary>
	/// Verifies that <see cref="SqlIdentifierHelper.QuotePostgres"/> wraps the identifier in double-quotes
	/// and escapes embedded double-quotes by doubling them.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The raw identifier to quote.</param>
	/// <param name="expected">The expected quoted identifier.</param>
	[Theory]
	[MemberData(nameof(DoubleQuoteEscaping_TestData))] // same test data as QuoteSqlite, quoting rules are the same.
	public void QuotePostgres_WhenValidIdentifier_ReturnsQuotedIdentifier(
		string scenario,
		string input,
		string expected)
	{
		_ = scenario;

		// Act
		string result = SqlIdentifierHelper.QuotePostgres(input);

		// Assert
		Assert.Equal(expected, result);
	}

	/// <summary>
	/// Verifies that <see cref="SqlIdentifierHelper.QuotePostgres"/> throws <see cref="ArgumentNullException"/>
	/// when <c>identifier</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void QuotePostgres_WhenIdentifierIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => SqlIdentifierHelper.QuotePostgres(null!));
		Assert.Equal("identifier", ex.ParamName);
	}

	#endregion

	#region QuoteSqlServer()

	/// <summary>
	/// Test data for bracket escaping (SQL Server).
	/// </summary>
	public static TheoryData<string, string, string> BracketEscaping_TestData() => new()
	{
		// Simple identifier without special characters
		{ "Simple identifier", "Users", "[Users]" },

		// Identifier containing an embedded closing bracket
		{ "Embedded closing bracket", "my]table", "[my]]table]" },

		// Empty identifier (edge case)
		{ "Empty identifier", "", "[]" }
	};

	/// <summary>
	/// Verifies that <see cref="SqlIdentifierHelper.QuoteSqlServer"/> wraps the identifier in square brackets
	/// and escapes embedded closing brackets by doubling them.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The raw identifier to quote.</param>
	/// <param name="expected">The expected quoted identifier.</param>
	[Theory]
	[MemberData(nameof(BracketEscaping_TestData))]
	public void QuoteSqlServer_WhenValidIdentifier_ReturnsQuotedIdentifier(
		string scenario,
		string input,
		string expected)
	{
		_ = scenario;

		// Act
		string result = SqlIdentifierHelper.QuoteSqlServer(input);

		// Assert
		Assert.Equal(expected, result);
	}

	/// <summary>
	/// Verifies that <see cref="SqlIdentifierHelper.QuoteSqlServer"/> throws <see cref="ArgumentNullException"/>
	/// when <c>identifier</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void QuoteSqlServer_WhenIdentifierIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => SqlIdentifierHelper.QuoteSqlServer(null!));
		Assert.Equal("identifier", ex.ParamName);
	}

	#endregion

	#region QuoteMySql()

	/// <summary>
	/// Test data for backtick escaping (MySQL).
	/// </summary>
	public static TheoryData<string, string, string> BacktickEscaping_TestData() => new()
	{
		// Simple identifier without special characters
		{ "Simple identifier", "Users", "`Users`" },

		// Identifier containing an embedded backtick
		{ "Embedded backtick", "my`table", "`my``table`" },

		// Empty identifier (edge case)
		{ "Empty identifier", "", "``" }
	};

	/// <summary>
	/// Verifies that <see cref="SqlIdentifierHelper.QuoteMySql"/> wraps the identifier in backticks
	/// and escapes embedded backticks by doubling them.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The raw identifier to quote.</param>
	/// <param name="expected">The expected quoted identifier.</param>
	[Theory]
	[MemberData(nameof(BacktickEscaping_TestData))]
	public void QuoteMySql_WhenValidIdentifier_ReturnsQuotedIdentifier(string scenario, string input, string expected)
	{
		_ = scenario;

		// Act
		string result = SqlIdentifierHelper.QuoteMySql(input);

		// Assert
		Assert.Equal(expected, result);
	}

	/// <summary>
	/// Verifies that <see cref="SqlIdentifierHelper.QuoteMySql"/> throws <see cref="ArgumentNullException"/>
	/// when <c>identifier</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void QuoteMySql_WhenIdentifierIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => SqlIdentifierHelper.QuoteMySql(null!));
		Assert.Equal("identifier", ex.ParamName);
	}

	#endregion
}
