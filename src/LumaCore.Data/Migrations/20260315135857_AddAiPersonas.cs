#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LumaCore.Data.Migrations;

/// <inheritdoc/>
public partial class AddAiPersonas : Migration
{
	/// <inheritdoc/>
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// --- Tables (alphabetical where dependencies allow) ---

		// MessageGenerationMetadata sorts first alphabetically and depends only on pre-existing tables.
		// The nullable SystemPromptId FK is added later once SystemPrompts exists.
		//
		// MessageId is intentionally declared without an Identity/Autoincrement annotation: it is both
		// the primary key and a foreign key to Messages.Id (1:1 owned-side pattern — each metadata row
		// borrows its parent message's ID). EF Core configures this via
		// HasOne(...).WithOne(...).HasForeignKey<MessageGenerationMetadataEntity>(e => e.MessageId).
		migrationBuilder.CreateTable(
			name: "MessageGenerationMetadata",
			columns: table => new
			{
				MessageId = table.Column<long>(nullable: false),
				ModelEndpointId = table.Column<long>(nullable: false),
				SystemPromptId = table.Column<long>(nullable: true),
				Model = table.Column<string>(maxLength: 100, nullable: false),
				FullPrompt = table.Column<string>(nullable: true),
				PromptTokens = table.Column<int>(nullable: false),
				CompletionTokens = table.Column<int>(nullable: false),
				ResponseTime = table.Column<TimeSpan>(nullable: false),
				MaxTokens = table.Column<int>(nullable: true),
				Temperature = table.Column<double>(nullable: true),
				TopP = table.Column<double>(nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_MessageGenerationMetadata", x => x.MessageId);
				table.ForeignKey(
					name: "FK_MessageGenerationMetadata_Messages_MessageId",
					column: x => x.MessageId,
					principalTable: "Messages",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
				table.ForeignKey(
					name: "FK_MessageGenerationMetadata_ModelEndpoints_ModelEndpointId",
					column: x => x.ModelEndpointId,
					principalTable: "ModelEndpoints",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict);
			});

		migrationBuilder.CreateTable(
			name: "Personas",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				ParticipantId = table.Column<long>(nullable: false),
				CreatedAtUtc = table.Column<DateTime>(nullable: false),
				UpdatedAtUtc = table.Column<DateTime>(nullable: false),
				CreatedByParticipantId = table.Column<long>(nullable: true),
				ActiveSystemPromptId = table.Column<long>(nullable: true),
				DefaultModel = table.Column<string>(maxLength: 100, nullable: true),
				IsActive = table.Column<bool>(nullable: false, defaultValue: true),
				Visibility = table.Column<int>(nullable: false, defaultValue: 0)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Personas", x => x.Id);
				table.ForeignKey(
					name: "FK_Personas_Participants_ParticipantId",
					column: x => x.ParticipantId,
					principalTable: "Participants",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
				table.ForeignKey(
					name: "FK_Personas_Participants_CreatedByParticipantId",
					column: x => x.CreatedByParticipantId,
					principalTable: "Participants",
					principalColumn: "Id",
					onDelete: ReferentialAction.SetNull);
			});

		// SystemPrompts cannot move ahead of Personas because PersonaId is an immediate FK.
		migrationBuilder.CreateTable(
			name: "SystemPrompts",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(nullable: false),
				PersonaId = table.Column<long>(nullable: false),
				CreatedAtUtc = table.Column<DateTime>(nullable: false),
				Content = table.Column<string>(nullable: false),
				Hash = table.Column<string>(maxLength: 64, nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_SystemPrompts", x => x.Id);
				table.ForeignKey(
					name: "FK_SystemPrompts_Personas_PersonaId",
					column: x => x.PersonaId,
					principalTable: "Personas",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
			});

		// PersonaDescriptionTranslations stores localized description translations for personas.
		// FK to Personas uses Cascade delete so translations are removed when their persona is deleted.
		migrationBuilder.CreateTable(
			name: "PersonaDescriptionTranslations",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				PersonaId = table.Column<long>(nullable: false),
				CultureCode = table.Column<string>(maxLength: 10, nullable: false),
				Value = table.Column<string>(maxLength: 2000, nullable: false),
				Source = table.Column<int>(nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_PersonaDescriptionTranslations", x => x.Id);
				table.ForeignKey(
					name: "FK_PersonaDescriptionTranslations_Personas_PersonaId",
					column: x => x.PersonaId,
					principalTable: "Personas",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
			});

		// --- Indexes (alphabetical by index name) ---

		migrationBuilder.CreateIndex(
			name: "IX_MessageGenerationMetadata_ModelEndpointId",
			table: "MessageGenerationMetadata",
			column: "ModelEndpointId");

		migrationBuilder.CreateIndex(
			name: "IX_MessageGenerationMetadata_SystemPromptId",
			table: "MessageGenerationMetadata",
			column: "SystemPromptId");

		migrationBuilder.CreateIndex(
			name: "IX_PersonaDescriptionTranslations_PersonaId_CultureCode",
			table: "PersonaDescriptionTranslations",
			columns: ["PersonaId", "CultureCode"],
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_Personas_ActiveSystemPromptId",
			table: "Personas",
			column: "ActiveSystemPromptId");

		migrationBuilder.CreateIndex(
			name: "IX_Personas_CreatedByParticipantId",
			table: "Personas",
			column: "CreatedByParticipantId");

		migrationBuilder.CreateIndex(
			name: "IX_Personas_IsActive",
			table: "Personas",
			column: "IsActive");

		migrationBuilder.CreateIndex(
			name: "IX_Personas_ParticipantId",
			table: "Personas",
			column: "ParticipantId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_SystemPrompts_PersonaId_Hash",
			table: "SystemPrompts",
			columns: ["PersonaId", "Hash"],
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_SystemPrompts_PublicId",
			table: "SystemPrompts",
			column: "PublicId",
			unique: true);

		// --- Deferred foreign keys (cross-reference between the new tables) ---

		migrationBuilder.AddForeignKey(
			name: "FK_MessageGenerationMetadata_SystemPrompts_SystemPromptId",
			table: "MessageGenerationMetadata",
			column: "SystemPromptId",
			principalTable: "SystemPrompts",
			principalColumn: "Id",
			onDelete: ReferentialAction.SetNull);

		migrationBuilder.AddForeignKey(
			name: "FK_Personas_SystemPrompts_ActiveSystemPromptId",
			table: "Personas",
			column: "ActiveSystemPromptId",
			principalTable: "SystemPrompts",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);
	}

	/// <inheritdoc/>
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// Drop PersonaDescriptionTranslations first — it has FK to Personas (Cascade).
		migrationBuilder.DropTable(name: "PersonaDescriptionTranslations");

		// Drop MessageGenerationMetadata first — it references both SystemPrompts and Messages.
		// This implicitly removes FK_MessageGenerationMetadata_SystemPrompts_SystemPromptId (one of
		// the two deferred FKs added in Up), so no explicit DropForeignKey is needed for it.
		migrationBuilder.DropTable(name: "MessageGenerationMetadata");

		// Personas ↔ SystemPrompts have a circular FK relationship:
		//   - FK_SystemPrompts_Personas_PersonaId          (SystemPrompts → Personas)
		//   - FK_Personas_SystemPrompts_ActiveSystemPromptId (Personas → SystemPrompts)
		//
		// Unlike the MessageGenerationMetadata FK above, this one cannot be left to DropTable: dropping
		// SystemPrompts first would fail on enforcing providers (PostgreSQL / SQL Server) because
		// Personas still references it; dropping Personas first would fail because SystemPrompts still
		// references Personas. Break the cycle by removing the Personas → SystemPrompts FK explicitly
		// before dropping the tables.
		//
		// SQLite does not enforce FK constraints during DROP TABLE, and its DropForeignKey
		// implementation triggers a table rebuild that fails here because the target model
		// (InitialCreate) does not contain these tables — so skip the explicit drop on SQLite.
		if (migrationBuilder.ActiveProvider != "Microsoft.EntityFrameworkCore.Sqlite")
		{
			migrationBuilder.DropForeignKey(
				name: "FK_Personas_SystemPrompts_ActiveSystemPromptId",
				table: "Personas");
		}

		migrationBuilder.DropTable(name: "SystemPrompts");

		migrationBuilder.DropTable(name: "Personas");
	}
}
