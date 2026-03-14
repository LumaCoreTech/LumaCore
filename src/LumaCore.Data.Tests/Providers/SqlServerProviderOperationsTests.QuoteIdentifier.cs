// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class SqlServerProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="SqlServerProviderOperations.QuoteIdentifier"/> throws
	/// <see cref="ArgumentNullException"/> when <c>identifier</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void QuoteIdentifier_WhenIdentifierIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var sut = new SqlServerProviderOperations();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => sut.QuoteIdentifier(null!));
		Assert.Equal("identifier", ex.ParamName);
	}

	/// <summary>
	/// Test data for <see cref="QuoteIdentifier_WhenValidIdentifier_ReturnsQuotedIdentifier"/>.
	/// SQL Server uses square bracket identifier quoting.
	/// </summary>
	public static TheoryData<string, string, string> QuoteIdentifier_TestData() => new()
	{
		// Simple identifier without special characters
		{ "Simple identifier", "Users", "[Users]" },

		// Identifier containing an embedded closing bracket
		{ "Embedded closing bracket", "my]table", "[my]]table]" },

		// Empty identifier (edge case)
		{ "Empty identifier", "", "[]" }
	};

	/// <summary>
	/// Verifies that <see cref="SqlServerProviderOperations.QuoteIdentifier"/> wraps the identifier in
	/// square brackets and escapes embedded closing brackets by doubling them.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="input">The raw identifier to quote.</param>
	/// <param name="expected">The expected quoted identifier.</param>
	[Theory]
	[MemberData(nameof(QuoteIdentifier_TestData))]
	public void QuoteIdentifier_WhenValidIdentifier_ReturnsQuotedIdentifier(
		string scenario,
		string input,
		string expected)
	{
		_ = scenario;

		// Arrange
		var sut = new SqlServerProviderOperations();

		// Act
		string result = sut.QuoteIdentifier(input);

		// Assert
		Assert.Equal(expected, result);
	}
}
