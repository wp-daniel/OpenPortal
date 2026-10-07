using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Web.Persistence.Migrations.Access
{
    /// <inheritdoc />
    public partial class AddPageGroupGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageGroupGrants",
                columns: table => new
                {
                    PageKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GrantedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageGroupGrants", x => new { x.PageKey, x.GroupId });
                    table.ForeignKey(
                        name: "FK_PageGroupGrants_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageGroupGrants_GroupId",
                table: "PageGroupGrants",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageGroupGrants");
        }
    }
}
