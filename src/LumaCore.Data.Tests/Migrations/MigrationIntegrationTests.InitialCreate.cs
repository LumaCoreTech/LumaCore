// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Migrations;
using LumaCore.Data.Tests.Infrastructure;

using Xunit;

namespace LumaCore.Data.Tests.Migrations;

// InitialCreate migration: verify that Up() creates the expected schema and Down() removes it cleanly.
//
//   1. Up — tables: applies the migration and asserts all 10 domain tables exist (discovery-based).
//   2. Up — indexes: asserts all 21 explicitly-defined indexes exist (discovery-based).
//   3. Down — reverts the migration and asserts all domain tables are gone.
//
// All three tests use discovery-based assertions (querying the provider's system catalog for the
// complete list of objects) rather than per-name existence checks. This catches unexpected extra
// or missing objects that a simple TableExistsAsync() loop would miss.
public sealed partial class MigrationIntegrationTests
{
	/// <summary>
	/// The migration ID targeting <see cref="InitialCreate"/>, preventing later migrations from being applied.
	/// </summary>
	private const string InitialCreateMigrationId = "20260315135847_InitialCreate";

	/// <summary>
	/// All user tables created by <see cref="InitialCreate.Up"/>, sorted alphabetically for deterministic
	/// assertion. Used by <see cref="InitialCreate_Up_CreatesAllExpectedTables"/> and
	/// <see cref="InitialCreate_Down_RemovesAllCreatedTables"/>.
	/// </summary>
	private static readonly string[] sInitialCreateExpectedTables =
	[
		"ConversationParticipants",
		"Conversations",
		"Messages",
		"ModelEndpoints",
		"Participants",
		"RevokedJwts",
		"Roles",
		"SeedHistory",
		"UserRoles",
		"Users"
	];

	/// <summary>
	/// All indexes explicitly created by <see cref="InitialCreate.Up"/>, sorted alphabetically.
	/// Excludes provider-specific auto-indexes for PRIMARY KEY and UNIQUE constraints.
	/// </summary>
	private static readonly string[] sInitialCreateExpectedIndexes =
	[
		"IX_ConversationParticipants_ParticipantId",
		"IX_Conversations_PublicId",
		"IX_Conversations_UpdatedAtUtc",
		"IX_Messages_ConversationId",
		"IX_Messages_ConversationId_CreatedAtUtc",
		"IX_Messages_CreatedAtUtc",
		"IX_Messages_PublicId",
		"IX_Messages_SenderId",
		"IX_ModelEndpoints_IsActive",
		"IX_ModelEndpoints_PublicId",
		"IX_Participants_PublicId",
		"IX_RevokedJwts_ExpiresAtUtc",
		"IX_Roles_Name",
		"IX_Roles_PublicId",
		"IX_SeedHistory_AppliedAtUtc",
		"IX_SeedHistory_SeedId",
		"IX_UserRoles_RoleId",
		"IX_Users_Email",
		"IX_Users_ParticipantId",
		"IX_Users_Username",
		"IX_Users_UsernameNormalized"
	];

	// --- 1. Up — apply InitialCreate and verify tables ---

	/// <summary>
	/// Verifies that <see cref="InitialCreate.Up"/> creates all 10 expected domain tables against the
	/// configured database provider. Uses discovery-based assertion (full table list from the system catalog).
	/// </summary>
	[Fact]
	public async Task InitialCreate_Up_CreatesAllExpectedTables()
	{
		// Arrange
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			// Act
			await harness.Migrator.MigrateAsync(InitialCreateMigrationId);

			// Assert — discovery-based: query the catalog for ALL user tables and compare the exact list
			string[] tables = await harness.GetUserTableNamesAsync();
			Assert.Equal(sInitialCreateExpectedTables, tables);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Up — apply InitialCreate and verify indexes ---

	/// <summary>
	/// Verifies that <see cref="InitialCreate.Up"/> creates all 21 explicitly-defined indexes against the
	/// configured database provider. Uses discovery-based assertion (full index list from the system catalog).
	/// </summary>
	[Fact]
	public async Task InitialCreate_Up_CreatesAllExpectedIndexes()
	{
		// Arrange
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			// Act
			await harness.Migrator.MigrateAsync(InitialCreateMigrationId);

			// Assert — discovery-based: query the catalog for ALL explicit indexes and compare the exact list
			string[] indexes = await harness.GetExplicitIndexNamesAsync();
			Assert.Equal(sInitialCreateExpectedIndexes, indexes);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Down — revert InitialCreate and verify tables are gone ---

	/// <summary>
	/// Verifies that <see cref="InitialCreate.Down"/> removes all domain tables, leaving only
	/// EF Core infrastructure tables.
	/// </summary>
	[Fact]
	public async Task InitialCreate_Down_RemovesAllCreatedTables()
	{
		// Arrange — apply Up() first to create the schema
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			await harness.Migrator.MigrateAsync(InitialCreateMigrationId);

			// Act — revert to before any migration, executing Down()
			await harness.Migrator.MigrateAsync("0");

			// Assert — discovery-based: no domain tables remain
			string[] tables = await harness.GetUserTableNamesAsync();
			Assert.Empty(tables);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
