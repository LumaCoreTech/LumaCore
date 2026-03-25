// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Api.Features.Auth;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

/// <summary>
/// Unit tests for <see cref="TokenRevocationOptions"/>.
/// </summary>
/// <remarks>
/// These tests verify the default values, constants, and data-annotation validation
/// of <see cref="TokenRevocationOptions"/>.
/// </remarks>
[Trait("Category", "Auth")]
public sealed class TokenRevocationOptionsTests
{
	#region Constructor

	/// <summary>
	/// Verifies that a default-constructed <see cref="TokenRevocationOptions"/> instance has the expected
	/// property defaults.
	/// </summary>
	[Fact]
	public void Constructor_Initially_HasExpectedDefaults()
	{
		// Arrange + Act
		var sut = new TokenRevocationOptions();

		// Assert
		Assert.Equal(5, sut.CacheDurationSeconds);
	}

	#endregion

	#region SectionName

	/// <summary>
	/// Verifies that <see cref="TokenRevocationOptions.SectionName"/> is <c>"Jwt:TokenRevocation"</c>.
	/// </summary>
	[Fact]
	public void SectionName_Always_ReturnsExpectedValue()
	{
		// Act + Assert
		Assert.Equal("Jwt:TokenRevocation", TokenRevocationOptions.SectionName);
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
		var options = new TokenRevocationOptions();
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that validation passes at both boundaries of the allowed <c>[0, 60]</c> range.
	/// </summary>
	/// <param name="caseName">A descriptive name for the test case.</param>
	/// <param name="value">The boundary value to test.</param>
	[Theory]
	[InlineData("minimum (cache disabled)", 0)]
	[InlineData("maximum", 60)]
	public void Validate_WithBoundaryValues_Succeeds(string caseName, int value)
	{
		_ = caseName; // Used for test output readability.

		// Arrange
		var options = new TokenRevocationOptions { CacheDurationSeconds = value };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="TokenRevocationOptions.CacheDurationSeconds"/>
	/// is outside the allowed <c>[0, 60]</c> range.
	/// </summary>
	/// <param name="caseName">A descriptive name for the test case.</param>
	/// <param name="value">The invalid value to test.</param>
	[Theory]
	[InlineData("below minimum", -1)]
	[InlineData("above maximum", 61)]
	public void Validate_WhenCacheDurationSecondsIsOutOfRange_Fails(string caseName, int value)
	{
		_ = caseName; // Used for test output readability.

		// Arrange
		var options = new TokenRevocationOptions { CacheDurationSeconds = value };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("CacheDurationSeconds", memberName);
		Assert.Equal("Jwt:TokenRevocation:CacheDurationSeconds must be between 0 and 60 seconds.", error.ErrorMessage);
	}

	#endregion
}
