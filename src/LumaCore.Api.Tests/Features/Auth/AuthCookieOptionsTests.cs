// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Api.Features.Auth;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

/// <summary>
/// Unit tests for <see cref="AuthCookieOptions"/>.
/// </summary>
/// <remarks>
/// These tests verify the default values and data-annotation validation of <see cref="AuthCookieOptions"/>.
/// </remarks>
[Trait("Category", "Auth")]
public sealed class AuthCookieOptionsTests
{
	#region Constructor

	/// <summary>
	/// Verifies that a default-constructed <see cref="AuthCookieOptions"/> instance has the expected
	/// property defaults.
	/// </summary>
	[Fact]
	public void Constructor_Initially_HasExpectedDefaults()
	{
		// Arrange + Act
		var sut = new AuthCookieOptions();

		// Assert
		Assert.True(sut.Enabled);
		Assert.Equal("lumacore_access", sut.Name);
		Assert.True(sut.SecureOnly);
		Assert.Null(sut.Domain);
		Assert.Equal("/api", sut.Path);
	}

	#endregion

	#region SectionName

	/// <summary>
	/// Verifies that <see cref="AuthCookieOptions.SectionName"/> is <c>"Jwt:Cookie"</c>.
	/// </summary>
	[Fact]
	public void SectionName_Always_ReturnsExpectedValue()
	{
		// Act + Assert
		Assert.Equal("Jwt:Cookie", AuthCookieOptions.SectionName);
	}

	#endregion

	#region Validation

	/// <summary>
	/// Verifies that validation succeeds with default values.
	/// </summary>
	[Fact]
	public void Validate_WithDefaultValues_Succeeds()
	{
		// Arrange
		var options = new AuthCookieOptions();
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="AuthCookieOptions.Name"/> is empty.
	/// </summary>
	[Fact]
	public void Validate_WhenNameIsEmpty_Fails()
	{
		// Arrange
		var options = new AuthCookieOptions();
		options.Name = string.Empty;
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("Name", memberName);
		Assert.Equal(
			"Jwt:Cookie:Name must be configured. "
			+ "Set configuration key 'Jwt:Cookie:Name' or environment variable 'Jwt__Cookie__Name'.",
			error.ErrorMessage);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="AuthCookieOptions.Path"/> is empty.
	/// </summary>
	[Fact]
	public void Validate_WhenPathIsEmpty_Fails()
	{
		// Arrange
		var options = new AuthCookieOptions();
		options.Path = string.Empty;
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("Path", memberName);
		Assert.Equal(
			"Jwt:Cookie:Path must be configured. "
			+ "Set configuration key 'Jwt:Cookie:Path' or environment variable 'Jwt__Cookie__Path'.",
			error.ErrorMessage);
	}

	#endregion
}
