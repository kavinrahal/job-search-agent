using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClaudeUsageLogSkillVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SkillVersion",
                table: "ClaudeUsageLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkillVersion",
                table: "ClaudeUsageLogs");
        }
    }
}
