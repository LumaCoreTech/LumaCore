// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Initialization;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Data.Tests;

public sealed partial class ResourceCleanupServiceTests : IAsyncLifetime
{
	private readonly DbFixture mFixture = DbFixture.CreateSqliteInMemory();

	/// <summary>
	/// Initializes the database schema for the test instance.
	/// </summary>
	/// <returns>A task that represents the asynchronous initialization operation.</returns>
	public ValueTask InitializeAsync() => mFixture.InitializeAsync();

	/// <summary>
	/// Disposes the underlying database resources.
	/// </summary>
	/// <returns>A task that represents the asynchronous dispose operation.</returns>
	public ValueTask DisposeAsync() => mFixture.DisposeAsync();

	/// <summary>
	/// Inserts a single orphaned (zero-reference) <see cref="ResourceEntity"/> with a unique hash and
	/// the supplied <paramref name="createdAt"/>.
	/// </summary>
	/// <param name="createdAt">The UTC timestamp to assign to <see cref="ResourceEntity.CreatedAtUtc"/>.</param>
	/// <returns>The persisted resource entity.</returns>
	private async Task<ResourceEntity> SeedOrphanResourceAsync(DateTime createdAt)
	{
		var orphan = new ResourceEntity
		{
			ContentHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
			StoragePath = Guid.NewGuid().ToString(),
			SizeBytes = 1,
			CreatedAtUtc = createdAt,
			CreatedByParticipantId = null,
			DeletionState = ResourceDeletionState.Active
		};
		mFixture.DbContext.Resources.Add(orphan);
		await mFixture.DbContext.SaveChangesAsync();
		return orphan;
	}

	/// <summary>
	/// Builds a <see cref="ResourceCleanupOptions"/> instance with explicit values for the parameters
	/// each test cares about; remaining values use defaults from the production class.
	/// </summary>
	/// <param name="gracePeriodMinutes">The grace period in minutes.</param>
	/// <param name="intervalMinutes">The throttle interval in minutes.</param>
	/// <param name="enabled">Whether the service is enabled.</param>
	/// <returns>A configured options instance.</returns>
	private static ResourceCleanupOptions BuildOptions(
		int  gracePeriodMinutes,
		int  intervalMinutes = 60,
		bool enabled         = true) => new()
	{
		Enabled = enabled,
		GracePeriodMinutes = gracePeriodMinutes,
		IntervalMinutes = intervalMinutes,
		SweepBatchSize = 100
	};

	/// <summary>
	/// Creates a <see cref="ResourceCleanupService"/> wired with a minimal scope-aware service provider
	/// that hands out the fixture's <see cref="LumaCoreDbContext"/>, a ready
	/// <see cref="DatabaseInitializationStatus"/>, and the supplied store/time-provider.
	/// </summary>
	/// <param name="store">The fake resource store.</param>
	/// <param name="time">The fake time provider.</param>
	/// <param name="gracePeriodMinutes">The grace period in minutes.</param>
	/// <param name="intervalMinutes">The throttle interval in minutes.</param>
	/// <param name="enabled">Whether the service should be enabled.</param>
	/// <param name="configureContext">
	/// Optional hook invoked on every scoped <see cref="LumaCoreDbContext"/> the SUT resolves;
	/// used to attach interceptors or event handlers (e.g., <see cref="DbContext.SavingChanges"/>)
	/// for race-injection tests.
	/// </param>
	/// <param name="logger">
	/// Optional logger to wire into the SUT; defaults to <see cref="NullLogger{T}.Instance"/>. Tests
	/// that need to assert on log output (e.g., absence of a Warning) inject a capturing logger here.
	/// </param>
	/// <returns>A configured <see cref="ResourceCleanupService"/>.</returns>
	private ResourceCleanupService CreateSut(
		RecordingStore                   store,
		FakeTimeProvider                 time,
		int                              gracePeriodMinutes,
		int                              intervalMinutes  = 60,
		bool                             enabled          = true,
		Action<LumaCoreDbContext>?       configureContext = null,
		ILogger<ResourceCleanupService>? logger           = null)
	{
		// The cycle creates a new scope and resolves LumaCoreDbContext from it; reuse the fixture's
		// context so assertions and SUT see the same in-memory database.
		var services = new ServiceCollection();
		services.AddScoped(_ =>
		{
			LumaCoreDbContext ctx = mFixture.CreateDbContext();
			configureContext?.Invoke(ctx);
			return ctx;
		});
		ServiceProvider provider = services.BuildServiceProvider();

		var dbStatus = new DatabaseInitializationStatus();
		dbStatus.SetCompleted();

		IOptions<ResourceCleanupOptions> options =
			Options.Create(BuildOptions(gracePeriodMinutes, intervalMinutes, enabled));

		return new ResourceCleanupService(
			provider,
			store,
			dbStatus,
			options,
			time,
			logger ?? NullLogger<ResourceCleanupService>.Instance);
	}
}
