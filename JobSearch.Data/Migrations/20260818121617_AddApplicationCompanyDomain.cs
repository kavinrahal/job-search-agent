using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobSearch.Data.Migrations
{
    // Also adds Users.GmailTrackingMode — both columns were added to the model together
    // before this was generated, so EF folded them into one migration.
    /// <inheritdoc />
    public partial class AddApplicationCompanyDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GmailTrackingMode",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyDomain",
                table: "Applications",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GmailTrackingMode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompanyDomain",
                table: "Applications");
        }
    }
}
