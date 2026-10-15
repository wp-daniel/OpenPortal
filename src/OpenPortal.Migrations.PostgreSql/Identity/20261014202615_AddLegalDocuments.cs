using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenPortal.Migrations.PostgreSql.Identity
{
    /// <inheritdoc />
    public partial class AddLegalDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequiresAcceptance = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalAcceptances",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalAcceptances", x => new { x.UserId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_LegalAcceptances_LegalDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "LegalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalAcceptances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LegalDocumentTexts",
                columns: table => new
                {
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocumentTexts", x => new { x.DocumentId, x.Language });
                    table.ForeignKey(
                        name: "FK_LegalDocumentTexts_LegalDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "LegalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalAcceptances_DocumentId",
                table: "LegalAcceptances",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Kind_Version",
                table: "LegalDocuments",
                columns: new[] { "Kind", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalAcceptances");

            migrationBuilder.DropTable(
                name: "LegalDocumentTexts");

            migrationBuilder.DropTable(
                name: "LegalDocuments");
        }
    }
}
