using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class SetPortfolioTradeFaceValueDefaultTo1000 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "PortfolioTrades"
                SET "FaceValue" = 1000.0
                WHERE "FaceValue" = 100.0;
                """);

            migrationBuilder.AlterColumn<double>(
                name: "FaceValue",
                table: "PortfolioTrades",
                type: "double precision",
                precision: 20,
                scale: 6,
                nullable: false,
                defaultValue: 1000.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 20,
                oldScale: 6);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Исправленные значения оставляем: после нормализации их нельзя отличить от исходных номиналов 1000.
            migrationBuilder.AlterColumn<double>(
                name: "FaceValue",
                table: "PortfolioTrades",
                type: "double precision",
                precision: 20,
                scale: 6,
                nullable: false,
                defaultValue: 100.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 20,
                oldScale: 6,
                oldDefaultValue: 1000.0);
        }
    }
}
