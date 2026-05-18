using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SenseFin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSenderIban : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReceiverIban",
                table: "Transactions",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderIban",
                table: "Transactions",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderIban",
                table: "Transactions");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverIban",
                table: "Transactions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(34)",
                oldMaxLength: 34,
                oldNullable: true);
        }
    }
}
