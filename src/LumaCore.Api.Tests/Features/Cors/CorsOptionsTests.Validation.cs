// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Api.Features.Cors;

using Xunit;

namespace LumaCore.Api.Tests.Features.Cors;

// Validation behavior: grouped by the property or concern being validated.
// Within each group, valid scenarios precede invalid ones.
//
//   1. General: disabled CORS skips validation (Enabled_WhenFalse),
//      fully configured options pass (WithFullyConfiguredOptions).
//   2. AllowedOrigins: wildcard accepted (WhenWildcardAndNoCredentials), empty origins rejected
//      (WhenEmptyAndEnabled), invalid format rejected (WhenContainsInvalidOrigin),
//      credentials with wildcard rejected (WhenWildcardWithCredentials).
//   3. AllowedMethods: known methods pass (WithKnownHttpMethod),
//      unknown verbs rejected (WhenContainsUnknownVerb).
//   4. PreflightMaxAge: boundary values pass (WithBoundaryValues),
//      negative values rejected (WhenNegative).
//
// For constructor defaults and SectionName, see the anchor file (CorsOptionsTests.cs).
// For the CreateValidOptions() factory, see Helpers.
public sealed partial class CorsOptionsTests
{
	// --- 1. General ---

	/// <summary>
	/// Verifies that validation succeeds when <see cref="CorsOptions.Enabled"/> is <see langword="false"/>,
	/// even with empty <see cref="CorsOptions.AllowedOrigins"/>.
	/// </summary>
	[Fact]
	public void Validate_Enabled_WhenFalse_Succeeds()
	{
		// Arrange
		var options = new CorsOptions { Enabled = false };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that a fully configured <see cref="CorsOptions"/> instance passes validation.
	/// </summary>
	[Fact]
	public void Validate_WithFullyConfiguredOptions_Succeeds()
	{
		// Arrange
		CorsOptions options = CreateValidOptions();
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	// --- 2. AllowedOrigins ---

	/// <summary>
	/// Verifies that validation succeeds when <see cref="CorsOptions.AllowedOrigins"/> contains only the wildcard
	/// <c>"*"</c> and <see cref="CorsOptions.AllowCredentials"/> is <see langword="false"/>.
	/// </summary>
	[Fact]
	public void Validate_AllowedOrigins_WhenWildcardAndNoCredentials_Succeeds()
	{
		// Arrange
		CorsOptions options = CreateValidOptions();
		options.AllowCredentials = false;
		options.AllowedOrigins = ["*"];
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="CorsOptions.Enabled"/> is <see langword="true"/> but
	/// <see cref="CorsOptions.AllowedOrigins"/> is empty.
	/// </summary>
	[Fact]
	public void Validate_AllowedOrigins_WhenEmptyAndEnabled_Fails()
	{
		// Arrange
		var options = new CorsOptions { Enabled = true };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("AllowedOrigins", memberName);
		Assert.Equal(
			"Cors:Enabled is set to true, but Cors:AllowedOrigins is empty. " +
			"You must specify at least one allowed origin, or set Enabled to false.",
			error.ErrorMessage);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="CorsOptions.AllowedOrigins"/> contains an entry that is not
	/// a valid CORS origin (absolute URI with http/https scheme and no path, query, or fragment).
	/// </summary>
	/// <param name="caseName">A descriptive name for the test case.</param>
	/// <param name="invalidOrigin">The invalid origin value to test.</param>
	[Theory]
	[InlineData("no scheme", "example.com")]
	[InlineData("typo in scheme", "htps://example.com")]
	[InlineData("non-HTTP scheme", "ftp://files.example.com")]
	[InlineData("has path", "https://example.com/api")]
	[InlineData("has query", "https://example.com?q=1")]
	[InlineData("has fragment", "https://example.com#section")]
	[InlineData("empty string", "")]
	public void Validate_AllowedOrigins_WhenContainsInvalidOrigin_Fails(string caseName, string invalidOrigin)
	{
		_ = caseName; // Used for test output readability.

		// Arrange
		CorsOptions options = CreateValidOptions();
		options.AllowedOrigins = [invalidOrigin];
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("AllowedOrigins", memberName);
		Assert.Equal(
			$"Cors:AllowedOrigins contains invalid origin '{invalidOrigin}'. " +
			"Each origin must use the format scheme://host[:port] with an http or https scheme " +
			"(e.g. 'https://example.com' or 'http://localhost:3000').",
			error.ErrorMessage);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="CorsOptions.AllowCredentials"/> is <see langword="true"/>
	/// and <see cref="CorsOptions.AllowedOrigins"/> contains the wildcard <c>"*"</c>.
	/// </summary>
	[Fact]
	public void Validate_AllowedOrigins_WhenWildcardWithCredentials_Fails()
	{
		// Arrange
		CorsOptions options = CreateValidOptions();
		options.AllowedOrigins = ["*"];
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		Assert.Equal(
			["AllowCredentials", "AllowedOrigins"],
			error.MemberNames.Order());
		Assert.Equal(
			"Cors:AllowCredentials cannot be true when Cors:AllowedOrigins contains '*' (wildcard). " +
			"Specify exact origins instead for security.",
			error.ErrorMessage);
	}

	// --- 3. AllowedMethods ---

	/// <summary>
	/// Verifies that validation succeeds for each known HTTP method in
	/// <see cref="CorsOptions.AllowedMethods"/>, including case variants to confirm case-insensitive matching.
	/// </summary>
	/// <param name="method">The HTTP method to test.</param>
	[Theory]
	[InlineData("DELETE")]
	[InlineData("GET")]
	[InlineData("HEAD")]
	[InlineData("OPTIONS")]
	[InlineData("PATCH")]
	[InlineData("POST")]
	[InlineData("PUT")]
	[InlineData("get")]    // Lowercase — verifies case-insensitive matching.
	[InlineData("Post")]   // Mixed case — verifies case-insensitive matching.
	public void Validate_AllowedMethods_WithKnownHttpMethod_Succeeds(string method)
	{
		// Arrange
		CorsOptions options = CreateValidOptions();
		options.AllowedMethods = [method];
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="CorsOptions.AllowedMethods"/> contains an unrecognized
	/// HTTP method.
	/// </summary>
	/// <param name="caseName">A descriptive name for the test case.</param>
	/// <param name="invalidMethod">The unrecognized HTTP method to test.</param>
	[Theory]
	[InlineData("typo", "DELET")]
	[InlineData("unsupported method", "TRACE")]
	public void Validate_AllowedMethods_WhenContainsUnknownVerb_Fails(string caseName, string invalidMethod)
	{
		_ = caseName; // Used for test output readability.

		// Arrange
		CorsOptions options = CreateValidOptions();
		options.AllowedMethods = [invalidMethod];
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("AllowedMethods", memberName);
		Assert.Equal(
			$"Cors:AllowedMethods contains unknown HTTP method '{invalidMethod}'. " +
			"Allowed values: DELETE, GET, HEAD, OPTIONS, PATCH, POST, PUT.",
			error.ErrorMessage);
	}

	// --- 4. PreflightMaxAge ---

	/// <summary>
	/// Verifies that <see cref="CorsOptions.PreflightMaxAge"/> passes validation at the boundary values of the
	/// allowed range (<c>0</c> and <see cref="int.MaxValue"/>).
	/// </summary>
	/// <param name="caseName">A descriptive name for the test case.</param>
	/// <param name="preflightMaxAge">The boundary value to test.</param>
	[Theory]
	[InlineData("lower boundary (0)", 0)]
	[InlineData("upper boundary (int.MaxValue)", int.MaxValue)]
	public void Validate_PreflightMaxAge_WithBoundaryValues_Succeeds(string caseName, int preflightMaxAge)
	{
		_ = caseName; // Used for test output readability.

		// Arrange
		CorsOptions options = CreateValidOptions();
		options.PreflightMaxAge = preflightMaxAge;
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.True(isValid);
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that validation fails when <see cref="CorsOptions.PreflightMaxAge"/> is negative.
	/// </summary>
	[Fact]
	public void Validate_PreflightMaxAge_WhenNegative_Fails()
	{
		// Arrange
		CorsOptions options = CreateValidOptions();
		options.PreflightMaxAge = -1;
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		Assert.False(isValid);
		ValidationResult error = Assert.Single(results);
		string memberName = Assert.Single(error.MemberNames);
		Assert.Equal("PreflightMaxAge", memberName);
		Assert.Equal(
			"PreflightMaxAge must be greater than or equal to 0 when specified.",
			error.ErrorMessage);
	}
}
