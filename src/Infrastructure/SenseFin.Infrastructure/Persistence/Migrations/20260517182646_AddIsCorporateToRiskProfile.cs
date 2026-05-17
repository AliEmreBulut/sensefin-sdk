using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SenseFin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsCorporateToRiskProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCorporate",
                table: "RiskProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCorporate",
                table: "RiskProfiles");
        }
    }
}
