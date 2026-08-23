using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscoveredPostingText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostingText",
                table: "DiscoveredPostings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PostingText",
                table: "DiscoveredPostings");
        }
    }
}
