using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Migrations.PostgreSql.Access
{
    /// <inheritdoc />
    public partial class AddNestedGroupsAndExpiringAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                table: "GroupMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                table: "ApplicationUserGrants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                table: "ApplicationGroupGrants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GroupNestings",
                columns: table => new
                {
                    ParentGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupNestings", x => new { x.ParentGroupId, x.ChildGroupId });
                    table.ForeignKey(
                        name: "FK_GroupNestings_Groups_ChildGroupId",
                        column: x => x.ChildGroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupNestings_Groups_ParentGroupId",
                        column: x => x.ParentGroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_ExpiresAtUtc",
                table: "GroupMembers",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GroupNestings_ChildGroupId",
                table: "GroupNestings",
                column: "ChildGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupNestings");

            migrationBuilder.DropIndex(
                name: "IX_GroupMembers_ExpiresAtUtc",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "ApplicationUserGrants");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "ApplicationGroupGrants");
        }
    }
}
