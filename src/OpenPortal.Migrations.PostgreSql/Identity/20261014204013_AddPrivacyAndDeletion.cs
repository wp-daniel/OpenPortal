using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Migrations.PostgreSql.Identity
{
    /// <inheritdoc />
    public partial class AddPrivacyAndDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletionRequestedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletionScheduledAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrivacySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SelfDeletionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DeletionGraceDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacySettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrivacySettings");

            migrationBuilder.DropColumn(
                name: "DeletionRequestedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionScheduledAtUtc",
                table: "Users");
        }
    }
}
