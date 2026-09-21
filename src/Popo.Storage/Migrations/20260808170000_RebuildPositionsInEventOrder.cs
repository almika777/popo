using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260808170000_RebuildPositionsInEventOrder")]
public partial class RebuildPositionsInEventOrder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Trades_SecId_BoardId_TradeDate_TradeSequence\";");
        migrationBuilder.Sql("""
            WITH ordered AS (
                SELECT "TradeId",
                       ROW_NUMBER() OVER (
                           PARTITION BY "SecId", "TradeDate"
                           ORDER BY "TradeSequence", "BoardId", "TradeId") AS sequence
                FROM "Trades"
            )
            UPDATE "Trades" AS trade
            SET "TradeSequence" = ordered.sequence
            FROM ordered
            WHERE trade."TradeId" = ordered."TradeId";
            """);
        migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Trades_SecId_TradeDate_TradeSequence\" ON \"Trades\" (\"SecId\", \"TradeDate\", \"TradeSequence\");");
        migrationBuilder.Sql("DELETE FROM \"Positions\" WHERE NOT EXISTS (SELECT 1 FROM \"Trades\" WHERE \"Trades\".\"SecId\" = \"Positions\".\"SecId\");");
        migrationBuilder.Sql("""
            DO $$
            DECLARE
                security_record RECORD;
                event_record RECORD;
                quantity bigint;
                cost double precision;
                realized_profit double precision;
                current_face_value double precision;
                average_price double precision;
                currency text;
                name text;
            BEGIN
                FOR security_record IN
                    SELECT DISTINCT "SecId" FROM "Trades"
                LOOP
                    quantity := 0;
                    cost := 0;
                    realized_profit := 0;
                    current_face_value := COALESCE((
                        SELECT "FaceValue"
                        FROM "MoexBondSecurities"
                        WHERE "SecId" = security_record."SecId"
                        ORDER BY "BoardId"
                        LIMIT 1), 0);

                    FOR event_record IN
                        SELECT event_type, event_quantity, event_price, event_date
                        FROM (
                            SELECT "Type" AS event_type,
                                   "Quantity" AS event_quantity,
                                   "Price" AS event_price,
                                   "TradeDate" AS event_date,
                                   0 AS event_priority,
                                   "TradeSequence" AS event_sequence,
                                   "TradeId" AS event_id
                            FROM "Trades"
                            WHERE "SecId" = security_record."SecId"

                            UNION ALL

                            SELECT "Type" AS event_type,
                                   0::bigint AS event_quantity,
                                   0::double precision AS event_price,
                                   "Date" AS event_date,
                                   1 AS event_priority,
                                   2147483647 AS event_sequence,
                                   "Id" AS event_id
                            FROM "CashFlows"
                            WHERE "SecId" = security_record."SecId"
                              AND "Type" IN ('Amortization', 'Redemption')
                        ) AS events
                        ORDER BY event_date, event_priority, event_sequence, event_id
                    LOOP
                        IF event_record.event_type = 'Buy' THEN
                            quantity := quantity + event_record.event_quantity;
                            cost := cost + event_record.event_quantity * event_record.event_price;
                        ELSIF event_record.event_type = 'Sell' THEN
                            IF event_record.event_quantity > quantity THEN
                                RAISE EXCEPTION 'Cannot rebuild position %: sell quantity exceeds available quantity.', security_record."SecId";
                            END IF;

                            average_price := cost / quantity;
                            realized_profit := realized_profit
                                + event_record.event_quantity * (event_record.event_price - average_price);
                            quantity := quantity - event_record.event_quantity;
                            cost := cost - event_record.event_quantity * average_price;
                        ELSIF event_record.event_type = 'Redemption' THEN
                            quantity := 0;
                            cost := 0;
                        END IF;
                    END LOOP;

                    SELECT COALESCE("CurrencyId", ''), "ShortName"
                    INTO currency, name
                    FROM "MoexBondSecurities"
                    WHERE "SecId" = security_record."SecId"
                    ORDER BY "BoardId"
                    LIMIT 1;

                    INSERT INTO "Positions" (
                        "SecId", "Currency", "Name", "Quantity", "AveragePrice",
                        "RealizedProfit", "CurrentFaceValue")
                    VALUES (
                        security_record."SecId", COALESCE(currency, ''), COALESCE(name, ''),
                        quantity, CASE WHEN quantity = 0 THEN 0 ELSE cost / quantity END,
                        realized_profit, CASE WHEN quantity = 0 THEN 0 ELSE current_face_value END)
                    ON CONFLICT ("SecId") DO UPDATE SET
                        "Currency" = EXCLUDED."Currency",
                        "Name" = EXCLUDED."Name",
                        "Quantity" = EXCLUDED."Quantity",
                        "AveragePrice" = EXCLUDED."AveragePrice",
                        "RealizedProfit" = EXCLUDED."RealizedProfit",
                        "CurrentFaceValue" = EXCLUDED."CurrentFaceValue";
                END LOOP;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Trades_SecId_TradeDate_TradeSequence\";");
        migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Trades_SecId_BoardId_TradeDate_TradeSequence\" ON \"Trades\" (\"SecId\", \"BoardId\", \"TradeDate\", \"TradeSequence\");");
    }
}
