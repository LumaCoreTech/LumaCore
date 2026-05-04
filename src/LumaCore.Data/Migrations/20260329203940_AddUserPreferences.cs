#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace LumaCore.Data.Migrations;

/// <inheritdoc/>
public partial class AddUserPreferences : Migration
{
	/// <inheritdoc/>
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "UserPreferences",
			columns: table => new
			{
				UserId = table.Column<long>(nullable: false),
				PreferencesJson = table.Column<string>(maxLength: 4000, nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_UserPreferences", x => x.UserId);
				table.ForeignKey(
					name: "FK_UserPreferences_Users_UserId",
					column: x => x.UserId,
					principalTable: "Users",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
			});
	}

	/// <inheritdoc/>
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "UserPreferences");
	}
}
