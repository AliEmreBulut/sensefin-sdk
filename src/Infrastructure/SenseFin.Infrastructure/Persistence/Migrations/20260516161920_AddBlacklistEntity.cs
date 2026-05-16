using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SenseFin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlacklistEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlacklistedAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IdentifierType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AddedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IncidentCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlacklistedAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlacklistedAccounts_Identifier",
                table: "BlacklistedAccounts",
                columns: new[] { "AccountIdentifier", "IdentifierType" });

            migrationBuilder.CreateIndex(
                name: "IX_BlacklistedAccounts_IsActive",
                table: "BlacklistedAccounts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BlacklistedAccounts_Reason",
                table: "BlacklistedAccounts",
                column: "Reason");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlacklistedAccounts");
        }
    }
}
