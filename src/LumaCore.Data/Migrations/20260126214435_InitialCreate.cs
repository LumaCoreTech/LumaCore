#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace LumaCore.Data.Migrations;

/// <inheritdoc/>
public partial class InitialCreate : Migration
{
	/// <inheritdoc/>
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "Conversations",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false)
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
				Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
				CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
				UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Conversations", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "ModelEndpoints",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false)
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
				ProviderType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
				BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
				Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
				Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
				IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
				EncryptedCredentials = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ModelEndpoints", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "Participants",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false)
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
				DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
				AvatarUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Participants", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "Roles",
			columns: table => new
			{
				Id = table.Column<long>(type: "INTEGER", nullable: false)
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
				CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
				Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
				Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_Roles", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "SeedHistory",
			columns: table => new
			{
				Id = table.Column<int>(type: "INTEGER", nullable: false)
					.Annotation("Sqlite:Autoincrement", true),
				SeedId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
				Version = table.Column<int>(type: "INTEGER", nullable: false),
				Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
				AppliedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_SeedHistory", x => x.Id);
			});

		migrationBuilder.CreateTable(
			name: "ConversationParticipants",
			columns: table => new
			{
				ConversationId = table.Column<long>(type: "INTEGER", nullable: false),
				ParticipantId = table.Column<long>(type: "INTEGER", nullable: false),
				JoinedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
				Role = table.Column<int>(type: "INTEGER", nullable: false)
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
				Id = table.Column<long>(type: "INTEGER", nullable: false)
					.Annotation("Sqlite:Autoincrement", true),
				PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
				ConversationId = table.Column<long>(type: "INTEGER", nullable: false),
				SenderId = table.Column<long>(type: "INTEGER", nullable: true),
				CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
				Content = table.Column<string>(type: "TEXT", nullable: true),
				RedactedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
				RedactionReason = table.Column<int>(type: "INTEGER", nullable: true)
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
				Id = table.Column<long>(type: "INTEGER", nullable: false)
					.Annotation("Sqlite:Autoincrement", true),
				ParticipantId = table.Column<long>(type: "INTEGER", nullable: false),
				LastLoginAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
				LastTokenRefreshAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
				Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
				PasswordHash = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
				Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
				UsernameNormalized = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
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

		migrationBuilder.CreateTable(
			name: "UserRoles",
			columns: table => new
			{
				UserId = table.Column<long>(type: "INTEGER", nullable: false),
				RoleId = table.Column<long>(type: "INTEGER", nullable: false),
				AssignedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
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
			name: "IX_Messages_ConversationId",
			table: "Messages",
			column: "ConversationId");

		migrationBuilder.CreateIndex(
			name: "IX_Messages_ConversationId_CreatedAtUtc",
			table: "Messages",
			columns: new[] { "ConversationId", "CreatedAtUtc" });

		migrationBuilder.CreateIndex(
			name: "IX_Messages_CreatedAtUtc",
			table: "Messages",
			column: "CreatedAtUtc");

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
			name: "IX_UserRoles_RoleId",
			table: "UserRoles",
			column: "RoleId");

		migrationBuilder.CreateIndex(
			name: "IX_Users_Email",
			table: "Users",
			column: "Email",
			unique: true);

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

		migrationBuilder.CreateIndex(
			name: "IX_SeedHistory_SeedId",
			table: "SeedHistory",
			column: "SeedId",
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_SeedHistory_AppliedAtUtc",
			table: "SeedHistory",
			column: "AppliedAtUtc");
	}

	/// <inheritdoc/>
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "ConversationParticipants");

		migrationBuilder.DropTable(name: "UserRoles");

		migrationBuilder.DropTable(name: "Messages");

		migrationBuilder.DropTable(name: "Users");

		migrationBuilder.DropTable(name: "ModelEndpoints");

		migrationBuilder.DropTable(name: "Roles");

		migrationBuilder.DropTable(name: "SeedHistory");

		migrationBuilder.DropTable(name: "Conversations");

		migrationBuilder.DropTable(name: "Participants");
	}
}
