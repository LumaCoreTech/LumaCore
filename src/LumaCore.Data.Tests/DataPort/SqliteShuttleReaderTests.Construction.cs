// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteShuttleReaderTests
{
	/// <summary>
	/// Verifies that the <see cref="SqliteShuttleReader"/> constructor succeeds with valid parameters.
	/// </summary>
	[Fact]
	public void Constructor_WhenParametersValid_CreatesInstance()
	{
		// Act
		var sut = new SqliteShuttleReader("/tmp/test.shuttle.sqlite", NullLogger.Instance);

		// Assert
		Assert.NotNull(sut);
	}

	/// <summary>
	/// Provides test data for <see cref="Constructor_WhenFilePathIsInvalid_ThrowsArgumentException"/>.
	/// Each row covers a different file-path validation branch: scenario name, the invalid file path,
	/// the expected exception type, and an optional expected message prefix for non-BCL exceptions.
	/// </summary>
	public static TheoryData<string, string?, Type, string?> InvalidFilePathData => new()
	{
		// null → ArgumentNullException from ArgumentNullException.ThrowIfNull()
		{
			"null",
			null,
			typeof(ArgumentNullException),
			null
		},

		// empty string → ArgumentException from ArgumentException.ThrowIfNullOrWhiteSpace()
		{
			"empty",
			"",
			typeof(ArgumentException),
			null
		},

		// whitespace-only → ArgumentException from ArgumentException.ThrowIfNullOrWhiteSpace()
		{
			"whitespace",
			"   ",
			typeof(ArgumentException),
			null
		},

		// NUL character → ArgumentException from FilePathValidator (universally invalid)
		{
			"NUL character",
			"test\0path",
			typeof(ArgumentException),
			"The file path has an invalid format for the current operating system."
		}
	};

	/// <summary>
	/// Verifies that the <see cref="SqliteShuttleReader"/> constructor throws the appropriate
	/// <see cref="ArgumentException"/> (or derived type) when <c>filePath</c> is invalid.
	/// Validation is delegated to <see cref="FilePathValidator"/>.
	/// </summary>
	/// <param name="scenario">Human-readable description of the test case.</param>
	/// <param name="filePath">The invalid file path to pass to the constructor.</param>
	/// <param name="expectedExceptionType">The expected exception type.</param>
	/// <param name="expectedMessage">
	/// Expected message prefix, or <see langword="null"/> for BCL-generated (localized) exceptions.
	/// </param>
	[Theory]
	[MemberData(nameof(InvalidFilePathData))]
	public void Constructor_WhenFilePathIsInvalid_ThrowsArgumentException(
		string  scenario,
		string? filePath,
		Type    expectedExceptionType,
		string? expectedMessage)
	{
		_ = scenario;

		// Act + Assert
		var ex = (ArgumentException)Assert.Throws(
			expectedExceptionType,
			() => new SqliteShuttleReader(filePath!, NullLogger.Instance));
		Assert.Equal("filePath", ex.ParamName);

		if (expectedMessage is not null)
		{
			Assert.StartsWith(expectedMessage, ex.Message);
		}
	}

	/// <summary>
	/// Verifies that the <see cref="SqliteShuttleReader"/> constructor throws
	/// <see cref="ArgumentNullException"/> when <c>logger</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new SqliteShuttleReader("/tmp/test.sqlite", null!));
		Assert.Equal("logger", ex.ParamName);
	}
}
