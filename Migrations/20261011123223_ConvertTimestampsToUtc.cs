using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Api.Migrations
{
    /// <summary>
    /// Data-only migration: timestamps were written with DateTime.Now (server local time,
    /// Egypt Standard Time). The app now writes DateTime.UtcNow, so existing rows are
    /// converted to UTC to keep each column consistent. AT TIME ZONE handles DST.
    /// </summary>
    public partial class ConvertTimestampsToUtc : Migration
    {
        private const string SourceTimeZone = "Egypt Standard Time";

        private static readonly (string Table, string Column)[] Columns =
        {
            ("Devices", "CreatedAt"),
            ("Users", "CreatedAt"),
            ("SystemUsers", "CreatedAt"),
            ("Assignments", "AssignedDate"),
            ("Assignments", "ReturnedDate"),
            ("AuditLogs", "Timestamp")
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in Columns)
            {
                migrationBuilder.Sql($@"
UPDATE [{table}]
SET [{column}] = CONVERT(datetime2, [{column}] AT TIME ZONE '{SourceTimeZone}' AT TIME ZONE 'UTC')
WHERE [{column}] IS NOT NULL;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in Columns)
            {
                migrationBuilder.Sql($@"
UPDATE [{table}]
SET [{column}] = CONVERT(datetime2, [{column}] AT TIME ZONE 'UTC' AT TIME ZONE '{SourceTimeZone}')
WHERE [{column}] IS NOT NULL;");
            }
        }
    }
}
