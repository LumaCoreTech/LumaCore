// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
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
//
// Provider scope: ModelSnapshot is provider-specific by design — provider annotations
// (e.g. SqlServer:Identity, Npgsql:ValueGenerationStrategy, Sqlite:Autoincrement) are baked
// into the snapshot at scaffold time. The same limitation applies to
// `dotnet ef migrations has-pending-model-changes`. Our snapshot is authored under SQL Server
// (via LumaCoreDbContextDesignTimeFactory); to keep the drift check meaningful and runnable
// everywhere (local dev on SQLite, CI without a SQL Server instance), the test builds its own
// DbContext through LumaCoreDbContextDesignTimeFactory rather than going through the shared
// IntegrationTestHarness. The factory's connection string is never opened — EF Core only needs
// the provider registration to materialize the model — so the test executes purely in-memory
// and requires no database engine of any kind.
//
// What this test does NOT cover:
//   * Provider-specific model branches (e.g. ConfigureUser's filtered unique index for
//     Users.Email, which currently produces different SQL on SQLite/PostgreSQL/SQL Server).
//     Drift introduced into a non-SQL-Server branch is invisible to this test because the
//     differ only ever sees the SQL Server materialization.
//   * Drift that would normally be caught at runtime by EF Core's PendingModelChangesWarning.
//     That warning is intentionally suppressed in both production (LumaCore.Data.ServiceRegistration)
//     and the test harness (IntegrationTestHarness) because the snapshot's SQL-Server-flavoured
//     annotations would otherwise produce false positives on every non-SQL-Server runtime — see
//     LumaCoreDbContextDesignTimeFactory's "Runtime drift detection" remarks for the rationale.
//     A consequence of this trade-off is that MigrationIntegrationTests against SQLite/PostgreSQL
//     also do NOT catch provider-specific drift through MigrateAsync(); they only validate that
//     the migrations execute against a real engine. Provider-specific model branches must be
//     reviewed manually when introduced.
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
	///     <para>
	///     The <see cref="LumaCoreDbContext"/> instance is built via
	///     <see cref="LumaCoreDbContextDesignTimeFactory"/>, which configures the SQL Server provider
	///     to match the snapshot's scaffold-time provider. The factory's connection string is never
	///     opened, so the test runs without any database engine — equally on local dev machines (where
	///     the integration harness defaults to SQLite) and on CI agents without a SQL Server instance.
	///     </para>
	/// </remarks>
	[Fact]
	public void NoDrift_LiveModelMatchesLatestSnapshot()
	{
		// Arrange — build a DbContext through the design-time factory so the live model uses the same
		// provider as the snapshot. No connection is opened; only the model graph is materialized.
		var factory = new LumaCoreDbContextDesignTimeFactory();
		using LumaCoreDbContext dbContext = factory.CreateDbContext([]);

		IInfrastructure<IServiceProvider> infrastructure = dbContext;
		IServiceProvider services = infrastructure.Instance;

		var differ = services.GetRequiredService<IMigrationsModelDiffer>();
		var migrationsAssembly = services.GetRequiredService<IMigrationsAssembly>();
		var designTimeModel = services.GetRequiredService<IDesignTimeModel>();
		var runtimeInitializer = services.GetRequiredService<IModelRuntimeInitializer>();

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
}
