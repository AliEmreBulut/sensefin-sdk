using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SenseFin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileFraudMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiverIban",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TremorScore",
                table: "Transactions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TypingScore",
                table: "Transactions",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiverIban",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TremorScore",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TypingScore",
                table: "Transactions");
        }
    }
}
