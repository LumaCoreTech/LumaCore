// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Migrations;
using LumaCore.Data.Tests.Infrastructure;

using Xunit;

namespace LumaCore.Data.Tests.Migrations;

// AddResources migration: verify that Up() adds the resource-storage tables (Resources,
// ResourceReferences, ResourceGcState) with their indexes, and Down() removes them.
//
//   1. Up — tables: applies all four migrations and asserts all domain tables exist (discovery-based).
//   2. Up — indexes: asserts all explicit indexes exist.
//   3. Down — reverts to AddUserPreferences and asserts the added tables are gone.
//
// Discovery-based assertions (same approach as InitialCreate): query the provider's system catalog for
// the complete list of objects rather than checking individual names.
public sealed partial class MigrationIntegrationTests
{
	/// <summary>
	/// The migration ID targeting <see cref="AddResources"/>, applying InitialCreate, AddAiPersonas,
	/// AddUserPreferences, and AddResources.
	/// </summary>
	private const string AddResourcesMigrationId = "20260411201732_AddResources";

	/// <summary>
	/// All user tables after <see cref="AddResources.Up"/> has been applied, sorted alphabetically.
	/// Extends <see cref="sAddUserPreferencesExpectedTables"/> with <c>ResourceGcState</c>,
	/// <c>ResourceReferences</c>, and <c>Resources</c>.
	/// </summary>
	private static readonly string[] sAddResourcesExpectedTables =
	[
		"ConversationParticipants",
		"Conversations",
		"MessageGenerationMetadata",
		"Messages",
		"ModelEndpoints",
		"Participants",
		"PersonaDescriptionTranslations",
		"Personas",
		"ResourceGcState",
		"ResourceReferences",
		"Resources",
		"RevokedJwts",
		"Roles",
		"SeedHistory",
		"SystemPrompts",
		"UserPreferences",
		"UserRoles",
		"Users"
	];

	/// <summary>
	/// All explicit indexes after <see cref="AddResources.Up"/> has been applied, sorted alphabetically.
	/// Extends <see cref="sAddAiPersonasExpectedIndexes"/> with new indexes for the resource tables
	/// (<see cref="sAddUserPreferencesExpectedTables"/> introduced no new explicit indexes).
	/// </summary>
	private static readonly string[] sAddResourcesExpectedIndexes =
	[
		"IX_ConversationParticipants_LastReadMessageId",
		"IX_ConversationParticipants_ParticipantId",
		"IX_Conversations_PublicId",
		"IX_Conversations_UpdatedAtUtc",
		"IX_MessageGenerationMetadata_ModelEndpointId",
		"IX_MessageGenerationMetadata_SystemPromptId",
		"IX_Messages_ConversationId_CreatedAtUtc",
		"IX_Messages_PublicId",
		"IX_Messages_SenderId",
		"IX_ModelEndpoints_IsActive",
		"IX_ModelEndpoints_PublicId",
		"IX_Participants_PublicId",
		"IX_PersonaDescriptionTranslations_PersonaId_CultureCode",
		"IX_Personas_ActiveSystemPromptId",
		"IX_Personas_CreatedByParticipantId",
		"IX_Personas_IsActive",
		"IX_Personas_ParticipantId",
		"IX_ResourceReferences_OwnerKind_OwnerId",
		"IX_ResourceReferences_PublicId",
		"IX_ResourceReferences_ResourceId",
		"IX_Resources_ContentHash_DeletionState",
		"IX_Resources_CreatedByParticipantId",
		"IX_Resources_DeletionState",
		"IX_Resources_StoragePath",
		"IX_RevokedJwts_ExpiresAtUtc",
		"IX_Roles_Name",
		"IX_Roles_PublicId",
		"IX_SeedHistory_AppliedAtUtc",
		"IX_SeedHistory_SeedId",
		"IX_SystemPrompts_PersonaId_Hash",
		"IX_SystemPrompts_PublicId",
		"IX_UserRoles_RoleId",
		"IX_Users_Email",
		"IX_Users_ParticipantId",
		"IX_Users_Username",
		"IX_Users_UsernameNormalized"
	];

	// --- 1. Up — apply AddResources and verify tables ---

	/// <summary>
	/// Verifies that <see cref="AddResources.Up"/> adds <c>Resources</c>, <c>ResourceReferences</c>, and
	/// <c>ResourceGcState</c> tables. Uses discovery-based assertion (full table list from the system catalog).
	/// </summary>
	[Fact]
	public async Task AddResources_Up_CreatesAllExpectedTables()
	{
		// Arrange
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			// Act
			await harness.Migrator.MigrateAsync(AddResourcesMigrationId);

			// Assert — discovery-based: query the catalog for ALL user tables and compare the exact list
			string[] tables = await harness.GetUserTableNamesAsync();
			Assert.Equal(sAddResourcesExpectedTables, tables);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Up — apply AddResources and verify indexes ---

	/// <summary>
	/// Verifies that <see cref="AddResources.Up"/> creates additional explicit indexes for the resource
	/// tables. Uses discovery-based assertion (full index list from the system catalog).
	/// </summary>
	[Fact]
	public async Task AddResources_Up_CreatesAllExpectedIndexes()
	{
		// Arrange
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			// Act
			await harness.Migrator.MigrateAsync(AddResourcesMigrationId);

			// Assert — discovery-based: query the catalog for ALL explicit indexes and compare the exact list
			string[] indexes = await harness.GetExplicitIndexNamesAsync();
			Assert.Equal(sAddResourcesExpectedIndexes, indexes);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Down — revert AddResources and verify resource tables are gone ---

	/// <summary>
	/// Verifies that <see cref="AddResources.Down"/> removes <c>Resources</c>, <c>ResourceReferences</c>,
	/// and <c>ResourceGcState</c>, restoring the schema to the <see cref="AddUserPreferences"/> state.
	/// </summary>
	[Fact]
	public async Task AddResources_Down_RemovesResourceTables()
	{
		// Arrange — apply all four migrations first
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			await harness.Migrator.MigrateAsync(AddResourcesMigrationId);

			// Act — revert to AddUserPreferences, executing AddResources.Down()
			await harness.Migrator.MigrateAsync(AddUserPreferencesMigrationId);

			// Assert — back to the 14 AddUserPreferences tables
			string[] tables = await harness.GetUserTableNamesAsync();
			Assert.Equal(sAddUserPreferencesExpectedTables, tables);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
