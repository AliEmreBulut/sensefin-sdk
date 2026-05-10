using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SenseFin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CurrentRiskLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AverageRiskScore = table.Column<double>(type: "double precision", nullable: false),
                    TotalEvaluations = table.Column<int>(type: "integer", nullable: false),
                    LastEvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SenderDeviceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SenderIpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    SenderAccountId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReceiverAccountId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    LocationCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocationCity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MerchantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskScoreEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskScoreEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskScoreEntries_RiskProfiles_RiskProfileId",
                        column: x => x.RiskProfileId,
                        principalTable: "RiskProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskProfiles_AccountId",
                table: "RiskProfiles",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskScoreEntries_RiskProfileId",
                table: "RiskScoreEntries",
                column: "RiskProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskScoreEntries_TransactionId",
                table: "RiskScoreEntries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReceiverAccountId",
                table: "Transactions",
                column: "ReceiverAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SenderAccountId",
                table: "Transactions",
                column: "SenderAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate",
                table: "Transactions",
                column: "TransactionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskScoreEntries");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "RiskProfiles");
        }
    }
}
