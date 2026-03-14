// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Unit tests for <see cref="DataPortSchemaMismatchException"/>.
/// </summary>
[Trait("Category", "DataPort")]
public sealed class DataPortSchemaMismatchExceptionTests
{
	/// <summary>
	/// Verifies that the constructor stores <see cref="DataPortSchemaMismatchException.ShuttleMigrationHistory"/>
	/// and <see cref="DataPortSchemaMismatchException.TargetMigrationHistory"/> correctly.
	/// </summary>
	[Fact]
	public void Constructor_WhenValid_StoresHistoryProperties()
	{
		// Arrange
		MigrationInfo[] shuttle = [new("20260126_Init", "10.0.0")];
		MigrationInfo[] target = [new("20260126_Init", "10.0.0"), new("20260201_AddUsers", "10.0.0")];

		// Act
		var sut = new DataPortSchemaMismatchException(shuttle, target);

		// Assert
		Assert.Same(shuttle, sut.ShuttleMigrationHistory);
		Assert.Same(target, sut.TargetMigrationHistory);
	}

	/// <summary>
	/// Test data for <see cref="Constructor_WhenCalled_SetsMessageAndFirstMismatchIndex"/>.
	/// Covers all branches of the internal <c>CreateMessage()</c> method.
	/// </summary>
	public static TheoryData<string, MigrationInfo[], MigrationInfo[], string, int?>
		Constructor_MessageVariations_TestData() => new()
	{
		// Mismatch detected at the first index
		{
			"Mismatch at first index",
			[new MigrationInfo("A", "1.0"), new MigrationInfo("B", "1.0")],
			[new MigrationInfo("C", "1.0"), new MigrationInfo("B", "1.0")],
			"Schema version mismatch at migration index 0.",
			0
		},

		// Mismatch detected at a later index (common prefix matches)
		{
			"Mismatch at later index",
			[new MigrationInfo("A", "1.0"), new MigrationInfo("B", "1.0")],
			[new MigrationInfo("A", "1.0"), new MigrationInfo("C", "1.0")],
			"Schema version mismatch at migration index 1.",
			1
		},

		// Different lengths but common prefix matches — length mismatch message
		{
			"Different lengths with matching prefix",
			[new MigrationInfo("A", "1.0")],
			[new MigrationInfo("A", "1.0"), new MigrationInfo("B", "1.0")],
			"Schema version mismatch due to different migration history length.",
			null
		},

		// Same content and same length — generic fallback message
		{
			"Same content same length",
			[new MigrationInfo("A", "1.0")],
			[new MigrationInfo("A", "1.0")],
			"Schema version mismatch.",
			null
		},

		// Both empty — edge case of same length (0 == 0)
		{
			"Both empty",
			[],
			[],
			"Schema version mismatch.",
			null
		}
	};

	/// <summary>
	/// Verifies that the constructor produces the correct <see cref="Exception.Message"/> and
	/// <see cref="DataPortSchemaMismatchException.FirstMismatchIndex"/> for various migration history
	/// combinations.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="shuttle">The shuttle migration history.</param>
	/// <param name="target">The target migration history.</param>
	/// <param name="expectedMessage">The expected exception message.</param>
	/// <param name="expectedMismatchIndex">
	/// The expected <see cref="DataPortSchemaMismatchException.FirstMismatchIndex"/>.
	/// </param>
	[Theory]
	[MemberData(nameof(Constructor_MessageVariations_TestData))]
	public void Constructor_WhenCalled_SetsMessageAndFirstMismatchIndex(
		string          scenario,
		MigrationInfo[] shuttle,
		MigrationInfo[] target,
		string          expectedMessage,
		int?            expectedMismatchIndex)
	{
		_ = scenario;

		// Act
		var sut = new DataPortSchemaMismatchException(shuttle, target);

		// Assert
		Assert.Equal(expectedMessage, sut.Message);
		Assert.Equal(expectedMismatchIndex, sut.FirstMismatchIndex);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when
	/// <c>shuttleMigrationHistory</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenShuttleHistoryIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		MigrationInfo[] target = [new("20260126_Init", "10.0.0")];

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new DataPortSchemaMismatchException(null!, target));
		Assert.Equal("shuttleMigrationHistory", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when
	/// <c>targetMigrationHistory</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenTargetHistoryIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		MigrationInfo[] shuttle = [new("20260126_Init", "10.0.0")];

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new DataPortSchemaMismatchException(shuttle, null!));
		Assert.Equal("targetMigrationHistory", ex.ParamName);
	}
}
