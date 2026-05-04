// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using Xunit;

namespace LumaCore.Data.Tests;

// ResourceStoreOptions.Validate covers the path-syntax sanity check for StorageRootPath:
//
//   1. StorageRootPath: whitespace short-circuits to no errors (the [Required] attribute is the
//      gating mechanism here; Validate() must not double-report).
//   2. StorageRootPath: a syntactically valid path (relative or absolute) yields no errors.
//   3. StorageRootPath: an OS-rejected path (NUL byte under .NET on every platform) is reported
//      with a descriptive ValidationResult that includes the offending value and the parser's
//      explanation.

/// <summary>
/// Tests for <see cref="ResourceStoreOptions"/>: verifies the <see cref="IValidatableObject.Validate"/>
/// path-syntax check that complements the <see cref="RequiredAttribute"/> on
/// <see cref="ResourceStoreOptions.StorageRootPath"/>.
/// </summary>
[Trait("Category", "Options")]
public sealed class ResourceStoreOptionsTests
{
	#region Validate

	/// <summary>
	/// Verifies that <see cref="ResourceStoreOptions.Validate"/> short-circuits without yielding
	/// errors when <see cref="ResourceStoreOptions.StorageRootPath"/> is whitespace — the
	/// <see cref="RequiredAttribute"/> already gates this case, so Validate must not double-report.
	/// </summary>
	[Fact]
	public void Validate_StorageRootPath_WhenWhitespace_YieldsNoErrors()
	{
		// Arrange
		var sut = new ResourceStoreOptions { StorageRootPath = "   " };

		// Act
		List<ValidationResult> results = sut.Validate(new ValidationContext(sut)).ToList();

		// Assert
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceStoreOptions.Validate"/> accepts a syntactically valid
	/// relative path without yielding errors.
	/// </summary>
	[Fact]
	public void Validate_StorageRootPath_WhenValidRelativePath_YieldsNoErrors()
	{
		// Arrange
		var sut = new ResourceStoreOptions { StorageRootPath = "./resources" };

		// Act
		List<ValidationResult> results = sut.Validate(new ValidationContext(sut)).ToList();

		// Assert
		Assert.Empty(results);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceStoreOptions.Validate"/> reports a descriptive
	/// <see cref="ValidationResult"/> when <see cref="ResourceStoreOptions.StorageRootPath"/>
	/// contains a NUL byte — the one path-syntax error reliably rejected by
	/// <see cref="Path.GetFullPath(string)"/> on every supported OS.
	/// </summary>
	[Fact]
	public void Validate_StorageRootPath_WhenContainsNullChar_YieldsDescriptiveValidationResult()
	{
		// Arrange — embedded NUL is rejected by Path.GetFullPath cross-platform.
		string invalidPath = "./resources\0bad";
		var sut = new ResourceStoreOptions { StorageRootPath = invalidPath };

		// Act
		List<ValidationResult> results = sut.Validate(new ValidationContext(sut)).ToList();

		// Assert: exactly one result, mentioning the offending path and the StorageRootPath member.
		ValidationResult result = Assert.Single(results);
		Assert.StartsWith(
			$"ResourceStorage:StorageRootPath contains an invalid path: '{invalidPath}'. ",
			result.ErrorMessage);
		Assert.Equal(nameof(ResourceStoreOptions.StorageRootPath), Assert.Single(result.MemberNames));
	}

	#endregion
}
