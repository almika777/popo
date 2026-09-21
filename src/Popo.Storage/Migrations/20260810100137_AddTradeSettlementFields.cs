using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeSettlementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AccruedInterest",
                table: "PortfolioTrades",
                type: "double precision",
                precision: 20,
                scale: 6,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Commission",
                table: "PortfolioTrades",
                type: "double precision",
                precision: 20,
                scale: 6,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "FaceValue",
                table: "PortfolioTrades",
                type: "double precision",
                precision: 20,
                scale: 6,
                nullable: false,
                defaultValue: 100.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccruedInterest",
                table: "PortfolioTrades");

            migrationBuilder.DropColumn(
                name: "Commission",
                table: "PortfolioTrades");

            migrationBuilder.DropColumn(
                name: "FaceValue",
                table: "PortfolioTrades");
        }
    }
}
