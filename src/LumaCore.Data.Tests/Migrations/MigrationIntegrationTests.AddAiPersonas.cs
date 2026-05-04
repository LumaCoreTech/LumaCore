// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Migrations;
using LumaCore.Data.Tests.Infrastructure;

using Xunit;

namespace LumaCore.Data.Tests.Migrations;

// AddAiPersonas migration: verify that Up() adds Personas, SystemPrompts,
// MessageGenerationMetadata, and PersonaDescriptionTranslations tables with their indexes,
// and Down() removes them.
//
//   1. Up — tables: applies InitialCreate + AddAiPersonas and asserts all domain tables exist.
//   2. Up — indexes: asserts all explicitly-defined indexes exist.
//   3. Down — reverts to InitialCreate and asserts the added tables are gone.
//
// Discovery-based assertions (same approach as InitialCreate): query the provider's system catalog for
// the complete list of objects rather than checking individual names.
public sealed partial class MigrationIntegrationTests
{
	/// <summary>
	/// The migration ID targeting <see cref="AddAiPersonas"/>, applying InitialCreate and AddAiPersonas.
	/// </summary>
	private const string AddAiPersonasMigrationId = "20260315135857_AddAiPersonas";

	/// <summary>
	/// All user tables after <see cref="AddAiPersonas.Up"/> has been applied, sorted alphabetically.
	/// Extends <see cref="sInitialCreateExpectedTables"/> with <c>MessageGenerationMetadata</c>,
	/// <c>Personas</c>, and <c>SystemPrompts</c>.
	/// </summary>
	private static readonly string[] sAddAiPersonasExpectedTables =
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
		"UserRoles",
		"Users"
	];

	/// <summary>
	/// All explicit indexes after <see cref="AddAiPersonas.Up"/> has been applied, sorted alphabetically.
	/// Extends <see cref="sInitialCreateExpectedIndexes"/> with 8 new indexes for the AI persona tables.
	/// </summary>
	private static readonly string[] sAddAiPersonasExpectedIndexes =
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

	// --- 1. Up — apply AddAiPersonas and verify tables ---

	/// <summary>
	/// Verifies that <see cref="AddAiPersonas.Up"/> adds <c>Personas</c>, <c>SystemPrompts</c>,
	/// <c>MessageGenerationMetadata</c>, and <c>PersonaDescriptionTranslations</c> tables,
	/// bringing the total to 14 domain tables. Uses discovery-based
	/// assertion (full table list from the system catalog).
	/// </summary>
	[Fact]
	public async Task AddAiPersonas_Up_CreatesAllExpectedTables()
	{
		// Arrange
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			// Act
			await harness.Migrator.MigrateAsync(AddAiPersonasMigrationId);

			// Assert — discovery-based: query the catalog for ALL user tables and compare the exact list
			string[] tables = await harness.GetUserTableNamesAsync();
			Assert.Equal(sAddAiPersonasExpectedTables, tables);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 2. Up — apply AddAiPersonas and verify indexes ---

	/// <summary>
	/// Verifies that <see cref="AddAiPersonas.Up"/> creates the additional explicit indexes for the AI persona
	/// tables. Uses discovery-based assertion (full index list from the system catalog).
	/// </summary>
	[Fact]
	public async Task AddAiPersonas_Up_CreatesAllExpectedIndexes()
	{
		// Arrange
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			// Act
			await harness.Migrator.MigrateAsync(AddAiPersonasMigrationId);

			// Assert — discovery-based: query the catalog for ALL explicit indexes and compare the exact list
			string[] indexes = await harness.GetExplicitIndexNamesAsync();
			Assert.Equal(sAddAiPersonasExpectedIndexes, indexes);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	// --- 3. Down — revert AddAiPersonas and verify tables are gone ---

	/// <summary>
	/// Verifies that <see cref="AddAiPersonas.Down"/> removes <c>Personas</c>, <c>SystemPrompts</c>,
	/// <c>MessageGenerationMetadata</c>, and <c>PersonaDescriptionTranslations</c>,
	/// restoring the schema to the <see cref="InitialCreate"/> state.
	/// </summary>
	[Fact]
	public async Task AddAiPersonas_Down_RemovesAiPersonaTables()
	{
		// Arrange — apply both migrations first
		IntegrationTestHarness harness = await CreateHarnessAsync();
		try
		{
			await harness.Migrator.MigrateAsync(AddAiPersonasMigrationId);

			// Act — revert to InitialCreate, executing AddAiPersonas.Down()
			await harness.Migrator.MigrateAsync(InitialCreateMigrationId);

			// Assert — back to the 10 InitialCreate tables
			string[] tables = await harness.GetUserTableNamesAsync();
			Assert.Equal(sInitialCreateExpectedTables, tables);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}
}
