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

public sealed partial class SqlServerProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="SqlServerProviderOperations.CreateExportReader"/> returns a
	/// <see cref="SqlServerExportReader"/> instance.
	/// </summary>
	[Fact]
	public void CreateExportReader_WhenCalled_ReturnsSqlServerExportReader()
	{
		// Arrange
		var sut = new SqlServerProviderOperations();
		var options = new DatabaseOptions { ConnectionString = "Server=localhost;Database=test" };

		// Act
		IDataExportReader result = sut.CreateExportReader(options, NullLogger.Instance);

		// Assert
		Assert.IsType<SqlServerExportReader>(result);
	}

	/// <summary>
	/// Verifies that <see cref="SqlServerProviderOperations.CreateImportWriter"/> returns a
	/// <see cref="SqlServerImportWriter"/> instance.
	/// </summary>
	[Fact]
	public void CreateImportWriter_WhenCalled_ReturnsSqlServerImportWriter()
	{
		// Arrange
		var sut = new SqlServerProviderOperations();

		// Act
		IDataImportWriter result = sut.CreateImportWriter(
			"Server=localhost;Database=test",
			NullLogger.Instance,
			TimeProvider.System);

		// Assert
		Assert.IsType<SqlServerImportWriter>(result);
	}
}
