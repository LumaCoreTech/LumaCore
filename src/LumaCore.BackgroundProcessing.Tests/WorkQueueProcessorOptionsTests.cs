// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using Xunit;

// ReSharper disable UseObjectOrCollectionInitializer

namespace LumaCore.BackgroundProcessing.Tests;

/// <summary>
/// Unit tests for <see cref="WorkQueueProcessorOptions"/>.
/// </summary>
public class WorkQueueProcessorOptionsTests
{
	#region Default Values

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorOptions.DefaultSectionName"/> has the expected value.
	/// </summary>
	[Fact]
	public void DefaultSectionName_HasExpectedValue()
	{
		// Assert
		Assert.Equal("WorkQueue", WorkQueueProcessorOptions.DefaultSectionName);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorOptions.DefaultMaxQueueSize"/> has the expected value.
	/// </summary>
	[Fact]
	public void DefaultMaxQueueSize_HasExpectedValue()
	{
		// Assert
		Assert.Equal(10000, WorkQueueProcessorOptions.DefaultMaxQueueSize);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorOptions.DefaultMaxConcurrency"/> has the expected value.
	/// </summary>
	[Fact]
	public void DefaultMaxConcurrency_HasExpectedValue()
	{
		// Assert
		Assert.Equal(1, WorkQueueProcessorOptions.DefaultMaxConcurrency);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorOptions.DefaultShutdownTimeout"/> has the expected value.
	/// </summary>
	[Fact]
	public void DefaultShutdownTimeout_HasExpectedValue()
	{
		// Assert
		Assert.Equal(TimeSpan.FromSeconds(30), WorkQueueProcessorOptions.DefaultShutdownTimeout);
	}

	#endregion

	#region Property Setters

	/// <summary>
	/// Verifies that a new <see cref="WorkQueueProcessorOptions"/> instance has correct default property values.
	/// </summary>
	[Fact]
	public void Constructor_CreatesInstanceWithDefaultValues()
	{
		// Act
		var options = new WorkQueueProcessorOptions();

		// Assert
		Assert.Equal(WorkQueueProcessorOptions.DefaultMaxQueueSize, options.MaxQueueSize);
		Assert.Equal(WorkQueueProcessorOptions.DefaultMaxConcurrency, options.MaxConcurrency);
		Assert.Equal(WorkQueueProcessorOptions.DefaultShutdownTimeout, options.ShutdownTimeout);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorOptions.MaxQueueSize"/> can be set.
	/// </summary>
	[Fact]
	public void MaxQueueSize_CanBeSet()
	{
		// Arrange
		var options = new WorkQueueProcessorOptions();

		// Act
		options.MaxQueueSize = 5000;

		// Assert
		Assert.Equal(5000, options.MaxQueueSize);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorOptions.MaxConcurrency"/> can be set.
	/// </summary>
	[Fact]
	public void MaxConcurrency_CanBeSet()
	{
		// Arrange
		var options = new WorkQueueProcessorOptions();

		// Act
		options.MaxConcurrency = 8;

		// Assert
		Assert.Equal(8, options.MaxConcurrency);
	}

	/// <summary>
	/// Verifies that <see cref="WorkQueueProcessorOptions.ShutdownTimeout"/> can be set.
	/// </summary>
	[Fact]
	public void ShutdownTimeout_CanBeSet()
	{
		// Arrange
		var options = new WorkQueueProcessorOptions();

		// Act
		options.ShutdownTimeout = TimeSpan.FromMinutes(2);

		// Assert
		Assert.Equal(TimeSpan.FromMinutes(2), options.ShutdownTimeout);
	}

	#endregion

	#region Validation

	/// <summary>
	/// Verifies that validation passes with default values.
	/// </summary>
	[Fact]
	public void Validate_WithDefaultValues_Succeeds()
	{
		// Arrange
		var options = new WorkQueueProcessorOptions();
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="WorkQueueProcessorOptions.MaxQueueSize"/> is invalid.
	/// </summary>
	/// <param name="value">The invalid value to test.</param>
	/// <param name="caseName">A descriptive name for the test case.</param>
	[Theory]
	[InlineData(0, "zero")]
	[InlineData(-1, "negative")]
	public void Validate_WhenMaxQueueSizeIsInvalid_Fails(int value, string caseName)
	{
		_ = caseName; // Used for test output

		// Arrange
		var options = new WorkQueueProcessorOptions { MaxQueueSize = value };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		Assert.Single(results);
		Assert.Contains("MaxQueueSize", results[0].MemberNames);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="WorkQueueProcessorOptions.MaxConcurrency"/> is invalid.
	/// </summary>
	/// <param name="value">The invalid value to test.</param>
	/// <param name="caseName">A descriptive name for the test case.</param>
	[Theory]
	[InlineData(0, "zero")]
	[InlineData(-5, "negative")]
	public void Validate_WhenMaxConcurrencyIsInvalid_Fails(int value, string caseName)
	{
		_ = caseName; // Used for test output

		// Arrange
		var options = new WorkQueueProcessorOptions { MaxConcurrency = value };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		Assert.Single(results);
		Assert.Contains("MaxConcurrency", results[0].MemberNames);
	}

	/// <summary>
	/// Test data for <see cref="Validate_WhenShutdownTimeoutIsInvalid_Fails"/>.
	/// </summary>
	public static TheoryData<TimeSpan, string> InvalidShutdownTimeoutTestData => new()
	{
		{ TimeSpan.Zero, "zero" },
		{ TimeSpan.FromSeconds(-5), "negative" }
	};

	/// <summary>
	/// Verifies that validation fails when <see cref="WorkQueueProcessorOptions.ShutdownTimeout"/> is invalid.
	/// </summary>
	/// <param name="value">The invalid value to test.</param>
	/// <param name="caseName">A descriptive name for the test case.</param>
	[Theory]
	[MemberData(nameof(InvalidShutdownTimeoutTestData))]
	public void Validate_WhenShutdownTimeoutIsInvalid_Fails(TimeSpan value, string caseName)
	{
		_ = caseName; // Used for test output

		// Arrange
		var options = new WorkQueueProcessorOptions { ShutdownTimeout = value };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		Assert.Single(results);
		Assert.Contains("ShutdownTimeout", results[0].MemberNames);
	}

	/// <summary>
	/// Verifies that validation passes with edge case values (minimum valid values).
	/// </summary>
	[Fact]
	public void Validate_WithMinimumValidValues_Succeeds()
	{
		// Arrange
		var options = new WorkQueueProcessorOptions
		{
			MaxQueueSize = 1,
			MaxConcurrency = 1,
			ShutdownTimeout = TimeSpan.FromMilliseconds(1)
		};
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	#endregion
}
