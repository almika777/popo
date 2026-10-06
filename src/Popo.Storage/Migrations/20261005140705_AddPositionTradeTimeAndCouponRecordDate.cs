using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionTradeTimeAndCouponRecordDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "TradeTime",
                table: "PortfolioTrades",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RecordDate",
                table: "MoexCouponsEntities",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TradeTime",
                table: "PortfolioTrades");

            migrationBuilder.DropColumn(
                name: "RecordDate",
                table: "MoexCouponsEntities");
        }
    }
}
