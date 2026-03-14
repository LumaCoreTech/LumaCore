// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

/// <summary>
/// Unit tests for <see cref="DatabaseInitializationException"/>.
/// </summary>
[Trait("Category", "Initialization")]
public sealed class DatabaseInitializationExceptionTests
{
	/// <summary>
	/// Verifies that the constructor stores <see cref="Exception.Message"/>,
	/// <see cref="DatabaseInitializationException.Category"/>, and <see cref="Exception.InnerException"/>
	/// correctly when all parameters are provided.
	/// </summary>
	[Fact]
	public void Constructor_WhenAllParametersProvided_SetsAllProperties()
	{
		// Arrange
		const string message = "Migration failed";
		const DatabaseFailureCategory category = DatabaseFailureCategory.Transient;
		var innerException = new InvalidOperationException("connection timeout");

		// Act
		var sut = new DatabaseInitializationException(message, category, innerException);

		// Assert
		Assert.Equal(message, sut.Message);
		Assert.Equal(category, sut.Category);
		Assert.Same(innerException, sut.InnerException);
	}

	/// <summary>
	/// Verifies that <see cref="Exception.InnerException"/> is <see langword="null"/> when the optional
	/// <c>innerException</c> parameter is omitted.
	/// </summary>
	[Fact]
	public void Constructor_WhenInnerExceptionOmitted_InnerExceptionIsNull()
	{
		// Arrange + Act
		var sut = new DatabaseInitializationException(
			"Configuration error",
			DatabaseFailureCategory.ConfigurationRequired);

		// Assert
		Assert.Equal("Configuration error", sut.Message);
		Assert.Equal(DatabaseFailureCategory.ConfigurationRequired, sut.Category);
		Assert.Null(sut.InnerException);
	}
}
