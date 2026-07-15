using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WhatsAppMessageId",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppSentAt",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppMessageId",
                table: "DiscoveredPostings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppNotificationSent",
                table: "DiscoveredPostings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_WhatsAppMessageId",
                table: "Notifications",
                column: "WhatsAppMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_WhatsAppSentAt",
                table: "Notifications",
                column: "WhatsAppSentAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPostings_WhatsAppMessageId",
                table: "DiscoveredPostings",
                column: "WhatsAppMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_WhatsAppMessageId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_WhatsAppSentAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_DiscoveredPostings_WhatsAppMessageId",
                table: "DiscoveredPostings");

            migrationBuilder.DropColumn(
                name: "WhatsAppMessageId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "WhatsAppSentAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "WhatsAppMessageId",
                table: "DiscoveredPostings");

            migrationBuilder.DropColumn(
                name: "WhatsAppNotificationSent",
                table: "DiscoveredPostings");
        }
    }
}
