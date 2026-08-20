using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixRawEmailAndClassificationMessageIdUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RawEmails_MessageId",
                table: "RawEmails");

            migrationBuilder.DropIndex(
                name: "IX_RawEmails_UserId",
                table: "RawEmails");

            migrationBuilder.DropIndex(
                name: "IX_Classifications_MessageId",
                table: "Classifications");

            migrationBuilder.DropIndex(
                name: "IX_Classifications_UserId",
                table: "Classifications");

            migrationBuilder.CreateIndex(
                name: "IX_RawEmails_UserId_MessageId",
                table: "RawEmails",
                columns: new[] { "UserId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classifications_UserId_MessageId",
                table: "Classifications",
                columns: new[] { "UserId", "MessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RawEmails_UserId_MessageId",
                table: "RawEmails");

            migrationBuilder.DropIndex(
                name: "IX_Classifications_UserId_MessageId",
                table: "Classifications");

            migrationBuilder.CreateIndex(
                name: "IX_RawEmails_MessageId",
                table: "RawEmails",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RawEmails_UserId",
                table: "RawEmails",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Classifications_MessageId",
                table: "Classifications",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classifications_UserId",
                table: "Classifications",
                column: "UserId");
        }
    }
}
