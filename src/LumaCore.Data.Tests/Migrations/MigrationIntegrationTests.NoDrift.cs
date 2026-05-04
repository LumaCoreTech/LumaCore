// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace LumaCore.Data.Tests.Migrations;

// NoDrift smoke test: verify that the live model and the most recent
// LumaCoreDbContextModelSnapshot agree on every detail visible to the migration differ.
//
//   1. Up — diff the rehydrated snapshot model against the live design-time model
//      and assert the operation list is empty.
//
// This is the same check that `dotnet ef migrations has-pending-model-changes` performs.
// Running it as a unit test catches any future drift the moment it appears — for example
// if an EF Core update changes how SqliteValueGenerationStrategy.Autoincrement is resolved,
// the SqliteAutoincrementForValueConvertedPrimaryKeysConvention silently stops working,
// or a developer modifies the model without scaffolding a new migration.
public sealed partial class MigrationIntegrationTests
{
	// --- 1. Up — live model matches the most recent migration snapshot ---

	/// <summary>
	/// Verifies that the EF Core differ reports zero changes between the most recent
	/// <c>LumaCoreDbContextModelSnapshot</c> and the live design-time model.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Reproduces the logic of <c>dotnet ef migrations has-pending-model-changes</c>: rehydrate the
	///     snapshot model, run it through the runtime initializer (so finalize-time conventions —
	///     including <c>SqliteAutoincrementForValueConvertedPrimaryKeysConvention</c> — are applied to
	///     the live side, and the snapshot side is brought to the same shape), then call
	///     <see cref="IMigrationsModelDiffer.GetDifferences"/> on the two
	///     <see cref="IRelationalModel"/> graphs.
	///     </para>
	///     <para>
	///     If a future EF Core release breaks the public APIs the convention relies on
	///     (<c>SqlitePropertyExtensions.SetValueGenerationStrategy</c>,
	///     <c>SqliteAnnotationProvider.For(IColumn)</c>'s strategy → annotation mapping, or the default-
	///     strategy resolver itself), this test fails with a non-empty diff naming the affected columns.
	///     </para>
	/// </remarks>
	[Fact]
	public async Task NoDrift_LiveModelMatchesLatestSnapshot()
	{
		// Arrange — spin up the harness only to obtain a configured DbContext; no schema is created.
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			IInfrastructure<IServiceProvider> infrastructure = harness.DbContext;
			IServiceProvider services = infrastructure.Instance;

			IMigrationsModelDiffer differ = services.GetRequiredService<IMigrationsModelDiffer>();
			IMigrationsAssembly migrationsAssembly = services.GetRequiredService<IMigrationsAssembly>();
			IDesignTimeModel designTimeModel = services.GetRequiredService<IDesignTimeModel>();
			IModelRuntimeInitializer runtimeInitializer = services.GetRequiredService<IModelRuntimeInitializer>();

			IModel? snapshotModel = migrationsAssembly.ModelSnapshot?.Model;
			Assert.NotNull(snapshotModel);

			// Mirror MigrationsScaffolder.HasDifferences: finalize the mutable snapshot model (if applicable)
			// and run it through the runtime initializer so its annotation graph matches what the live
			// design-time model produces.
			if (snapshotModel is IMutableModel mutableSnapshot)
			{
				snapshotModel = mutableSnapshot.FinalizeModel();
			}

			snapshotModel = runtimeInitializer.Initialize(snapshotModel);

			IRelationalModel sourceRelational = snapshotModel.GetRelationalModel();
			IRelationalModel targetRelational = designTimeModel.Model.GetRelationalModel();

			// Act
			IReadOnlyList<MigrationOperation> diff = differ.GetDifferences(sourceRelational, targetRelational);

			// Assert — empty diff means every annotation, column, index, and key matches. A non-empty
			// diff is rendered into the failure message so the regression is immediately diagnosable
			// (e.g. "AlterColumnOperation: Conversations.Id" → autoincrement annotation lost).
			Assert.True(
				diff.Count == 0,
				$"Expected no model drift, but the differ produced {diff.Count} operation(s): " +
				$"{string.Join(", ", diff.Select(op => op.GetType().Name))}. " +
				"Run `dotnet ef migrations add <Name>` to inspect the diff, then either accept the new " +
				"migration or fix the convention/model so the snapshot stays in sync.");
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
