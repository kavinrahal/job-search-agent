using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentThreadClientRequestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientRequestId",
                table: "AgentThreads",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentThreads_UserId_ClientRequestId",
                table: "AgentThreads",
                columns: new[] { "UserId", "ClientRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentThreads_UserId_ClientRequestId",
                table: "AgentThreads");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "AgentThreads");
        }
    }
}
