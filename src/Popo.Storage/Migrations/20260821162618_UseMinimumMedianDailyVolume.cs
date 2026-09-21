using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class UseMinimumMedianDailyVolume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaxMedianTurnoverShare",
                table: "InvestmentStrategySettings",
                newName: "MinimumMedianDailyVolume");

            migrationBuilder.UpdateData(
                table: "InvestmentStrategySettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "MinimumMedianDailyVolume",
                value: 1000.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MinimumMedianDailyVolume",
                table: "InvestmentStrategySettings",
                newName: "MaxMedianTurnoverShare");

            migrationBuilder.UpdateData(
                table: "InvestmentStrategySettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "MaxMedianTurnoverShare",
                value: 10.0);
        }
    }
}
