using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Web.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddRegistrationAndInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Users",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Created");

            migrationBuilder.AddColumn<bool>(
                name: "PendingApproval",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupIds = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationIds = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSentAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    AcceptedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    AcceptedUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RevokedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AllowedDomains = table.Column<string>(type: "TEXT", nullable: false),
                    RequireEmailConfirmation = table.Column<bool>(type: "INTEGER", nullable: false),
                    InvitationLifetimeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_CreatedAtUtc",
                table: "Invitations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_NormalizedEmail",
                table: "Invitations",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                table: "Invitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "RegistrationSettings");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PendingApproval",
                table: "Users");
        }
    }
}
