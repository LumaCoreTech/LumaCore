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
		migrationBuilder.CreateTable(
			name: "Personas",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				ParticipantId = table.Column<long>(nullable: false),
				ActiveSystemPromptId = table.Column<long>(nullable: true),
				DefaultModel = table.Column<string>(maxLength: 100, nullable: true),
				Description = table.Column<string>(maxLength: 1000, nullable: true),
				IsActive = table.Column<bool>(nullable: false, defaultValue: true)
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
			});

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

		migrationBuilder.CreateIndex(
			name: "IX_MessageGenerationMetadata_ModelEndpointId",
			table: "MessageGenerationMetadata",
			column: "ModelEndpointId");

		migrationBuilder.CreateIndex(
			name: "IX_MessageGenerationMetadata_SystemPromptId",
			table: "MessageGenerationMetadata",
			column: "SystemPromptId");

		migrationBuilder.CreateIndex(
			name: "IX_Personas_ActiveSystemPromptId",
			table: "Personas",
			column: "ActiveSystemPromptId");

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
			columns: new[] { "PersonaId", "Hash" },
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_SystemPrompts_PublicId",
			table: "SystemPrompts",
			column: "PublicId",
			unique: true);

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
		migrationBuilder.DropForeignKey(
			name: "FK_Personas_SystemPrompts_ActiveSystemPromptId",
			table: "Personas");

		migrationBuilder.DropTable(name: "MessageGenerationMetadata");

		migrationBuilder.DropTable(name: "SystemPrompts");

		migrationBuilder.DropTable(name: "Personas");
	}
}
