using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Api.Migrations
{
    /// <summary>
    /// Data-only migration (B9, B3):
    /// 1. Device status "Active" is merged into "Assigned".
    /// 2. Active assignments on devices that are no longer Assigned (left behind by the old
    ///    edit-device bug) are closed. GETDATE() (local time) is used deliberately: the next
    ///    migration, ConvertTimestampsToUtc, converts all existing local timestamps to UTC.
    /// </summary>
    public partial class UnifyDeviceStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE Devices SET Status = 'Assigned' WHERE Status = 'Active';

UPDATE a
SET a.Status = 'Returned',
    a.ReturnedDate = GETDATE()
FROM Assignments a
INNER JOIN Devices d ON d.Id = a.DeviceId
WHERE a.Status = 'Active' AND d.Status <> 'Assigned';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: the original "Active" rows cannot be told apart from "Assigned".
        }
    }
}
