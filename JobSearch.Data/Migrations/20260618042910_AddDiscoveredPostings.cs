using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JobSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscoveredPostings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscoveredPostings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: false),
                    Recommendation = table.Column<string>(type: "text", nullable: true),
                    EvaluationJson = table.Column<string>(type: "text", nullable: true),
                    DisqualifierHit = table.Column<string>(type: "text", nullable: true),
                    DiscoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationSent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveredPostings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPostings_DiscoveredAt",
                table: "DiscoveredPostings",
                column: "DiscoveredAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPostings_Recommendation",
                table: "DiscoveredPostings",
                column: "Recommendation");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPostings_Url",
                table: "DiscoveredPostings",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveredPostings");
        }
    }
}
