using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Hldfh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "InvestmentStrategySettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CommissionRate", "MaxIssuerShare", "MaxMedianTurnoverShare", "MaximumYtm" },
                values: new object[] { 0.04, 10.0, 10.0, 30.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "InvestmentStrategySettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CommissionRate", "MaxIssuerShare", "MaxMedianTurnoverShare", "MaximumYtm" },
                values: new object[] { 0.00040000000000000002, 0.10000000000000001, 0.10000000000000001, 0.29999999999999999 });
        }
    }
}
