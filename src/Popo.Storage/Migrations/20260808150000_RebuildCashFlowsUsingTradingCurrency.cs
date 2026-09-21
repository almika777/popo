using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260808150000_RebuildCashFlowsUsingTradingCurrency")]
public partial class RebuildCashFlowsUsingTradingCurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM "Trades" AS trade
                    JOIN "MoexBondSecurities" AS security
                      ON security."SecId" = trade."SecId" AND security."BoardId" = trade."BoardId"
                    LEFT JOIN LATERAL (
                        SELECT rate."UnitRate"
                        FROM "CurrencyRates" AS rate
                        WHERE rate."CurrencyCode" = security."CurrencyId"
                          AND rate."RateDate" <= trade."TradeDate"
                        ORDER BY rate."RateDate" DESC
                        LIMIT 1
                    ) AS fx ON TRUE
                    WHERE security."CurrencyId" IS NULL
                       OR (security."CurrencyId" <> 'RUB' AND fx."UnitRate" IS NULL)
                ) THEN
                    RAISE EXCEPTION 'Cannot rebuild portfolio cash flows: trading currency or historical FX rate is missing.';
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            UPDATE "CashFlows" AS cash_flow
            SET "Currency" = security."CurrencyId",
                "FxRate" = CASE WHEN security."CurrencyId" = 'RUB' THEN 1 ELSE fx."UnitRate" END,
                "FxRateDate" = CASE WHEN security."CurrencyId" = 'RUB' THEN trade."TradeDate" ELSE fx."RateDate" END,
                "AmountRub" = cash_flow."Amount" * CASE WHEN security."CurrencyId" = 'RUB' THEN 1 ELSE fx."UnitRate" END
            FROM "Trades" AS trade
            JOIN "MoexBondSecurities" AS security
              ON security."SecId" = trade."SecId" AND security."BoardId" = trade."BoardId"
            LEFT JOIN LATERAL (
                SELECT rate."UnitRate", rate."RateDate"
                FROM "CurrencyRates" AS rate
                WHERE rate."CurrencyCode" = security."CurrencyId"
                  AND rate."RateDate" <= trade."TradeDate"
                ORDER BY rate."RateDate" DESC
                LIMIT 1
            ) AS fx ON TRUE
            WHERE cash_flow."TradeId" = trade."TradeId";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
