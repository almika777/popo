using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationCurrencyFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyId",
                table: "InvestmentStrategySettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaceUnit",
                table: "InvestmentStrategySettings",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "InvestmentStrategySettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CurrencyId", "FaceUnit" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "InvestmentStrategySettings");

            migrationBuilder.DropColumn(
                name: "FaceUnit",
                table: "InvestmentStrategySettings");
        }
    }
}
