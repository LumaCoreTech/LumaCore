// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using Xunit;

namespace LumaCore.BackgroundProcessing.Tests;

/// <summary>
/// Unit tests for <see cref="WorkQueueProcessorOptions"/>.
/// </summary>
public sealed class WorkQueueProcessorOptionsTests
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

	#region Constructor

	/// <summary>
	/// Verifies that a default-constructed <see cref="WorkQueueProcessorOptions"/> instance has the expected
	/// property defaults.
	/// </summary>
	[Fact]
	public void Constructor_Initially_HasExpectedDefaults()
	{
		// Arrange + Act
		var options = new WorkQueueProcessorOptions();

		// Assert
		Assert.Equal(WorkQueueProcessorOptions.DefaultMaxQueueSize, options.MaxQueueSize);
		Assert.Equal(WorkQueueProcessorOptions.DefaultMaxConcurrency, options.MaxConcurrency);
		Assert.Equal(WorkQueueProcessorOptions.DefaultShutdownTimeout, options.ShutdownTimeout);
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

	/// <summary>
	/// Verifies that validation fails when <see cref="WorkQueueProcessorOptions.MaxQueueSize"/> is invalid.
	/// </summary>
	/// <param name="caseName">A descriptive name for the test case.</param>
	/// <param name="value">The invalid value to test.</param>
	[Theory]
	[InlineData("zero", 0)]
	[InlineData("negative", -1)]
	public void Validate_MaxQueueSize_WhenInvalid_Fails(string caseName, int value)
	{
		_ = caseName; // Used for test output readability.

		// Arrange
		var options = new WorkQueueProcessorOptions { MaxQueueSize = value };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("MaxQueueSize", memberName);
		Assert.Equal("Queue size must be at least 1.", error.ErrorMessage);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="WorkQueueProcessorOptions.MaxConcurrency"/> is invalid.
	/// </summary>
	/// <param name="caseName">A descriptive name for the test case.</param>
	/// <param name="value">The invalid value to test.</param>
	[Theory]
	[InlineData("zero", 0)]
	[InlineData("negative", -5)]
	public void Validate_MaxConcurrency_WhenInvalid_Fails(string caseName, int value)
	{
		_ = caseName; // Used for test output readability.

		// Arrange
		var options = new WorkQueueProcessorOptions { MaxConcurrency = value };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("MaxConcurrency", memberName);
		Assert.Equal("Concurrency must be at least 1.", error.ErrorMessage);
	}

	/// <summary>
	/// Test data for <see cref="Validate_ShutdownTimeout_WhenInvalid_Fails"/>.
	/// </summary>
	public static TheoryData<string, TimeSpan> InvalidShutdownTimeoutTestData => new()
	{
		{ "zero", TimeSpan.Zero },
		{ "negative", TimeSpan.FromSeconds(-5) }
	};

	/// <summary>
	/// Verifies that validation fails when <see cref="WorkQueueProcessorOptions.ShutdownTimeout"/> is invalid.
	/// </summary>
	/// <param name="caseName">A descriptive name for the test case.</param>
	/// <param name="value">The invalid value to test.</param>
	[Theory]
	[MemberData(nameof(InvalidShutdownTimeoutTestData))]
	public void Validate_ShutdownTimeout_WhenInvalid_Fails(string caseName, TimeSpan value)
	{
		_ = caseName; // Used for test output readability.

		// Arrange
		var options = new WorkQueueProcessorOptions { ShutdownTimeout = value };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("ShutdownTimeout", memberName);
		Assert.Equal("Shutdown timeout must be greater than zero.", error.ErrorMessage);
	}

	#endregion
}
