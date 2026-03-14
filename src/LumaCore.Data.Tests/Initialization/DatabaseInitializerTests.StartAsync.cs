// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Core.Diagnostics;
using LumaCore.Data.Entities;
using LumaCore.Data.Initialization;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

// Database initializer lifecycle: first boot through repeated failures.
//
// These tests follow the initializer from its first successful startup to the edge cases
// that arise when things go wrong — and how it recovers (or doesn't):
//
//   1. Happy path: fresh database, migration, seeding → Completed (CompletesSuccessfully).
//      Calling StartAsync twice on an already-migrated DB still completes (CalledTwice).
//
//   2. Configuration gates: AutoCreate disabled on a new DB → ConfigurationRequired.
//      No retry — the operator must change the configuration.
//
//   3. Error classification: uncategorized exceptions → Transient (auto-retry).
//      OperationCanceledException propagates cleanly without status transition.
//
//   4. Optional features: cleanup toggle and compiled-query flag are exercised without
//      affecting the outcome — they just need to not break anything.
//
//   5. Runtime integrity: orphaned conversations are cleaned up on second startup.
//      Default roles are seeded; a seeding failure is Transient (self-healing on next boot).
//
//   6. Retry mechanics: consecutive failures increment a counter. After
//      MaxConsecutiveFailures the category escalates to ManualInterventionRequired.
//      A single success resets the counter to zero.
//
// For migration-specific scenarios (backup, restore, failure recovery) on existing databases,
// see HandleUpdateMigrations. For the 6-phase restore pipeline in isolation, see ResumeRestore.
// The anchor file (DatabaseInitializerTests.cs) has the full reading order.
public sealed partial class DatabaseInitializerTests
{
	#region StartAsync()

	// --- 1. Happy path: first boot and idempotency ---

	/// <summary>
	/// Verifies the happy-path initialization: empty database with <see cref="DatabaseOptions.AutoCreate"/> enabled
	/// applies the initial migration, seeds default roles, and transitions the status to
	/// <see cref="DatabaseInitializationState.Completed"/>.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenNewDatabaseWithAutoCreate_CompletesSuccessfully()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			await AssertCompletedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that calling <see cref="DatabaseInitializer.StartAsync"/> a second time on an already-migrated
	/// database still completes successfully (idempotent — no pending migrations, seeds already applied).
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenCalledTwiceOnSameDatabase_CompletesSuccessfully()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			// Act — second invocation on already-migrated DB
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			await AssertCompletedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Configuration gates: what prevents initialization ---

