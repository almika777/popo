using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTradeTimeFromPortfolioTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TradeTime",
                table: "PortfolioTrades");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "TradeTime",
                table: "PortfolioTrades",
                type: "time without time zone",
                nullable: true);
        }
    }
}
