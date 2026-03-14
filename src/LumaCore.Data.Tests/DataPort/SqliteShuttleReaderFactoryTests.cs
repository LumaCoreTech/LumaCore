// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;
using LumaCore.Data.DataPort.Shuttle;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Unit tests for <see cref="SqliteShuttleReaderFactory"/>.
/// </summary>
[Trait("Category", "DataPort")]
public sealed class SqliteShuttleReaderFactoryTests
{
	#region Constructor

	/// <summary>
	/// Verifies that the <see cref="SqliteShuttleReaderFactory"/> constructor succeeds with a valid logger.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerIsValid_CreatesInstance()
	{
		// Act
		var sut = new SqliteShuttleReaderFactory(NullLogger<SqliteShuttleReader>.Instance);

		// Assert
		Assert.NotNull(sut);
	}

	/// <summary>
	/// Verifies that the <see cref="SqliteShuttleReaderFactory"/> constructor throws
	/// <see cref="ArgumentNullException"/> when <c>logger</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new SqliteShuttleReaderFactory(null!));
		Assert.Equal("logger", ex.ParamName);
	}

	#endregion

	#region Create()

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReaderFactory.Create"/> returns a new
	/// <see cref="SqliteShuttleReader"/> instance for the specified file path.
	/// </summary>
	[Fact]
	public void Create_WhenFilePathIsValid_ReturnsSqliteShuttleReader()
	{
		// Arrange
		var sut = new SqliteShuttleReaderFactory(NullLogger<SqliteShuttleReader>.Instance);

		// Act
		IShuttleReader reader = sut.Create("/tmp/test.shuttle.sqlite");

		// Assert
		Assert.IsType<SqliteShuttleReader>(reader);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReaderFactory.Create"/> returns a distinct instance on
	/// each call, confirming that the factory does not cache or reuse readers.
	/// </summary>
	[Fact]
	public void Create_WhenCalledMultipleTimes_ReturnsDistinctInstances()
	{
		// Arrange
		var sut = new SqliteShuttleReaderFactory(NullLogger<SqliteShuttleReader>.Instance);

		// Act
		IShuttleReader reader1 = sut.Create("/tmp/a.shuttle.sqlite");
		IShuttleReader reader2 = sut.Create("/tmp/b.shuttle.sqlite");

		// Assert
		Assert.NotSame(reader1, reader2);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReaderFactory.Create"/> throws
	/// <see cref="ArgumentNullException"/> when <c>filePath</c> is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Create_WhenFilePathIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var sut = new SqliteShuttleReaderFactory(NullLogger<SqliteShuttleReader>.Instance);

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => sut.Create(null!));
		Assert.Equal("filePath", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReaderFactory.Create"/> throws
	/// <see cref="ArgumentException"/> when <c>filePath</c> is empty or consists only of white-space characters.
	/// </summary>
	/// <param name="scenario">A human-readable description of the test case.</param>
	/// <param name="filePath">The invalid file path to test.</param>
	[Theory]
	[InlineData("empty string", "")]
	[InlineData("single space", " ")]
	[InlineData("tab character", "\t")]
	public void Create_WhenFilePathIsEmptyOrWhiteSpace_ThrowsArgumentException(string scenario, string filePath)
	{
		_ = scenario;

		// Arrange
		var sut = new SqliteShuttleReaderFactory(NullLogger<SqliteShuttleReader>.Instance);

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => sut.Create(filePath));
		Assert.Equal("filePath", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteShuttleReaderFactory.Create"/> delegates file path validation to
	/// <see cref="FilePathValidator"/> and surfaces the resulting <see cref="ArgumentException"/>.
	/// </summary>
	[Fact]
	public void Create_WhenFilePathHasInvalidFormat_ThrowsArgumentException()
	{
		// Arrange
		var sut = new SqliteShuttleReaderFactory(NullLogger<SqliteShuttleReader>.Instance);

		// Act + Assert — NUL character is universally invalid across all operating systems.
		var ex = Assert.Throws<ArgumentException>(() => sut.Create("test\0path"));
		Assert.Equal("filePath", ex.ParamName);
		Assert.StartsWith(
			"The file path has an invalid format for the current operating system.",
			ex.Message);
	}

	#endregion
}
