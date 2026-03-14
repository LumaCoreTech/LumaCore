// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Export.Implementations;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.DataPort.Import.Implementations;
using LumaCore.Data.Providers;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class SqliteProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.CreateExportReader"/> returns a
	/// <see cref="SqliteExportReader"/> instance.
	/// </summary>
	[Fact]
	public void CreateExportReader_WhenCalled_ReturnsSqliteExportReader()
	{
		// Arrange
		var sut = new SqliteProviderOperations();
		var options = new DatabaseOptions { ConnectionString = "Data Source=:memory:" };

		// Act
		IDataExportReader result = sut.CreateExportReader(options, NullLogger.Instance);

		// Assert
		Assert.IsType<SqliteExportReader>(result);
	}

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.CreateImportWriter"/> returns a
	/// <see cref="SqliteImportWriter"/> instance.
	/// </summary>
	[Fact]
	public void CreateImportWriter_WhenCalled_ReturnsSqliteImportWriter()
	{
		// Arrange
		var sut = new SqliteProviderOperations();

		// Act
		IDataImportWriter result = sut.CreateImportWriter(
			"Data Source=:memory:",
			NullLogger.Instance,
			TimeProvider.System);

		// Assert
		Assert.IsType<SqliteImportWriter>(result);
	}
}
