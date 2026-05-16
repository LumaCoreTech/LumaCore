// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Core.Cryptography;

using Xunit;

namespace LumaCore.Core.Tests.Cryptography;

/// <summary>
/// Unit tests for <see cref="PasswordHashingOptions"/>.
/// </summary>
[Trait("Category", "Cryptography")]
public sealed class PasswordHashingOptionsTests
{
	#region Iterations

	/// <summary>
	/// Verifies that the default iteration count matches the documented baseline so consumers binding the
	/// options from an empty configuration section still receive a strong, OWASP-exceeding default.
	/// </summary>
	[Fact]
	public void Iterations_Default_EqualsDocumentedDefault()
	{
		// Arrange + Act
		var options = new PasswordHashingOptions();

		// Assert
		Assert.Equal(PasswordHashingOptions.DefaultIterations, options.Iterations);
		Assert.Equal(600_000, PasswordHashingOptions.DefaultIterations);
	}

	#endregion

	#region SectionName

	/// <summary>
	/// Verifies that the section name constant matches the documented configuration key so accidental
	/// renames are caught here rather than surfacing as silent binding failures at startup.
	/// </summary>
	[Fact]
	public void SectionName_HasExpectedValue()
	{
		// Act + Assert
		Assert.Equal("PasswordHashing", PasswordHashingOptions.SectionName);
	}

	#endregion

	#region Validate()

	/// <summary>
	/// Verifies that the default iteration count passes validation.
	/// </summary>
	[Fact]
	public void Validate_Iterations_WhenDefault_Succeeds()
	{
		// Arrange
		var options = new PasswordHashingOptions();
		var context = new ValidationContext(options);

		// Act
		List<ValidationResult> results = [.. options.Validate(context)];

		// Assert
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that the minimum permitted iteration count (100,000) passes validation — boundary case.
	/// </summary>
	[Fact]
	public void Validate_Iterations_WhenAtMinimum_Succeeds()
	{
		// Arrange
		var options = new PasswordHashingOptions { Iterations = 100_000 };
		var context = new ValidationContext(options);

		// Act
		List<ValidationResult> results = [.. options.Validate(context)];

		// Assert
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that the maximum permitted iteration count (10,000,000) passes validation — boundary case.
	/// </summary>
	[Fact]
	public void Validate_Iterations_WhenAtMaximum_Succeeds()
	{
		// Arrange
		var options = new PasswordHashingOptions { Iterations = 10_000_000 };
		var context = new ValidationContext(options);

		// Act
		List<ValidationResult> results = [.. options.Validate(context)];

		// Assert
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that an iteration count just below the configured minimum is rejected with a precise
	/// diagnostic that identifies the offending property.
	/// </summary>
	[Fact]
	public void Validate_Iterations_WhenBelowMinimum_Fails()
	{
		// Arrange
		var options = new PasswordHashingOptions { Iterations = 99_999 };
		var context = new ValidationContext(options);

		// Act
		List<ValidationResult> results = [.. options.Validate(context)];

		// Assert
		ValidationResult result = Assert.Single(results);
		Assert.Equal("Iterations must be at least 100000, but was 99999.", result.ErrorMessage);
		Assert.Equal("Iterations", Assert.Single(result.MemberNames));
	}

	/// <summary>
	/// Verifies that an iteration count above the configured maximum is rejected — guards against accidental
	/// misconfiguration turning login into a self-inflicted denial-of-service.
	/// </summary>
	[Fact]
	public void Validate_Iterations_WhenAboveMaximum_Fails()
	{
		// Arrange
		var options = new PasswordHashingOptions { Iterations = 10_000_001 };
		var context = new ValidationContext(options);

		// Act
		List<ValidationResult> results = [.. options.Validate(context)];

		// Assert
		ValidationResult result = Assert.Single(results);
		Assert.Equal("Iterations must not exceed 10000000, but was 10000001.", result.ErrorMessage);
		Assert.Equal("Iterations", Assert.Single(result.MemberNames));
	}

	#endregion

	#region ThrowIfInvalid()

	/// <summary>
	/// Verifies that <c>ThrowIfInvalid</c> is a no-op for a valid options instance.
	/// </summary>
	[Fact]
	public void ThrowIfInvalid_WhenValid_DoesNotThrow()
	{
		// Arrange
		var options = new PasswordHashingOptions();

		// Act + Assert
		options.ThrowIfInvalid();
	}

	/// <summary>
	/// Verifies that <c>ThrowIfInvalid</c> raises a <see cref="ValidationException"/> for invalid options.
	/// </summary>
	[Fact]
	public void ThrowIfInvalid_WhenInvalid_ThrowsValidationException()
	{
		// Arrange
		var options = new PasswordHashingOptions { Iterations = 1 };

		// Act + Assert
		Assert.Throws<ValidationException>(() => options.ThrowIfInvalid());
	}

	#endregion
}
