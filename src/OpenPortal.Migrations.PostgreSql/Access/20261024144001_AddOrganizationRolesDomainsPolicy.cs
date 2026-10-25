using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Migrations.PostgreSql.Access
{
    /// <inheritdoc />
    public partial class AddOrganizationRolesDomainsPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireMultiFactor",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OrganizationDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VerificationToken = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifiedBy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AutoJoin = table.Column<bool>(type: "boolean", nullable: false),
                    AutoJoinRole = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationDomains_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMemberRoles",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMemberRoles", x => new { x.OrganizationId, x.UserId, x.ApplicationId, x.Role });
                    table.ForeignKey(
                        name: "FK_OrganizationMemberRoles_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationMemberRoles_OrganizationMembers_OrganizationId_~",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationDomains_Domain",
                table: "OrganizationDomains",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationDomains_OrganizationId",
                table: "OrganizationDomains",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberRoles_ApplicationId",
                table: "OrganizationMemberRoles",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberRoles_UserId_ApplicationId",
                table: "OrganizationMemberRoles",
                columns: new[] { "UserId", "ApplicationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationDomains");

            migrationBuilder.DropTable(
                name: "OrganizationMemberRoles");

            migrationBuilder.DropColumn(
                name: "RequireMultiFactor",
                table: "Organizations");
        }
    }
}
