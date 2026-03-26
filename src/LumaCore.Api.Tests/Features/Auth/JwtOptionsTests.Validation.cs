// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Api.Features.Auth;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

// Validation behavior: grouped by the property being validated.
// Within each group, valid scenarios precede invalid ones.
//
//   1. General: fully configured options pass (WithFullyConfiguredOptions).
//   2. AccessTokenLifetimeMinutes: boundary values pass (WithBoundaryValues),
//      out-of-range values rejected (WhenOutOfRange).
//   3. Audience: empty value rejected (WhenEmpty).
//   4. Issuer: empty value rejected (WhenEmpty).
//   5. SigningKey: empty value rejected (WhenEmpty),
//      too-short value rejected (WhenTooShort).
//
// For constructor defaults and property setters, see the anchor file (JwtOptionsTests.cs).
// For the CreateValidOptions() factory, see Helpers.
public sealed partial class JwtOptionsTests
{
	// --- 1. General ---

	/// <summary>
	/// Verifies that a fully configured <see cref="JwtOptions"/> instance passes validation.
	/// </summary>
	[Fact]
	public void Validate_WithFullyConfiguredOptions_Succeeds()
	{
		// Arrange
		JwtOptions options = CreateValidOptions();
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	// --- 2. AccessTokenLifetimeMinutes ---

	/// <summary>
	/// Verifies that <see cref="JwtOptions.AccessTokenLifetimeMinutes"/> passes validation at the boundary values
	/// of the allowed range (1 and 1440).
	/// </summary>
	/// <param name="lifetime">The boundary lifetime value to test.</param>
	[Theory]
	[InlineData(1)]    // Lower boundary
	[InlineData(1440)] // Upper boundary (24 hours)
	public void Validate_AccessTokenLifetimeMinutes_WithBoundaryValues_Succeeds(int lifetime)
	{
		// Arrange
		JwtOptions options = CreateValidOptions();
		options.AccessTokenLifetimeMinutes = lifetime;
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="JwtOptions.AccessTokenLifetimeMinutes"/> is outside the
	/// allowed range.
	/// </summary>
	/// <param name="lifetime">The out-of-range lifetime value to test.</param>
	[Theory]
	[InlineData(0)]    // Below minimum
	[InlineData(1441)] // Above maximum
	public void Validate_AccessTokenLifetimeMinutes_WhenOutOfRange_Fails(int lifetime)
	{
		// Arrange
		JwtOptions options = CreateValidOptions();
		options.AccessTokenLifetimeMinutes = lifetime;
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("AccessTokenLifetimeMinutes", memberName);
		Assert.Equal(
			"Jwt:AccessTokenLifetimeMinutes must be between 1 and 1440 minutes.",
			error.ErrorMessage);
	}

	// --- 3. Audience ---

	/// <summary>
	/// Verifies that validation fails when <see cref="JwtOptions.Audience"/> is empty.
	/// </summary>
	[Fact]
	public void Validate_Audience_WhenEmpty_Fails()
	{
		// Arrange
		JwtOptions options = CreateValidOptions();
		options.Audience = string.Empty;
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("Audience", memberName);
		Assert.Equal(
			"Jwt:Audience must be configured. Set configuration key 'Jwt:Audience' or environment variable 'Jwt__Audience'.",
			error.ErrorMessage);
	}

	// --- 4. Issuer ---

	/// <summary>
	/// Verifies that validation fails when <see cref="JwtOptions.Issuer"/> is empty.
	/// </summary>
	[Fact]
	public void Validate_Issuer_WhenEmpty_Fails()
	{
		// Arrange
		JwtOptions options = CreateValidOptions();
		options.Issuer = string.Empty;
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("Issuer", memberName);
		Assert.Equal(
			"Jwt:Issuer must be configured. Set configuration key 'Jwt:Issuer' or environment variable 'Jwt__Issuer'.",
			error.ErrorMessage);
	}

	// --- 5. SigningKey ---

	/// <summary>
	/// Verifies that validation fails when <see cref="JwtOptions.SigningKey"/> is empty.
	/// </summary>
	[Fact]
	public void Validate_SigningKey_WhenEmpty_Fails()
	{
		// Arrange
		JwtOptions options = CreateValidOptions();
		options.SigningKey = string.Empty;
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("SigningKey", memberName);
		Assert.Equal(
			"Jwt:SigningKey must be configured. "
			+ "Set configuration key 'Jwt:SigningKey' or environment variable 'Jwt__SigningKey'.",
			error.ErrorMessage);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="JwtOptions.SigningKey"/> is shorter than the required
	/// 32 characters.
	/// </summary>
	[Fact]
	public void Validate_SigningKey_WhenTooShort_Fails()
	{
		// Arrange
		JwtOptions options = CreateValidOptions();
		options.SigningKey = "TooShort";
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("SigningKey", memberName);
		Assert.Equal(
			"Jwt:SigningKey must be at least 32 characters long. "
			+ "Use a long, random secret and do not commit it to source control.",
			error.ErrorMessage);
	}
}
