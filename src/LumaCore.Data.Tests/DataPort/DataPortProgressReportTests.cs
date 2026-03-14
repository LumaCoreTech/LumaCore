// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Models;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Unit tests for the computed properties of <see cref="DataPortProgressReport"/>.
/// </summary>
[Trait("Category", "DataPort")]
public sealed class DataPortProgressReportTests
{
	#region OverallPercentage

	/// <summary>
	/// Verifies that <see cref="DataPortProgressReport.OverallPercentage"/> returns <c>0</c> when
	/// <see cref="DataPortProgressReport.OverallTotalSteps"/> is <c>0</c> (avoids division by zero).
	/// </summary>
	[Fact]
	public void OverallPercentage_WhenTotalStepsIsZero_ReturnsZero()
	{
		// Arrange
		var sut = new DataPortProgressReport { OverallTotalSteps = 0, OverallCurrentStep = 0 };

		// Act
		double result = sut.OverallPercentage;

		// Assert
		Assert.Equal(0.0, result);
	}

	/// <summary>
	/// Test data for <see cref="OverallPercentage_WhenInProgress_ReturnsCorrectPercentage"/>.
	/// </summary>
	public static TheoryData<string, int, int, double> OverallPercentage_TestData() => new()
	{
		// At the start (0%)
		{ "At start", 0, 10, 0.0 },

		// Halfway through (50%)
		{ "Halfway", 5, 10, 50.0 },

		// Fully completed (100%)
		{ "Completed", 10, 10, 100.0 }
	};

	/// <summary>
	/// Verifies that <see cref="DataPortProgressReport.OverallPercentage"/> calculates the correct percentage
	/// for various step values.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="currentStep">The current step value.</param>
	/// <param name="totalSteps">The total step count.</param>
	/// <param name="expectedPercentage">The expected percentage value.</param>
	[Theory]
	[MemberData(nameof(OverallPercentage_TestData))]
	public void OverallPercentage_WhenInProgress_ReturnsCorrectPercentage(
		string scenario,
		int    currentStep,
		int    totalSteps,
		double expectedPercentage)
	{
		_ = scenario;

		// Arrange
		var sut = new DataPortProgressReport
		{
			OverallCurrentStep = currentStep,
			OverallTotalSteps = totalSteps
		};

		// Act
		double result = sut.OverallPercentage;

		// Assert
		Assert.Equal(expectedPercentage, result);
	}

	#endregion

	#region DetailedPercentage

	/// <summary>
	/// Test data for <see cref="DetailedPercentage_WhenNoValidTotal_ReturnsNull"/>.
	/// </summary>
	public static TheoryData<string, long?> DetailedPercentage_NoTotal_TestData() => new()
	{
		// Total is null (unknown)
		{ "Null total", null },

		// Total is zero
		{ "Zero total", 0L },

		// Total is negative
		{ "Negative total", -1L }
	};

	/// <summary>
	/// Verifies that <see cref="DataPortProgressReport.DetailedPercentage"/> returns <see langword="null"/>
	/// when the total step count is unknown or invalid (not greater than zero).
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="totalSteps">The total step count to test.</param>
	[Theory]
	[MemberData(nameof(DetailedPercentage_NoTotal_TestData))]
	public void DetailedPercentage_WhenNoValidTotal_ReturnsNull(string scenario, long? totalSteps)
	{
		_ = scenario;

		// Arrange
		var sut = new DataPortProgressReport
		{
			DetailedTotalSteps = totalSteps,
			DetailedCurrentStep = 50
		};

		// Act
		double? result = sut.DetailedPercentage;

		// Assert
		Assert.Null(result);
	}

	/// <summary>
	/// Test data for <see cref="DetailedPercentage_WhenCurrentMeetsOrExceedsTotal_Returns100"/>.
	/// </summary>
	public static TheoryData<string, long?> DetailedPercentage_Capped_TestData() => new()
	{
		// Exactly at total
		{ "Equals total", 100L },

		// Exceeds total (estimate was off)
		{ "Exceeds total", 150L }
	};

	/// <summary>
	/// Verifies that <see cref="DataPortProgressReport.DetailedPercentage"/> is capped at <c>100.0</c>
	/// when <see cref="DataPortProgressReport.DetailedCurrentStep"/> meets or exceeds
	/// <see cref="DataPortProgressReport.DetailedTotalSteps"/>.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="currentStep">The current step value.</param>
	[Theory]
	[MemberData(nameof(DetailedPercentage_Capped_TestData))]
	public void DetailedPercentage_WhenCurrentMeetsOrExceedsTotal_Returns100(string scenario, long? currentStep)
	{
		_ = scenario;

		// Arrange
		var sut = new DataPortProgressReport
		{
			DetailedTotalSteps = 100,
			DetailedCurrentStep = currentStep
		};

		// Act
		double? result = sut.DetailedPercentage;

		// Assert
		Assert.Equal(100.0, result);
	}

	/// <summary>
	/// Test data for <see cref="DetailedPercentage_WhenInProgress_ReturnsCorrectPercentage"/>.
	/// </summary>
	public static TheoryData<string, long?, long, double> DetailedPercentage_InProgress_TestData() => new()
	{
		// CurrentStep is null — GetValueOrDefault() returns 0
		{ "Null current step", null, 100L, 0.0 },

		// At the start (0%)
		{ "At start", 0L, 100L, 0.0 },

		// Halfway through (50%)
		{ "Halfway", 50L, 100L, 50.0 },

		// Quarter through (25%)
		{ "Quarter", 25L, 100L, 25.0 }
	};

	/// <summary>
	/// Verifies that <see cref="DataPortProgressReport.DetailedPercentage"/> calculates the correct percentage
	/// for various step values.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="currentStep">The current step value.</param>
	/// <param name="totalSteps">The total step count.</param>
	/// <param name="expectedPercentage">The expected percentage value.</param>
	[Theory]
	[MemberData(nameof(DetailedPercentage_InProgress_TestData))]
	public void DetailedPercentage_WhenInProgress_ReturnsCorrectPercentage(
		string scenario,
		long?  currentStep,
		long   totalSteps,
		double expectedPercentage)
	{
		_ = scenario;

		// Arrange
		var sut = new DataPortProgressReport
		{
			DetailedCurrentStep = currentStep,
			DetailedTotalSteps = totalSteps
		};

		// Act
		double? result = sut.DetailedPercentage;

		// Assert
		Assert.Equal(expectedPercentage, result);
	}

	#endregion
}
