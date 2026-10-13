using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Api.Migrations
{
    /// <summary>
    /// Data-only migration: SystemUsers.Role may now only be SuperAdmin, Admin or Viewer.
    /// Role checks are case-sensitive, so exact casing is rewritten too.
    /// Any other value (e.g. "IT Support", "Manager") becomes Viewer.
    /// SupportIT (added in Phase 2) is kept, so this never demotes it on a database that already has it.
    /// </summary>
    public partial class NormalizeSystemUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE SystemUsers
SET Role = CASE
    WHEN LTRIM(RTRIM(Role)) = 'SuperAdmin' THEN 'SuperAdmin'
    WHEN LTRIM(RTRIM(Role)) = 'Admin' THEN 'Admin'
    WHEN LTRIM(RTRIM(Role)) = 'SupportIT' THEN 'SupportIT'
    ELSE 'Viewer'
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Original role names are not recoverable.
        }
    }
}
