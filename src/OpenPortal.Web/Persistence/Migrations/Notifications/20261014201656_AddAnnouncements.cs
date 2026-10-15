using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Web.Persistence.Migrations.Notifications
{
    /// <inheritdoc />
    public partial class AddAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Audience = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StartsAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    EndsAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    Dismissible = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementTexts",
                columns: table => new
                {
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AnnouncementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementTexts", x => new { x.AnnouncementId, x.Language });
                    table.ForeignKey(
                        name: "FK_AnnouncementTexts_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementTexts");

            migrationBuilder.DropTable(
                name: "Announcements");
        }
    }
}
