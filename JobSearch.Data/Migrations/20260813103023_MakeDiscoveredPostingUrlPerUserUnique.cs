using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeDiscoveredPostingUrlPerUserUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiscoveredPostings_Url",
                table: "DiscoveredPostings");

            migrationBuilder.DropIndex(
                name: "IX_DiscoveredPostings_UserId",
                table: "DiscoveredPostings");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPostings_UserId_Url",
                table: "DiscoveredPostings",
                columns: new[] { "UserId", "Url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiscoveredPostings_UserId_Url",
                table: "DiscoveredPostings");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPostings_Url",
                table: "DiscoveredPostings",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPostings_UserId",
                table: "DiscoveredPostings",
                column: "UserId");
        }
    }
}
