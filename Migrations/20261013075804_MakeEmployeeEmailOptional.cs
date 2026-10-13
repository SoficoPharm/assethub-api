using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Api.Migrations
{
    /// <summary>Employee email is optional (Phase 2). Existing blank emails become NULL.</summary>
    public partial class MakeEmployeeEmailOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // Email is optional now; a blank email is stored as NULL.
            migrationBuilder.Sql("UPDATE Users SET Email = NULL WHERE LTRIM(RTRIM(Email)) = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
