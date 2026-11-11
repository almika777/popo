using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Sdhg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "PortfolioTrades",
                type: "integer",
                precision: 20,
                scale: 6,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 20,
                oldScale: 6);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "Quantity",
                table: "PortfolioTrades",
                type: "double precision",
                precision: 20,
                scale: 6,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldPrecision: 20,
                oldScale: 6);
        }
    }
}
