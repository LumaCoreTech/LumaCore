// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.IO;
using LumaCore.Data.DataPort;
using LumaCore.Data.DataPort.Export;
using LumaCore.Data.DataPort.Import;
using LumaCore.Data.DataPort.Shuttle;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

// Full roundtrip: Source DB (migrate + seed) → RunExportAsync → shuttle → RunImportAsync → Target DB → verify.
//
//   1. Export + Import: seed a source database, export to shuttle, import into a fresh target, verify.
public sealed partial class DataPortRoundtripTests
{
	// --- 1. Export + Import: full roundtrip ---

	/// <summary>
	/// Verifies that the full DataPort pipeline preserves all data: creates a source database with
	/// real EF Core entities, exports it via <see cref="DataPortService.RunExportAsync"/> to a
	/// shuttle file, imports the shuttle into a fresh target database via
	/// <see cref="DataPortService.RunImportAsync"/>, and asserts that all row counts and key entity
	/// data match the original seed.
	/// </summary>
	[Fact]
	public async Task Roundtrip_ExportAndImport_PreservesAllData()
	{
		// Arrange — source database: migrate + seed
		IntegrationTestHarness source = await CreateSourceHarnessAsync();
		try
		{
			SeedData seedData = await SeedTestDataAsync(source.DbContext);

			// Arrange — target database: migrate (empty, same schema)
			IntegrationTestHarness target = await CreateTargetHarnessAsync();
			try
			{
				// Arrange — shuttle file in a temp directory
				using var tempDir = new TemporaryFolder("dataport-roundtrip");
				string shuttlePath = Path.Combine(tempDir.Path, "roundtrip.shuttle.sqlite");

				var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
				var service = new DataPortService(NullLogger<DataPortService>.Instance);

				// Arrange — build export reader from the provider factory using the source connection string
				var exportOptions = new DatabaseOptions
				{
					ConnectionString = source.ConnectionString,
					Provider = source.ProviderOperations.ProviderName
				};
				IDataExportReader exportReader = source
					.ProviderOperations
					.CreateExportReader(exportOptions, NullLogger.Instance);

				// Act — Phase 1: Export
				var shuttleWriter = new SqliteShuttleWriter(shuttlePath, NullLogger.Instance, fakeTime);
				try
				{
					await service.RunExportAsync(exportReader, shuttleWriter);
				}
				finally
				{
					await shuttleWriter.DisposeAsync();
				}

				// Act — Phase 2: Import
				var shuttleReader = new SqliteShuttleReader(shuttlePath, NullLogger.Instance);
				IDataImportWriter importWriter = target
					.ProviderOperations
					.CreateImportWriter(target.ConnectionString, NullLogger.Instance, fakeTime);
				try
				{
					await service.RunImportAsync(shuttleReader, importWriter);
				}
				finally
				{
					await importWriter.DisposeAsync();
					await shuttleReader.DisposeAsync();
				}

				// Assert — verify row counts for every domain table
				await VerifyRowCountsAsync(source, target);

				// Assert — verify key entity data was preserved
				await VerifyEntityDataAsync(seedData, target);
			}
			finally
			{
				await target.DisposeAsync();
			}
		}
		finally
		{
			await source.DisposeAsync();
		}
	}
}
