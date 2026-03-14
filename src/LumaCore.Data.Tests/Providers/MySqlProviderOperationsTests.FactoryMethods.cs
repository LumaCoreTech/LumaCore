// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Providers;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class MySqlProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="MySqlProviderOperations.CreateExportReader"/> throws
	/// <see cref="NotSupportedException"/> because MySQL DataPort is temporarily unavailable
	/// (Pomelo EF Core 10 not released).
	/// </summary>
	[Fact]
	public void CreateExportReader_WhenCalled_ThrowsNotSupportedException()
	{
		// Arrange
		var sut = new MySqlProviderOperations();
		var options = new DatabaseOptions { ConnectionString = "Server=localhost;Database=test" };

		// Act + Assert
		var ex = Assert.Throws<NotSupportedException>(() => sut.CreateExportReader(options, NullLogger.Instance));
		Assert.Equal(
			"MySQL DataPort (data export/import) is not yet available. Pomelo.EntityFrameworkCore.MySql has " +
			"not released an EF Core 10 compatible version. Track progress at: " +
			"https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues",
			ex.Message);
	}

	/// <summary>
	/// Verifies that <see cref="MySqlProviderOperations.CreateImportWriter"/> throws
	/// <see cref="NotSupportedException"/> because MySQL DataPort is temporarily unavailable
	/// (Pomelo EF Core 10 not released).
	/// </summary>
	[Fact]
	public void CreateImportWriter_WhenCalled_ThrowsNotSupportedException()
	{
		// Arrange
		var sut = new MySqlProviderOperations();

		// Act + Assert
		var ex = Assert.Throws<NotSupportedException>(() => sut.CreateImportWriter(
			"Server=localhost;Database=test",
			NullLogger.Instance,
			TimeProvider.System));
		Assert.Equal(
			"MySQL DataPort (data export/import) is not yet available. Pomelo.EntityFrameworkCore.MySql has " +
			"not released an EF Core 10 compatible version. Track progress at: " +
			"https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues",
			ex.Message);
	}
}
