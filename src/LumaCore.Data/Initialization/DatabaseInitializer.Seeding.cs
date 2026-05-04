// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;
using LumaCore.Data.Seeding;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LumaCore.Data.Initialization;

partial class DatabaseInitializer
{
	/// <summary>
	/// Seeds default data into the database if missing.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="scopeProvider">The scoped service provider for resolving seed dependencies.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// Uses the versioned seeding infrastructure to ensure idempotent data initialization.
	/// Seeds are only applied if they haven't been run before or if a newer version exists.
	/// </remarks>
	private async Task SeedDefaultDataAsync(
		LumaCoreDbContext dbContext,
		IServiceProvider  scopeProvider,
		CancellationToken cancellationToken)
	{
		ExecutionStageMonitor.ReportStage("SeedDefaultData.BeforeExecute");

		var seedExecutor = new SeedExecutor(
			scopeProvider.GetRequiredService<ILogger<SeedExecutor>>(),
			mTimeProvider);

		// Define all seeds to execute
		ISeedDefinition[] seeds =
		[
			new DefaultRolesSeed(
				scopeProvider.GetRequiredService<ILogger<DefaultRolesSeed>>(),
				mTimeProvider),

			new DefaultPersonaSeed(
				scopeProvider.GetRequiredService<ILogger<DefaultPersonaSeed>>(),
				mTimeProvider)
		];

		int executedCount = await seedExecutor
			                    .ExecuteSeedsAsync(dbContext, seeds, cancellationToken)
			                    .ConfigureAwait(false);

		if (executedCount > 0)
		{
			mLogger.LogInformation("Executed {ExecutedSeedCount} seed operation(s)", executedCount);
		}
		else
		{
			mLogger.LogDebug("All seeds are up to date");
		}
	}
}
