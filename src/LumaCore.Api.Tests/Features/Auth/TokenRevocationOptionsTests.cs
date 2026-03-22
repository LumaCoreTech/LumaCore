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
/// These tests verify the default values, property setters, and data-annotation validation
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

	#region CacheDurationSeconds

	/// <summary>
	/// Verifies that <see cref="TokenRevocationOptions.CacheDurationSeconds"/> can be set to a custom value.
	/// </summary>
	[Fact]
	public void CacheDurationSeconds_WhenSet_StoresValue()
	{
		// Arrange
		var sut = new TokenRevocationOptions();

		// Act
		sut.CacheDurationSeconds = 30;

		// Assert
		Assert.Equal(30, sut.CacheDurationSeconds);
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
	/// <param name="value">The boundary value to test.</param>
	/// <param name="caseName">A descriptive name for the test case.</param>
	[Theory]
	[InlineData(0, "minimum (cache disabled)")]
	[InlineData(60, "maximum")]
	public void Validate_WithBoundaryValues_Succeeds(int value, string caseName)
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
	/// <param name="value">The invalid value to test.</param>
	/// <param name="caseName">A descriptive name for the test case.</param>
	[Theory]
	[InlineData(-1, "below minimum")]
	[InlineData(61, "above maximum")]
	public void Validate_WhenCacheDurationSecondsIsOutOfRange_Fails(int value, string caseName)
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
