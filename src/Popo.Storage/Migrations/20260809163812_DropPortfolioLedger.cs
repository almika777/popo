using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class DropPortfolioLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashFlows");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Trades");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashFlows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    AmountRub = table.Column<double>(type: "double precision", nullable: false),
                    BoardId = table.Column<string>(type: "text", nullable: false),
                    Commission = table.Column<double>(type: "double precision", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    FaceValue = table.Column<double>(type: "double precision", nullable: false),
                    FxRate = table.Column<double>(type: "double precision", nullable: true),
                    FxRateDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SecId = table.Column<string>(type: "text", nullable: false),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    SecId = table.Column<string>(type: "text", nullable: false),
                    AveragePrice = table.Column<double>(type: "double precision", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CurrentFaceValue = table.Column<double>(type: "double precision", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    RealizedProfit = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.SecId);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    TradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccruedInterest = table.Column<double>(type: "double precision", nullable: false),
                    BoardId = table.Column<string>(type: "text", nullable: false),
                    Commission = table.Column<double>(type: "double precision", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ExternalTradeId = table.Column<string>(type: "text", nullable: true),
                    FaceValue = table.Column<double>(type: "double precision", nullable: false),
                    Price = table.Column<double>(type: "double precision", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    SecId = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: true),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TradeSequence = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.TradeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlows_Date",
                table: "CashFlows",
                column: "Date",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CashFlows_TradeId",
                table: "CashFlows",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlows_TradeId_Type",
                table: "CashFlows",
                columns: new[] { "TradeId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Quantity",
                table: "Positions",
                column: "Quantity");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_SecId_TradeDate_TradeSequence",
                table: "Trades",
                columns: new[] { "SecId", "TradeDate", "TradeSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_Source_ExternalTradeId",
                table: "Trades",
                columns: new[] { "Source", "ExternalTradeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_TradeDate",
                table: "Trades",
                column: "TradeDate",
                descending: new bool[0]);
        }
    }
}
