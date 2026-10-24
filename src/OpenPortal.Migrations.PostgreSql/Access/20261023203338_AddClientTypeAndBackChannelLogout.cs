using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Migrations.PostgreSql.Access
{
    /// <inheritdoc />
    public partial class AddClientTypeAndBackChannelLogout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnouncedBackChannelLogoutUri",
                table: "Applications",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackChannelLogoutUri",
                table: "Applications",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientType",
                table: "Applications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Confidential");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncedBackChannelLogoutUri",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "BackChannelLogoutUri",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "Applications");
        }
    }
}
