// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;

using LumaCore.Data.Initialization;
using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class ProviderOperationsIntegrationTests
{
	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations.DropSchemaObjectsAsync"/> drops all user tables
	/// from the database while leaving system catalog tables intact.
	/// </summary>
	[Fact]
	public async Task DropSchemaObjectsAsync_WhenCalled_DropsAllUserTables()
	{
		// Arrange
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			DbConnection connection = await harness.GetOpenConnectionAsync();

			// Verify entity tables exist before the drop (created by EnsureCreatedAsync).
			Assert.True(await harness.Sut.TableExistsAsync(connection, "Users", CancellationToken.None));
			Assert.True(await harness.Sut.TableExistsAsync(connection, "Conversations", CancellationToken.None));

			// Act
			await harness.Sut.DropSchemaObjectsAsync(
				harness.DbContext,
				new HashSet<string>(),
				CancellationToken.None);

			// Assert — all user tables should be gone
			Assert.False(await harness.Sut.TableExistsAsync(connection, "Users", CancellationToken.None));
			Assert.False(await harness.Sut.TableExistsAsync(connection, "Conversations", CancellationToken.None));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Verifies that <see cref="IDatabaseProviderOperations.DropSchemaObjectsAsync"/> preserves tables
	/// listed in <c>tablesToPreserve</c> while dropping all other user tables. Uses a checkpoint table
	/// created via <see cref="IDatabaseProviderOperations.WriteCheckpointAsync"/> to mirror the production
	/// restore scenario.
	/// </summary>
	[Fact]
	public async Task DropSchemaObjectsAsync_WhenPreserveSpecified_PreservesListedTables()
	{
		// Arrange
		TestHarness harness = await CreateHarnessAsync();
		try
		{
			DbConnection connection = await harness.GetOpenConnectionAsync();

			// Create a checkpoint table — the production use case for tablesToPreserve.
			await harness.Sut.WriteCheckpointAsync(
				harness.DbContext,
				"__TestCheckpoint",
				shuttleId: "shuttle-001",
				baselineMigrationId: "20260101_Init",
				startedUtc: "2026-01-15T10:00:00Z",
				CancellationToken.None);

			Assert.True(await harness.Sut.TableExistsAsync(connection, "__TestCheckpoint", CancellationToken.None));
			Assert.True(await harness.Sut.TableExistsAsync(connection, "Users", CancellationToken.None));

			// Act — preserve only the checkpoint table
			await harness.Sut.DropSchemaObjectsAsync(
				harness.DbContext,
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "__TestCheckpoint" },
				CancellationToken.None);

			// Assert — preserved table still exists with data intact
			Assert.True(await harness.Sut.TableExistsAsync(connection, "__TestCheckpoint", CancellationToken.None));

			RestoreCheckpointData? checkpoint = await harness.Sut.ReadCheckpointAsync(
				                                    connection,
				                                    "__TestCheckpoint",
				                                    CancellationToken.None);
			Assert.NotNull(checkpoint);
			Assert.Equal("shuttle-001", checkpoint.ShuttleId);

			// Assert — other tables are gone
			Assert.False(await harness.Sut.TableExistsAsync(connection, "Users", CancellationToken.None));
			Assert.False(await harness.Sut.TableExistsAsync(connection, "Conversations", CancellationToken.None));
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
