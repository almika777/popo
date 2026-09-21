using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260808160000_MergeTradeFeesIntoCashFlows")]
public partial class MergeTradeFeesIntoCashFlows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "CashFlows" AS trade_flow
            SET "Amount" = trade_flow."Amount" + fee_flow."Amount",
                "AmountRub" = trade_flow."AmountRub" + fee_flow."AmountRub"
            FROM "CashFlows" AS fee_flow
            WHERE trade_flow."TradeId" IS NOT NULL
              AND trade_flow."Type" IN ('Buy', 'Sell')
              AND fee_flow."TradeId" = trade_flow."TradeId"
              AND fee_flow."Type" = 'Fee';

            DELETE FROM "CashFlows"
            WHERE "Type" = 'Fee' AND "TradeId" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
