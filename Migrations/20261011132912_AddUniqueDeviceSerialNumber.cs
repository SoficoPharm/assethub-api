using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueDeviceSerialNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Normalize existing serials the same way the API does (SerialNumbers.Normalize):
            //    trim whitespace, upper-case, empty -> NULL.
            migrationBuilder.Sql(@"
UPDATE Devices
SET SerialNumber = NULLIF(UPPER(TRIM(CHAR(9) + CHAR(10) + CHAR(13) + ' ' FROM SerialNumber)), '')
WHERE SerialNumber IS NOT NULL;");

            // 2. Stop with a readable error if duplicates remain; nothing is deleted here.
            //    The migration runs in a transaction, so the UPDATE above is rolled back too.
            migrationBuilder.Sql(@"
DECLARE @dups nvarchar(max) = (
    SELECT STRING_AGG(CONCAT(SerialNumber, ' (', Devices, ' devices)'), ', ')
    FROM (SELECT SerialNumber, COUNT(*) AS Devices
          FROM Devices
          WHERE SerialNumber IS NOT NULL
          GROUP BY SerialNumber
          HAVING COUNT(*) > 1) AS d);

IF @dups IS NOT NULL
BEGIN
    DECLARE @msg nvarchar(2048) = CONCAT('Duplicate device serial numbers must be resolved first: ', @dups);
    THROW 50001, @msg, 1;
END");

            // 3. Unique filtered index: devices without a serial (NULL) are still allowed.
            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "Devices",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true,
                filter: "[SerialNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The serial normalization is not reverted (original spacing/casing is not kept).
            migrationBuilder.DropIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
