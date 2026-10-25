using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Web.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddExternalLoginProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalLoginProviders",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ProtectedClientSecret = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Tenant = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SignUp = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AllowedDomains = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLoginProviders", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalLoginProviders");
        }
    }
}
