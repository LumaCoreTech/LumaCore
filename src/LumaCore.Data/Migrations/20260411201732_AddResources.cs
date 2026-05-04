#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LumaCore.Data.Migrations;

/// <inheritdoc/>
public partial class AddResources : Migration
{
	/// <inheritdoc/>
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Note: The scaffolded migration originally included AlterColumn operations that removed
		// Sqlite:Autoincrement annotations from all existing Id columns. These were no-ops for
		// SQLite (INTEGER PRIMARY KEY is always auto-increment) but triggered full table rebuilds
		// that failed with FK constraint errors. Removed to keep the migration safe.

		// --- Tables (alphabetical where dependencies allow) ---

		// ResourceGcState.Id is intentionally NOT auto-generated: the application always uses
		// Id = 1 (singleton row), see ResourceGcStateEntity / ConfigureResourceGcState
		// (ValueGeneratedNever). Hence no Identity / Autoincrement annotations here.
		migrationBuilder.CreateTable(
			name: "ResourceGcState",
			columns: table => new
			{
				Id = table.Column<int>(nullable: false),
				LastRunAtUtc = table.Column<DateTime>(nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ResourceGcState", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "Resources",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				ContentHash = table.Column<string>(maxLength: 64, nullable: false),
				StoragePath = table.Column<string>(maxLength: 100, nullable: false),
				SizeBytes = table.Column<long>(nullable: false),
				CreatedAtUtc = table.Column<DateTime>(nullable: false),
				CreatedByParticipantId = table.Column<long>(nullable: true),
				DeletionState = table.Column<int>(nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Resources", x => x.Id);
				table.ForeignKey(
					name: "FK_Resources_Participants_CreatedByParticipantId",
					column: x => x.CreatedByParticipantId,
					principalTable: "Participants",
					principalColumn: "Id",
					onDelete: ReferentialAction.SetNull);
			});

		// ResourceReferences would sort before Resources alphabetically, but its FK requires Resources first.
		migrationBuilder.CreateTable(
			name: "ResourceReferences",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(nullable: false),
				ResourceId = table.Column<long>(nullable: false),
				OwnerKind = table.Column<int>(nullable: false),
				OwnerId = table.Column<long>(nullable: false),
				OriginalFileName = table.Column<string>(maxLength: 255, nullable: true),
				ContentType = table.Column<string>(maxLength: 255, nullable: false),
				CreatedAtUtc = table.Column<DateTime>(nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ResourceReferences", x => x.Id);
				table.ForeignKey(
					name: "FK_ResourceReferences_Resources_ResourceId",
					column: x => x.ResourceId,
					principalTable: "Resources",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
			});

		// --- Indexes (alphabetical by index name) ---

		migrationBuilder.CreateIndex(
			name: "IX_ResourceReferences_OwnerKind_OwnerId",
			table: "ResourceReferences",
			columns: ["OwnerKind", "OwnerId"]);

		migrationBuilder.CreateIndex(
			name: "IX_ResourceReferences_PublicId",
			table: "ResourceReferences",
			column: "PublicId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_ResourceReferences_ResourceId",
			table: "ResourceReferences",
			column: "ResourceId");

		migrationBuilder.CreateIndex(
			name: "IX_Resources_ContentHash_DeletionState",
			table: "Resources",
			columns: ["ContentHash", "DeletionState"],
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_Resources_CreatedByParticipantId",
			table: "Resources",
			column: "CreatedByParticipantId");

		migrationBuilder.CreateIndex(
			name: "IX_Resources_DeletionState",
			table: "Resources",
			column: "DeletionState");

		migrationBuilder.CreateIndex(
			name: "IX_Resources_StoragePath",
			table: "Resources",
			column: "StoragePath",
			unique: true);
	}

	/// <inheritdoc/>
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// Drop in reverse-Up order: most-dependent first (ResourceReferences → FK to Resources),
		// then the principal (Resources), and finally the standalone singleton (ResourceGcState).
		// Provider-agnostic: prevents FK-violation errors on engines that enforce constraints during
		// DROP (PostgreSQL/SQL Server) and matches how a manual schema teardown would be ordered.
		migrationBuilder.DropTable(name: "ResourceReferences");

		migrationBuilder.DropTable(name: "Resources");

		migrationBuilder.DropTable(name: "ResourceGcState");
	}
}
