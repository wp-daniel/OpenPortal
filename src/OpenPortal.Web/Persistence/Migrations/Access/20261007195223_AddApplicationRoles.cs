using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Web.Persistence.Migrations.Access
{
    /// <inheritdoc />
    public partial class AddApplicationRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Roles",
                table: "ApplicationUserGrants",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GroupClaims",
                table: "Applications",
                type: "TEXT",
                maxLength: 16,
                nullable: false,

                // Existing applications keep sending no groups, as before: GroupClaimMode.None.
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "Roles",
                table: "ApplicationGroupGrants",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ApplicationRoles",
                columns: table => new
                {
                    ApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRoles", x => new { x.ApplicationId, x.Key });
                    table.ForeignKey(
                        name: "FK_ApplicationRoles_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationRoles");

            migrationBuilder.DropColumn(
                name: "Roles",
                table: "ApplicationUserGrants");

            migrationBuilder.DropColumn(
                name: "GroupClaims",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Roles",
                table: "ApplicationGroupGrants");
        }
    }
}
