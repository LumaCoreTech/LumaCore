// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Import.Implementations;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

public sealed partial class SqliteImportWriterTests
{
	#region Constructor

	/// <summary>
	/// Verifies that the constructor succeeds with a valid connection string.
	/// </summary>
	[Fact]
	public void Constructor_WhenConnectionStringIsValid_CreatesInstance()
	{
		// Act
		var writer = new SqliteImportWriter("Data Source=test.db", TimeProvider.System);

		// Assert
		Assert.NotNull(writer);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when the connection string is
	/// <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenConnectionStringIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new SqliteImportWriter(null!, TimeProvider.System));
		Assert.Equal("connectionString", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentException"/> when the connection string is empty.
	/// </summary>
	[Fact]
	public void Constructor_WhenConnectionStringIsEmpty_ThrowsArgumentException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => new SqliteImportWriter("", TimeProvider.System));
		Assert.Equal("connectionString", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentException"/> when the connection string is whitespace.
	/// </summary>
	[Fact]
	public void Constructor_WhenConnectionStringIsWhitespace_ThrowsArgumentException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => new SqliteImportWriter("   ", TimeProvider.System));
		Assert.Equal("connectionString", ex.ParamName);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when the time provider is
	/// <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new SqliteImportWriter("Data Source=test.db", null!));
		Assert.Equal("timeProvider", ex.ParamName);
	}

	#endregion
}