	/// <summary>
	/// Verifies that a new (empty) database with <see cref="DatabaseOptions.AutoCreate"/> disabled causes a
	/// <see cref="DatabaseInitializationException"/> with <see cref="DatabaseFailureCategory.ConfigurationRequired"/>,
	/// and the status transitions to <see cref="DatabaseInitializationState.Failed"/>.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenAutoCreateDisabledAndNewDatabase_FailsWithConfigurationRequired()
	{
		// Arrange
		TestHarness harness = CreateHarness(options => options.AutoCreate = false);
		try
		{
			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — StartAsync does NOT throw; it catches the exception and sets status to Failed.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ConfigurationRequired,
				1,
				expectedShouldRetry: false);
			var ex = Assert.IsType<DatabaseInitializationException>(harness.Status.FailureException);
			Assert.Equal(
				"Database is empty and AutoCreate is disabled. " +
				"Run 'dotnet ef database update' manually or set Database:AutoCreate=true.",
				ex.Message);
			Assert.Equal(ex.Message, harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Error classification: how exceptions become failure categories ---

	/// <summary>
	/// Verifies that an uncategorized exception (not <see cref="DatabaseInitializationException"/> and not
	/// <see cref="OperationCanceledException"/>) is treated as <see cref="DatabaseFailureCategory.Transient"/>
	/// and the status transitions to <see cref="DatabaseInitializationState.Failed"/>.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenUncategorizedException_FailsWithTransient()
	{
		// Arrange — use an invalid connection string to force a generic exception during migration.
		TestHarness harness = CreateHarness(options =>
			options.ConnectionString = "Data Source=/nonexistent/path/that/cannot/be/created/test.db");
		try
		{
			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			AssertFailedStatusCore(harness.Status, DatabaseFailureCategory.Transient, 1, expectedShouldRetry: true);
			Assert.NotNull(harness.Status.FailureException);
			Assert.Equal(
				"Database initialization failed. See logs for details.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="OperationCanceledException"/> propagates out of
	/// <see cref="DatabaseInitializer.StartAsync"/> without being caught — the caller (hosting infrastructure)
	/// handles cancellation. The status remains <see cref="DatabaseInitializationState.InProgress"/> because
	/// <see cref="DatabaseInitializationStatus.SetFailed"/> is never called.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenCancelled_PropagatesOperationCanceledException()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			using var cts = new CancellationTokenSource();
			await cts.CancelAsync();

			// Act + Assert
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Sut.StartAsync(cts.Token));

			// Status was set to InProgress but never completed or failed.
			AssertInProgressStatus(harness.Status);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 4. Optional features: must not break the pipeline ---

	/// <summary>
	/// Verifies that initialization with <see cref="DatabaseOptions.CleanupConversationsWithNoUsersOnStartup"/>
	/// disabled skips the cleanup step but still completes migration and seeding successfully.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenCleanupDisabled_CompletesWithoutCleanup()
	{
		// Arrange
		TestHarness harness = CreateHarness(options =>
			options.CleanupConversationsWithNoUsersOnStartup = false);
		try
		{
			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — initialization completes; cleanup was skipped (no observable side effect
			// in a fresh DB, but the code path is exercised).
			await AssertCompletedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that the <c>PreferCompiledHotPathQueries</c> informational log branch is exercised when the
	/// option is enabled. The initialization still completes successfully — the option only affects query
	/// compilation strategy at runtime.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenPreferCompiledHotPathQueriesEnabled_CompletesSuccessfully()
	{
		// Arrange
		TestHarness harness = CreateHarness(options =>
			options.PreferCompiledHotPathQueries = true);
		try
		{
			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			await AssertCompletedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 5. Runtime integrity: cleanup, seeding, and seeding failures ---

	/// <summary>
	/// Verifies that the integrity cleanup removes orphaned conversations (conversations without
	/// participants) and that the <c>deleted &gt; 0</c> warning-log branch in
	/// <see cref="DatabaseInitializer.RunInitializationCoreAsync"/> is exercised.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenOrphanedConversationsExist_CleansUpAndCompletes()
	{
		// Arrange — initialize DB first, then insert an orphaned conversation.
		TestHarness harness = CreateHarness();
		try
		{
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(DatabaseInitializationState.Completed, harness.Status.State);

			// Insert a conversation with no participants (orphaned).
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				DateTime now = harness.TimeProvider.GetUtcNow().UtcDateTime;
				dbContext.Conversations.Add(
					new ConversationEntity
					{
						PublicId = Guid.NewGuid(),
						Title = "Orphaned conversation",
						CreatedAtUtc = now,
						UpdatedAtUtc = now
					});
				await dbContext.SaveChangesAsync();
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Act — second StartAsync triggers cleanup that finds the orphan.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — still completes, orphan was cleaned up.
			await AssertCompletedAsync(harness);

			// Verify the orphaned conversation was actually deleted.
			(AsyncServiceScope scope2, LumaCoreDbContext dbContext2) = harness.CreateScopedDbContext();
			try
			{
				int remaining = await dbContext2
					                .Conversations
					                .CountAsync(c => c.Title == "Orphaned conversation");
				Assert.Equal(0, remaining);
			}
			finally
			{
				await scope2.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that initialization seeds all default roles defined in <see cref="RoleDefinitions.Defaults"/>
	/// into the database. Each role must be present by name.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenNewDatabaseWithAutoCreate_SeedsDefaultRoles()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			await AssertCompletedAsync(harness);

			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				List<string> roleNames = await dbContext.Roles
					                         .Select(r => r.Name)
					                         .ToListAsync();

				// Verify that exactly the default role definitions are present (no more, no fewer).
				string[] expectedNames = RoleDefinitions.Defaults.Select(r => r.Name).Order().ToArray();
				Assert.Equal(expectedNames, roleNames.Order());
			}
			finally
			{
				await scope.DisposeAsync();
			}
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that an exception thrown during seeding (after migrations succeed) is caught by
	/// <see cref="DatabaseInitializer.StartAsync"/> and treated as
	/// <see cref="DatabaseFailureCategory.Transient"/>. Uses <see cref="ExecutionStageMonitor"/> to inject
	/// a fault at the <c>SeedDefaultData.BeforeExecute</c> stage.
	/// </summary>
	/// <remarks>
	/// Migrations complete successfully before the fault fires, so the database schema is valid.
	/// On the next startup attempt, seeding would run normally (self-healing via <c>Transient</c> retry).
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenSeedingFails_FailsWithTransient()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			// Act — inject a fault to make seeding throw after migrations succeed.
			using (ExecutionStageMonitor.Configure()
				       .ThrowAt(
					       "SeedDefaultData.BeforeExecute",
					       new InvalidOperationException("Simulated seeding failure")))
			{
				await harness.Sut.StartAsync(CancellationToken.None);
			}

			// Assert — the exception was caught and treated as Transient.
			AssertFailedStatusCore(harness.Status, DatabaseFailureCategory.Transient, 1, expectedShouldRetry: true);
			var ex = Assert.IsType<InvalidOperationException>(harness.Status.FailureException);
			Assert.Equal("Simulated seeding failure", ex.Message);
			Assert.Equal(
				"Database initialization failed. See logs for details.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that a migration failure on a new (empty) database with
	/// <see cref="DatabaseOptions.AutoCreate"/> enabled is caught by
	/// <see cref="DatabaseInitializer.StartAsync"/> and treated as
	/// <see cref="DatabaseFailureCategory.Transient"/>. This exercises the
	/// <c>HandleInitialCreationAsync</c> error path which has no try/catch — exceptions propagate
	/// to the generic handler in <c>StartAsync</c>.
	/// </summary>
	/// <remarks>
	/// The test uses <see cref="DatabaseFacade.EnsureCreatedAsync"/> to pre-create the schema without
	/// migration history, so <c>MigrateAsync</c> encounters conflicting tables when attempting the
	/// initial creation.
	/// </remarks>
	[Fact]
	public async Task StartAsync_WhenMigrationFailsOnNewDatabase_FailsWithTransient()
	{
		// Arrange — pre-create schema without migration history so MigrateAsync conflicts.
		TestHarness harness = CreateHarness();
		try
		{
			(AsyncServiceScope scope, LumaCoreDbContext dbContext) = harness.CreateScopedDbContext();
			try
			{
				// EnsureCreatedAsync creates all model tables but NOT __EFMigrationsHistory.
				// StartAsync will see 0 applied migrations (isNewDatabase = true) and attempt
				// HandleInitialCreationAsync → MigrateAsync → table conflict.
				await dbContext.Database.EnsureCreatedAsync();
			}
			finally
			{
				await scope.DisposeAsync();
			}

			// Act
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			AssertFailedStatusCore(harness.Status, DatabaseFailureCategory.Transient, 1, expectedShouldRetry: true);
			Assert.IsAssignableFrom<DbException>(harness.Status.FailureException);
			Assert.Equal(
				"Database initialization failed. See logs for details.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 6. Retry mechanics: counter, escalation, and reset ---

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.ConsecutiveFailureCount"/> increments on each
	/// consecutive transient failure. Two successive <see cref="DatabaseInitializer.StartAsync"/> calls with an
	/// invalid connection string must result in <c>ConsecutiveFailureCount == 2</c>.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenTransientFailureRepeated_IncrementsConsecutiveFailureCount()
	{
		// Arrange — invalid connection string forces a generic (Transient) exception on every attempt.
		TestHarness harness = CreateHarness(options =>
			options.ConnectionString = "Data Source=/nonexistent/path/that/cannot/be/created/test.db");
		try
		{
			// Act — first failure
			await harness.Sut.StartAsync(CancellationToken.None);
			Assert.Equal(1, harness.Status.ConsecutiveFailureCount);
			Assert.True(harness.Status.ShouldRetry);

			// Act — second failure
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert
			AssertFailedStatusCore(harness.Status, DatabaseFailureCategory.Transient, 2, expectedShouldRetry: true);
			Assert.NotNull(harness.Status.FailureException);
			Assert.Equal(
				"Database initialization failed. See logs for details.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that after <see cref="DatabaseInitializationStatus.MaxConsecutiveFailures"/> consecutive
	/// transient failures, the failure category is automatically escalated from
	/// <see cref="DatabaseFailureCategory.Transient"/> to
	/// <see cref="DatabaseFailureCategory.ManualInterventionRequired"/> and
	/// <see cref="DatabaseInitializationStatus.ShouldRetry"/> becomes <see langword="false"/>.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenTransientFailureExceedsMax_EscalatesToManualIntervention()
	{
		// Arrange
		TestHarness harness = CreateHarness(options =>
			options.ConnectionString = "Data Source=/nonexistent/path/that/cannot/be/created/test.db");
		try
		{
			// Act — fail MaxConsecutiveFailures times.
			for (int i = 0; i < DatabaseInitializationStatus.MaxConsecutiveFailures; i++)
			{
				await harness.Sut.StartAsync(CancellationToken.None);
			}

			// Assert — escalated to ManualInterventionRequired, no more retries.
			AssertFailedStatusCore(
				harness.Status,
				DatabaseFailureCategory.ManualInterventionRequired,
				DatabaseInitializationStatus.MaxConsecutiveFailures,
				expectedShouldRetry: false);
			Assert.NotNull(harness.Status.FailureException);
			Assert.Matches(
				@"Failed \d+ times consecutively\. Automatic recovery has been disabled\.",
				harness.Status.FailureMessage);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializationStatus.ConsecutiveFailureCount"/> resets to 0 after
	/// a successful <see cref="DatabaseInitializer.StartAsync"/> that follows a failure. Uses
	/// <see cref="ExecutionStageMonitor"/> to inject a fault on the first attempt and lets the second
	/// attempt succeed normally.
	/// </summary>
	[Fact]
	public async Task StartAsync_WhenSuccessAfterFailure_ResetsConsecutiveFailureCount()
	{
		// Arrange — apply M1 first so the DB is "existing" and HandleUpdateMigrationsAsync runs
		// (isNewDatabase = false). The ThrowAt stage fires inside HandleUpdateMigrationsAsync().
		TestHarness harness = CreateHarness();
		try
		{
			await harness.MigrateToFirstMigrationOnlyAsync();

			// First attempt — inject a fault to force a failure.
			using (ExecutionStageMonitor.Configure()
				       .ThrowAt(
					       "HandleUpdateMigrations.BeforeMigrate",
					       new InvalidOperationException("Simulated migration failure")))
			{
				await harness.Sut.StartAsync(CancellationToken.None);
			}

			Assert.Equal(DatabaseInitializationState.Failed, harness.Status.State);
			Assert.Equal(1, harness.Status.ConsecutiveFailureCount);

			// Act — second attempt without fault injection → succeeds.
			await harness.Sut.StartAsync(CancellationToken.None);

			// Assert — counter reset, status completed.
			await AssertCompletedAsync(harness);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion

	#region StopAsync()

	/// <summary>
	/// Verifies that <see cref="DatabaseInitializer.StopAsync"/> completes immediately without performing
	/// any work — there is no cleanup required on shutdown.
	/// </summary>
	[Fact]
	public async Task StopAsync_Always_CompletesImmediately()
	{
		// Arrange
		TestHarness harness = CreateHarness();
		try
		{
			// Act
			Task result = harness.Sut.StopAsync(CancellationToken.None);

			// Assert
			Assert.True(result.IsCompletedSuccessfully);
			await result;
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	#endregion
}
