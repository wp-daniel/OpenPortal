using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Migrations.PostgreSql.Identity
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
                    Key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProtectedClientSecret = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Tenant = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SignUp = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AllowedDomains = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
