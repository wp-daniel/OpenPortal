using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Migrations.PostgreSql.Audit
{
    /// <inheritdoc />
    public partial class AddAuditOnBehalfOf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OnBehalfOfId",
                table: "AuditEntries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnBehalfOfLabel",
                table: "AuditEntries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OnBehalfOfId",
                table: "AuditEntries",
                column: "OnBehalfOfId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_OnBehalfOfId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "OnBehalfOfId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "OnBehalfOfLabel",
                table: "AuditEntries");
        }
    }
}
