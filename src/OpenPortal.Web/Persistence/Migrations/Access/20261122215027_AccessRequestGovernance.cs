using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Web.Persistence.Migrations.Access
{
    /// <inheritdoc />
    public partial class AccessRequestGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccessRequestApproverGroupId",
                table: "Applications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Revision",
                table: "AccessReviews",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "GrantedUntilUtc",
                table: "AccessRequests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_AccessRequestApproverGroupId",
                table: "Applications",
                column: "AccessRequestApproverGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Groups_AccessRequestApproverGroupId",
                table: "Applications",
                column: "AccessRequestApproverGroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Groups_AccessRequestApproverGroupId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_AccessRequestApproverGroupId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "AccessRequestApproverGroupId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "AccessReviews");

            migrationBuilder.DropColumn(
                name: "GrantedUntilUtc",
                table: "AccessRequests");
        }
    }
}
