using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260808140000_RebuildPortfolioCashFlows")]
public partial class RebuildPortfolioCashFlows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>("FxRate", "CashFlows", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("FxRateDate", "CashFlows", type: "date", nullable: true);
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM "Trades" AS trade
                    LEFT JOIN "MoexBondSecurities" AS security
                      ON security."SecId" = trade."SecId" AND security."BoardId" = trade."BoardId"
                    LEFT JOIN LATERAL (
                        SELECT rate."UnitRate"
                        FROM "CurrencyRates" AS rate
                        WHERE rate."CurrencyCode" = security."CurrencyId"
                          AND rate."RateDate" <= trade."TradeDate"
                        ORDER BY rate."RateDate" DESC
                        LIMIT 1
                    ) AS fx ON TRUE
                    WHERE security."SecId" IS NULL
                       OR (security."CurrencyId" <> 'RUB' AND fx."UnitRate" IS NULL)
                ) THEN
                    RAISE EXCEPTION 'Cannot rebuild portfolio cash flows: security currency or historical FX rate is missing.';
                END IF;
            END $$;
            """);
        migrationBuilder.Sql("DELETE FROM \"CashFlows\" WHERE \"TradeId\" IS NOT NULL;");
        migrationBuilder.Sql("""
            WITH trade_values AS (
                SELECT trade."TradeId", trade."SecId", trade."BoardId", trade."Type", trade."TradeDate",
                       trade."Quantity" * (trade."Price" + trade."AccruedInterest") AS gross_amount,
                       trade."Commission",
                       security."CurrencyId" AS currency,
                       CASE WHEN security."CurrencyId" = 'RUB'
                            THEN 1 ELSE fx."UnitRate" END AS fx_rate,
                       CASE WHEN security."CurrencyId" = 'RUB'
                            THEN trade."TradeDate" ELSE fx."RateDate" END AS fx_date
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
            )
            INSERT INTO "CashFlows" ("Id", "SecId", "BoardId", "Type", "Date", "Amount", "Currency", "AmountRub", "FxRate", "FxRateDate", "Commission", "TradeId")
            SELECT gen_random_uuid(), "SecId", "BoardId",
                   CASE WHEN "Type" = 'Buy' THEN 'Buy' ELSE 'Sell' END,
                   "TradeDate",
                   CASE WHEN "Type" = 'Buy' THEN -"gross_amount" - "gross_amount" * "Commission" / 100
                        ELSE "gross_amount" - "gross_amount" * "Commission" / 100 END,
                   "currency",
                   CASE WHEN "Type" = 'Buy' THEN (-"gross_amount" - "gross_amount" * "Commission" / 100) * "fx_rate"
                        ELSE ("gross_amount" - "gross_amount" * "Commission" / 100) * "fx_rate" END,
                   "fx_rate", "fx_date", "gross_amount" * "Commission" / 100, "TradeId"
            FROM trade_values;
            """);
        migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_CashFlows_TradeId_Type\" ON \"CashFlows\" (\"TradeId\", \"Type\") WHERE \"TradeId\" IS NOT NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_CashFlows_TradeId_Type\";");
        migrationBuilder.DropColumn("FxRate", "CashFlows");
        migrationBuilder.DropColumn("FxRateDate", "CashFlows");
    }
}
