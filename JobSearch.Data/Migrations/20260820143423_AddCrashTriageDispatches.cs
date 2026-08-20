using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JobSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrashTriageDispatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrashTriageDispatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SentryIssueId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ProjectSlug = table.Column<string>(type: "text", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrashTriageDispatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrashTriageDispatches_DispatchedAt",
                table: "CrashTriageDispatches",
                column: "DispatchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CrashTriageDispatches_SentryIssueId",
                table: "CrashTriageDispatches",
                column: "SentryIssueId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrashTriageDispatches");
        }
    }
}
