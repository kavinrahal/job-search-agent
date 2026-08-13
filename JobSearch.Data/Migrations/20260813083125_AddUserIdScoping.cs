using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "RawEmails",
                type: "integer",
                nullable: false,
                defaultValue: 1); // backfill: existing single-user data belongs to the owner, User #1

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 1); // backfill: existing single-user data belongs to the owner, User #1

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "DiscoveredPostings",
                type: "integer",
                nullable: false,
                defaultValue: 1); // backfill: existing single-user data belongs to the owner, User #1

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Classifications",
                type: "integer",
                nullable: false,
                defaultValue: 1); // backfill: existing single-user data belongs to the owner, User #1

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Applications",
                type: "integer",
                nullable: false,
                defaultValue: 1); // backfill: existing single-user data belongs to the owner, User #1

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ApplicationEvents",
                type: "integer",
                nullable: false,
                defaultValue: 1); // backfill: existing single-user data belongs to the owner, User #1

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "AgentThreads",
                type: "integer",
                nullable: false,
                defaultValue: 1); // backfill: existing single-user data belongs to the owner, User #1

            migrationBuilder.CreateIndex(
                name: "IX_RawEmails_UserId",
                table: "RawEmails",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPostings_UserId",
                table: "DiscoveredPostings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Classifications_UserId",
                table: "Classifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserId",
                table: "Applications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_UserId",
                table: "ApplicationEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentThreads_UserId",
                table: "AgentThreads",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RawEmails_UserId",
                table: "RawEmails");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_DiscoveredPostings_UserId",
                table: "DiscoveredPostings");

            migrationBuilder.DropIndex(
                name: "IX_Classifications_UserId",
                table: "Classifications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_UserId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationEvents_UserId",
                table: "ApplicationEvents");

            migrationBuilder.DropIndex(
                name: "IX_AgentThreads_UserId",
                table: "AgentThreads");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "RawEmails");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "DiscoveredPostings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Classifications");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ApplicationEvents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AgentThreads");
        }
    }
}
