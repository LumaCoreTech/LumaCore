// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Data.Tests;

/// <summary>
/// Unit tests for <see cref="UtcDateTimeConverter"/>.
/// </summary>
/// <remarks>
/// The converter stamps <see cref="DateTimeKind.Utc"/> on both read and write without altering the tick value.
/// These tests exercise both the "to provider" (write) and "from provider" (read) conversion expressions
/// across all three <see cref="DateTimeKind"/> values.
/// </remarks>
public sealed class UtcDateTimeConverterTests
{
	/// <summary>
	/// Test data for <see cref="ConvertToProvider_WhenCalled_StampsUtcWithoutChangingTicks"/> and
	/// <see cref="ConvertFromProvider_WhenCalled_StampsUtcWithoutChangingTicks"/>.
	/// Each row covers a different <see cref="DateTimeKind"/> input.
	/// </summary>
	public static TheoryData<string, DateTimeKind> DateTimeKind_TestData() => new()
	{
		// Already UTC — should pass through unchanged.
		{ "Utc", DateTimeKind.Utc },

		// Local — kind should be overwritten to Utc without altering ticks.
		{ "Local", DateTimeKind.Local },

		// Unspecified — the most common case (e.g., values read from SQLite or SQL Server).
		{ "Unspecified", DateTimeKind.Unspecified }
	};

	#region ConvertToProvider() (write path)

	/// <summary>
	/// Verifies that the write conversion stamps <see cref="DateTimeKind.Utc"/> on the output
	/// while preserving the original tick value, regardless of the input <see cref="DateTimeKind"/>.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="inputKind">The <see cref="DateTimeKind"/> of the input value.</param>
	[Theory]
	[MemberData(nameof(DateTimeKind_TestData))]
	public void ConvertToProvider_WhenCalled_StampsUtcWithoutChangingTicks(string scenario, DateTimeKind inputKind)
	{
		_ = scenario;

		// Arrange
		var sut = new UtcDateTimeConverter();
		var input = new DateTime(2026, 6, 15, 12, 30, 45, inputKind);
		Func<DateTime, DateTime> convert = sut.ConvertToProviderTyped;

		// Act
		DateTime result = convert(input);

		// Assert
		Assert.Equal(DateTimeKind.Utc, result.Kind);
		Assert.Equal(input.Ticks, result.Ticks);
	}

	#endregion

	#region ConvertFromProvider() (read path)

	/// <summary>
	/// Verifies that the read conversion stamps <see cref="DateTimeKind.Utc"/> on the output
	/// while preserving the original tick value, regardless of the input <see cref="DateTimeKind"/>.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="inputKind">The <see cref="DateTimeKind"/> of the input value.</param>
	[Theory]
	[MemberData(nameof(DateTimeKind_TestData))]
	public void ConvertFromProvider_WhenCalled_StampsUtcWithoutChangingTicks(string scenario, DateTimeKind inputKind)
	{
		_ = scenario;

		// Arrange
		var sut = new UtcDateTimeConverter();
		var input = new DateTime(2026, 6, 15, 12, 30, 45, inputKind);
		Func<DateTime, DateTime> convert = sut.ConvertFromProviderTyped;

		// Act
		DateTime result = convert(input);

		// Assert
		Assert.Equal(DateTimeKind.Utc, result.Kind);
		Assert.Equal(input.Ticks, result.Ticks);
	}

	#endregion
}
