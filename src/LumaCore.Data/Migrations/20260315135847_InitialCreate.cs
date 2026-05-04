#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LumaCore.Data.Migrations;

/// <inheritdoc/>
public partial class InitialCreate : Migration
{
	/// <inheritdoc/>
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// --- Independent tables (no foreign keys, alphabetical) ---

		migrationBuilder.CreateTable(
			name: "Conversations",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(nullable: false),
				Title = table.Column<string>(maxLength: 200, nullable: false),
				Description = table.Column<string>(maxLength: 500, nullable: true),
				CreatedAtUtc = table.Column<DateTime>(nullable: false),
				UpdatedAtUtc = table.Column<DateTime>(nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Conversations", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "ModelEndpoints",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(nullable: false),
				CreatedAtUtc = table.Column<DateTime>(nullable: false),
				UpdatedAtUtc = table.Column<DateTime>(nullable: false),
				ProviderType = table.Column<string>(maxLength: 50, nullable: false),
				BaseUrl = table.Column<string>(maxLength: 500, nullable: false),
				Name = table.Column<string>(maxLength: 100, nullable: false),
				Description = table.Column<string>(maxLength: 500, nullable: true),
				IsActive = table.Column<bool>(nullable: false, defaultValue: true),
				EncryptedCredentials = table.Column<string>(maxLength: 4000, nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ModelEndpoints", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "Participants",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(nullable: false),
				CreatedAtUtc = table.Column<DateTime>(nullable: false),
				DisplayName = table.Column<string>(maxLength: 100, nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Participants", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "RevokedJwts",
			columns: table => new
			{
				Jti = table.Column<string>(maxLength: 36, nullable: false),
				ExpiresAtUtc = table.Column<DateTime>(nullable: false),
				RevokedAtUtc = table.Column<DateTime>(nullable: false),
				Subject = table.Column<string>(maxLength: 50, nullable: false),
				Reason = table.Column<string>(maxLength: 100, nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RevokedJwts", x => x.Jti);
			});

		migrationBuilder.CreateTable(
			name: "Roles",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(nullable: false),
				CreatedAtUtc = table.Column<DateTime>(nullable: false),
				Name = table.Column<string>(maxLength: 50, nullable: false),
				Description = table.Column<string>(maxLength: 200, nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Roles", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "SeedHistory",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				SeedId = table.Column<string>(maxLength: 100, nullable: false),
				Version = table.Column<int>(nullable: false),
				Description = table.Column<string>(maxLength: 200, nullable: false),
				AppliedAtUtc = table.Column<DateTime>(nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_SeedHistory", x => x.Id);
			});

		// --- Dependent tables (have foreign keys, alphabetical where dependencies allow) ---

		migrationBuilder.CreateTable(
			name: "ConversationParticipants",
			columns: table => new
			{
				ConversationId = table.Column<long>(nullable: false),
				ParticipantId = table.Column<long>(nullable: false),
				JoinedAtUtc = table.Column<DateTime>(nullable: false),
				LastReadMessageId = table.Column<long>(nullable: true),
				Role = table.Column<int>(nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ConversationParticipants", x => new { x.ConversationId, x.ParticipantId });
				table.ForeignKey(
					name: "FK_ConversationParticipants_Conversations_ConversationId",
					column: x => x.ConversationId,
					principalTable: "Conversations",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
				table.ForeignKey(
					name: "FK_ConversationParticipants_Participants_ParticipantId",
					column: x => x.ParticipantId,
					principalTable: "Participants",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
			});

		migrationBuilder.CreateTable(
			name: "Messages",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(nullable: false),
				ConversationId = table.Column<long>(nullable: false),
				SenderId = table.Column<long>(nullable: true),
				CreatedAtUtc = table.Column<DateTime>(nullable: false),
				Type = table.Column<int>(nullable: false, defaultValue: 0),
				Content = table.Column<string>(nullable: true),
				RedactedAtUtc = table.Column<DateTime>(nullable: true),
				RedactionReason = table.Column<int>(nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Messages", x => x.Id);
				table.ForeignKey(
					name: "FK_Messages_Conversations_ConversationId",
					column: x => x.ConversationId,
					principalTable: "Conversations",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
				table.ForeignKey(
					name: "FK_Messages_Participants_SenderId",
					column: x => x.SenderId,
					principalTable: "Participants",
					principalColumn: "Id",
					onDelete: ReferentialAction.SetNull);
			});

		migrationBuilder.CreateTable(
			name: "Users",
			columns: table => new
			{
				Id = table.Column<long>(nullable: false)
					.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
					.Annotation("SqlServer:Identity", "1, 1")
					.Annotation("Sqlite:Autoincrement", true),
				ParticipantId = table.Column<long>(nullable: false),
				CreatedAtUtc = table.Column<DateTime>(nullable: false),
				LastLoginAtUtc = table.Column<DateTime>(nullable: true),
				LastTokenRefreshAtUtc = table.Column<DateTime>(nullable: true),
				Email = table.Column<string>(maxLength: 254, nullable: true),
				PasswordHash = table.Column<string>(maxLength: 255, nullable: false),
				Username = table.Column<string>(maxLength: 50, nullable: false),
				UsernameNormalized = table.Column<string>(maxLength: 50, nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Users", x => x.Id);
				table.ForeignKey(
					name: "FK_Users_Participants_ParticipantId",
					column: x => x.ParticipantId,
					principalTable: "Participants",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict);
			});

		// UserRoles would sort before Users alphabetically, but its FK to Users requires Users to exist first.
		migrationBuilder.CreateTable(
			name: "UserRoles",
			columns: table => new
			{
				UserId = table.Column<long>(nullable: false),
				RoleId = table.Column<long>(nullable: false),
				AssignedAtUtc = table.Column<DateTime>(nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
				table.ForeignKey(
					name: "FK_UserRoles_Roles_RoleId",
					column: x => x.RoleId,
					principalTable: "Roles",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
				table.ForeignKey(
					name: "FK_UserRoles_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
			});

		// --- Deferred foreign keys (cross-reference between dependent tables) ---

		migrationBuilder.AddForeignKey(
			name: "FK_ConversationParticipants_Messages_LastReadMessageId",
			table: "ConversationParticipants",
			column: "LastReadMessageId",
			principalTable: "Messages",
			principalColumn: "Id",
			onDelete: ReferentialAction.SetNull);

		// --- Indexes (alphabetical by index name) ---

		migrationBuilder.CreateIndex(
			name: "IX_ConversationParticipants_LastReadMessageId",
			table: "ConversationParticipants",
			column: "LastReadMessageId");

		migrationBuilder.CreateIndex(
			name: "IX_ConversationParticipants_ParticipantId",
			table: "ConversationParticipants",
			column: "ParticipantId");

		migrationBuilder.CreateIndex(
			name: "IX_Conversations_PublicId",
			table: "Conversations",
			column: "PublicId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_Conversations_UpdatedAtUtc",
			table: "Conversations",
			column: "UpdatedAtUtc");

		migrationBuilder.CreateIndex(
			name: "IX_Messages_ConversationId_CreatedAtUtc",
			table: "Messages",
			columns: ["ConversationId", "CreatedAtUtc"]);

		migrationBuilder.CreateIndex(
			name: "IX_Messages_PublicId",
			table: "Messages",
			column: "PublicId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_Messages_SenderId",
			table: "Messages",
			column: "SenderId");

		migrationBuilder.CreateIndex(
			name: "IX_ModelEndpoints_IsActive",
			table: "ModelEndpoints",
			column: "IsActive");

		migrationBuilder.CreateIndex(
			name: "IX_ModelEndpoints_PublicId",
			table: "ModelEndpoints",
			column: "PublicId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_Participants_PublicId",
			table: "Participants",
			column: "PublicId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_RevokedJwts_ExpiresAtUtc",
			table: "RevokedJwts",
			column: "ExpiresAtUtc");

		migrationBuilder.CreateIndex(
			name: "IX_Roles_Name",
			table: "Roles",
			column: "Name",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_Roles_PublicId",
			table: "Roles",
			column: "PublicId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_SeedHistory_AppliedAtUtc",
			table: "SeedHistory",
			column: "AppliedAtUtc");

		migrationBuilder.CreateIndex(
			name: "IX_SeedHistory_SeedId",
			table: "SeedHistory",
			column: "SeedId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_UserRoles_RoleId",
			table: "UserRoles",
			column: "RoleId");

		// Provider-specific filter for the nullable unique Users.Email index. Delegated to the
		// DbContext so model snapshot and migration cannot drift apart — both sides throw
		// consistently for unknown providers (the DbContext would already fail at model-build
		// time, so a silent null fallback here would be dead code).
		migrationBuilder.CreateIndex(
			name: "IX_Users_Email",
			table: "Users",
			column: "Email",
			unique: true,
			filter: LumaCoreDbContext.GetUniqueEmailIndexFilter(ActiveProvider));

		migrationBuilder.CreateIndex(
			name: "IX_Users_ParticipantId",
			table: "Users",
			column: "ParticipantId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_Users_Username",
			table: "Users",
			column: "Username");

		migrationBuilder.CreateIndex(
			name: "IX_Users_UsernameNormalized",
			table: "Users",
			column: "UsernameNormalized",
			unique: true);
	}

	/// <inheritdoc/>
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// --- Dependent tables first (reverse dependency order, alphabetical within tier) ---

		migrationBuilder.DropTable(name: "ConversationParticipants");
		migrationBuilder.DropTable(name: "Messages");
		migrationBuilder.DropTable(name: "UserRoles");

		// Users must be dropped after UserRoles (FK dependency).
		migrationBuilder.DropTable(name: "Users");

		// --- Independent tables (alphabetical) ---

		migrationBuilder.DropTable(name: "Conversations");
		migrationBuilder.DropTable(name: "ModelEndpoints");
		migrationBuilder.DropTable(name: "Participants");
		migrationBuilder.DropTable(name: "RevokedJwts");
		migrationBuilder.DropTable(name: "Roles");
		migrationBuilder.DropTable(name: "SeedHistory");
	}
}
