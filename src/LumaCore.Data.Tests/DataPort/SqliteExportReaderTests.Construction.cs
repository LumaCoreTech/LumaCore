// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Export.Implementations;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteExportReaderTests
{
	#region Constructor

	/// <summary>
	/// Verifies that the <see cref="SqliteExportReader"/> constructor succeeds with a valid connection string.
	/// </summary>
	[Fact]
	public void Constructor_WhenConnectionStringIsValid_CreatesInstance()
	{
		// Act
		var sut = new SqliteExportReader("Data Source=:memory:");

		// Assert
		Assert.NotNull(sut);
	}

	/// <summary>
	/// Verifies that the <see cref="SqliteExportReader"/> constructor throws
	/// <see cref="ArgumentNullException"/> when the connection string is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenConnectionStringIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new SqliteExportReader(null!));
		Assert.Equal("connectionString", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the <see cref="SqliteExportReader"/> constructor throws
	/// <see cref="ArgumentException"/> when the connection string is empty.
	/// </summary>
	[Fact]
	public void Constructor_WhenConnectionStringIsEmpty_ThrowsArgumentException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => new SqliteExportReader(""));
		Assert.Equal("connectionString", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the <see cref="SqliteExportReader"/> constructor throws
	/// <see cref="ArgumentException"/> when the connection string is whitespace.
	/// </summary>
	[Fact]
	public void Constructor_WhenConnectionStringIsWhitespace_ThrowsArgumentException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => new SqliteExportReader("   "));
		Assert.Equal("connectionString", ex.ParamName);
	}

	#endregion
}
