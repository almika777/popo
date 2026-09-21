using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyId = table.Column<string>(type: "text", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioTrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SecId = table.Column<string>(type: "text", nullable: false),
                    BoardId = table.Column<string>(type: "text", nullable: false),
                    CurrencyId = table.Column<string>(type: "text", nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    Price = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioTrades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashSnapshots_CurrencyId_SnapshotDate",
                table: "CashSnapshots",
                columns: new[] { "CurrencyId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioTrades_TradeDate_SecId_BoardId",
                table: "PortfolioTrades",
                columns: new[] { "TradeDate", "SecId", "BoardId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashSnapshots");

            migrationBuilder.DropTable(
                name: "PortfolioTrades");
        }
    }
}
