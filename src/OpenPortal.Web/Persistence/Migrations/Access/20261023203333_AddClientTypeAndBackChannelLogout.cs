using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Web.Persistence.Migrations.Access
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
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackChannelLogoutUri",
                table: "Applications",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientType",
                table: "Applications",
                type: "TEXT",
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
