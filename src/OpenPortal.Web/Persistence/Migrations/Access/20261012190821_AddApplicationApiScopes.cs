using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Web.Persistence.Migrations.Access
{
    /// <inheritdoc />
    public partial class AddApplicationApiScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiScopes",
                table: "Applications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiScopes",
                table: "Applications");
        }
    }
}
