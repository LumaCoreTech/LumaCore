// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Migrations;
using LumaCore.Data.Tests.Infrastructure;

using Xunit;

namespace LumaCore.Data.Tests.Migrations;

// AddUserPreferences migration: verify that Up() adds the UserPreferences table and Down() removes it.
//
//   1. Up — tables: applies all three migrations and asserts all domain tables exist (discovery-based).
//   2. Up — indexes: asserts index list is unchanged from AddAiPersonas (no new explicit indexes — PK is
//      auto-generated).
//   3. Down — reverts to AddAiPersonas and asserts UserPreferences is gone.
//
// Discovery-based assertions (same approach as InitialCreate): query the provider's system catalog for
// the complete list of objects rather than checking individual names.
public sealed partial class MigrationIntegrationTests
{
	/// <summary>
	/// The migration ID targeting <see cref="AddUserPreferences"/>, applying InitialCreate, AddAiPersonas,
	/// and AddUserPreferences.
	/// </summary>
	private const string AddUserPreferencesMigrationId = "20260329203940_AddUserPreferences";

	/// <summary>
	/// All user tables after <see cref="AddUserPreferences.Up"/> has been applied, sorted alphabetically.
	/// Extends <see cref="sAddAiPersonasExpectedTables"/> with the <c>UserPreferences</c> table.
	/// </summary>
	private static readonly string[] sAddUserPreferencesExpectedTables =
	[
		"ConversationParticipants",
		"Conversations",
		"MessageGenerationMetadata",
		"Messages",
		"ModelEndpoints",
		"Participants",
		"PersonaDescriptionTranslations",
		"Personas",
		"RevokedJwts",
		"Roles",
		"SeedHistory",
		"SystemPrompts",
		"UserPreferences",
		"UserRoles",
		"Users"
	];

	// --- 1. Up — apply AddUserPreferences and verify tables ---

	/// <summary>
	/// Verifies that <see cref="AddUserPreferences.Up"/> adds the <c>UserPreferences</c> table.
	/// Uses discovery-based assertion (full table list from the system catalog).
	/// </summary>
	[Fact]
	public async Task AddUserPreferences_Up_CreatesAllExpectedTables()
	{
		// Arrange
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			// Act
			await harness.Migrator.MigrateAsync(AddUserPreferencesMigrationId);

			// Assert — discovery-based: query the catalog for ALL user tables and compare the exact list
			string[] tables = await harness.GetUserTableNamesAsync();
			Assert.Equal(sAddUserPreferencesExpectedTables, tables);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Up — apply AddUserPreferences and verify indexes are unchanged ---

	/// <summary>
	/// Verifies that <see cref="AddUserPreferences.Up"/> does not create additional explicit indexes.
	/// The <c>UserPreferences</c> table uses <c>UserId</c> as its primary key (auto-indexed by the provider),
	/// same as after <see cref="AddAiPersonas"/>.
	/// </summary>
	[Fact]
	public async Task AddUserPreferences_Up_DoesNotCreateAdditionalIndexes()
	{
		// Arrange
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			// Act
			await harness.Migrator.MigrateAsync(AddUserPreferencesMigrationId);

			// Assert — index set is identical to AddAiPersonas (PK index is provider-auto, not explicit)
			string[] indexes = await harness.GetExplicitIndexNamesAsync();
			Assert.Equal(sAddAiPersonasExpectedIndexes, indexes);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Down — revert AddUserPreferences and verify UserPreferences table is gone ---

	/// <summary>
	/// Verifies that <see cref="AddUserPreferences.Down"/> removes the <c>UserPreferences</c> table, restoring
	/// the schema to the <see cref="AddAiPersonas"/> state.
	/// </summary>
	[Fact]
	public async Task AddUserPreferences_Down_RemovesUserPreferencesTable()
	{
		// Arrange — apply all three migrations first
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			await harness.Migrator.MigrateAsync(AddUserPreferencesMigrationId);

			// Act — revert to AddAiPersonas, executing AddUserPreferences.Down()
			await harness.Migrator.MigrateAsync(AddAiPersonasMigrationId);

			// Assert — back to the 13 AddAiPersonas tables
			string[] tables = await harness.GetUserTableNamesAsync();
			Assert.Equal(sAddAiPersonasExpectedTables, tables);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
