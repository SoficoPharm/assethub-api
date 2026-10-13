using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceScreenSizeInches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ScreenSizeInches",
                table: "Devices",
                type: "decimal(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScreenSizeInches",
                table: "Devices");
        }
    }
}
