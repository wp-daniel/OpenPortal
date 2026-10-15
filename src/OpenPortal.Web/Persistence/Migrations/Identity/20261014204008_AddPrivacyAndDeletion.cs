using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Web.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddPrivacyAndDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DeletionRequestedAtUtc",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeletionScheduledAtUtc",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrivacySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    SelfDeletionEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletionGraceDays = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
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
