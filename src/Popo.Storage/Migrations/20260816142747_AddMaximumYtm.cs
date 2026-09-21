using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddMaximumYtm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MaximumYtm",
                table: "InvestmentStrategySettings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.UpdateData(
                table: "InvestmentStrategySettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "MaximumYtm",
                value: 0.29999999999999999);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumYtm",
                table: "InvestmentStrategySettings");
        }
    }
}
