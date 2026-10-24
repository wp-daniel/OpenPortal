using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Migrations.PostgreSql.Access
{
    /// <inheritdoc />
    public partial class AddApplicationWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedWebhookSecret",
                table: "Applications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookEvents",
                table: "Applications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "Applications",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedWebhookSecret",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "WebhookEvents",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "Applications");
        }
    }
}
