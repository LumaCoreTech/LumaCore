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

public sealed partial class PostgreSqlProviderOperationsTests
{
	/// <summary>
	/// Verifies that <see cref="PostgreSqlProviderOperations.CreateExportReader"/> returns a
	/// <see cref="PostgresExportReader"/> instance.
	/// </summary>
	[Fact]
	public void CreateExportReader_WhenCalled_ReturnsPostgresExportReader()
	{
		// Arrange
		var sut = new PostgreSqlProviderOperations();
		var options = new DatabaseOptions { ConnectionString = "Host=localhost;Database=test" };

		// Act
		IDataExportReader result = sut.CreateExportReader(options, NullLogger.Instance);

		// Assert
		Assert.IsType<PostgresExportReader>(result);
	}

	/// <summary>
	/// Verifies that <see cref="PostgreSqlProviderOperations.CreateImportWriter"/> returns a
	/// <see cref="PostgresImportWriter"/> instance.
	/// </summary>
	[Fact]
	public void CreateImportWriter_WhenCalled_ReturnsPostgresImportWriter()
	{
		// Arrange
		var sut = new PostgreSqlProviderOperations();

		// Act
		IDataImportWriter result = sut.CreateImportWriter(
			"Host=localhost;Database=test",
			NullLogger.Instance,
			TimeProvider.System);

		// Assert
		Assert.IsType<PostgresImportWriter>(result);
	}
}
