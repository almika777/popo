using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Popo.Storage;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260808100000_RekeyPositionsBySecId")]
public partial class RekeyPositionsBySecId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Positions");

        migrationBuilder.CreateTable(
            name: "Positions",
            columns: table => new
            {
                SecId = table.Column<string>(type: "text", nullable: false),
                Currency = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Quantity = table.Column<long>(type: "bigint", nullable: false),
                AveragePrice = table.Column<double>(type: "double precision", nullable: false),
                RealizedProfit = table.Column<double>(type: "double precision", nullable: false),
                CurrentFaceValue = table.Column<double>(type: "double precision", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Positions", x => x.SecId));

        migrationBuilder.CreateIndex(
            name: "IX_Positions_Quantity",
            table: "Positions",
            column: "Quantity");

        migrationBuilder.Sql("""
            DO $$
            DECLARE
                security_record RECORD;
                event_record RECORD;
                quantity bigint;
                cost double precision;
                realized_profit double precision;
                current_face_value double precision;
                initial_face_value double precision;
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
                    current_face_value := 0;
                    initial_face_value := (
                        SELECT "FaceValue"
                        FROM "MoexBondSecurities"
                        WHERE "SecId" = security_record."SecId"
                        ORDER BY "BoardId"
                        LIMIT 1);
                    initial_face_value := COALESCE(
                        initial_face_value,
                        (SELECT COALESCE("InitialFaceValue", "FaceValue")
                         FROM "MoexBonds"
                         WHERE "SecId" = security_record."SecId"
                         LIMIT 1), 0);

                    FOR event_record IN
                        SELECT "Type" AS event_type,
                               "Quantity" AS event_quantity,
                               "Price" AS event_price
                        FROM "Trades"
                        WHERE "SecId" = security_record."SecId"
                        ORDER BY "TradeDate", "TradeSequence", "TradeId"
                    LOOP
                        IF event_record.event_type = 'Buy' THEN
                            quantity := quantity + event_record.event_quantity;
                            cost := cost + event_record.event_quantity * event_record.event_price;
                            current_face_value := CASE
                                WHEN current_face_value > 0 THEN current_face_value
                                ELSE initial_face_value
                            END;
                        ELSIF event_record.event_type = 'Sell' THEN
                            IF quantity > 0 THEN
                                average_price := cost / quantity;
                                realized_profit := realized_profit
                                    + event_record.event_quantity * (event_record.event_price - average_price);
                                quantity := quantity - event_record.event_quantity;
                                cost := cost - event_record.event_quantity * average_price;
                                IF quantity = 0 THEN
                                    current_face_value := 0;
                                END IF;
                            END IF;
                        END IF;
                    END LOOP;

                    FOR event_record IN
                        SELECT "Type" AS event_type
                        FROM "CashFlows"
                        WHERE "SecId" = security_record."SecId"
                          AND "Type" IN ('Amortization', 'Redemption')
                        ORDER BY "Date", "Id"
                    LOOP
                        IF event_record.event_type = 'Redemption' THEN
                            quantity := 0;
                            cost := 0;
                            current_face_value := 0;
                        ELSIF quantity > 0 THEN
                            current_face_value := initial_face_value;
                        END IF;
                    END LOOP;

                    SELECT COALESCE(NULLIF("FaceUnit", ''), "CurrencyId", ''), "ShortName"
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
                        realized_profit, CASE WHEN quantity = 0 THEN 0 ELSE current_face_value END);
                END LOOP;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Positions");

        migrationBuilder.CreateTable(
            name: "Positions",
            columns: table => new
            {
                SecId = table.Column<string>(type: "text", nullable: false),
                BoardId = table.Column<string>(type: "text", nullable: false),
                Currency = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Quantity = table.Column<long>(type: "bigint", nullable: false),
                AveragePrice = table.Column<double>(type: "double precision", nullable: false),
                RealizedProfit = table.Column<double>(type: "double precision", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Positions", x => new { x.SecId, x.BoardId }));

        migrationBuilder.CreateIndex(
            name: "IX_Positions_Quantity",
            table: "Positions",
            column: "Quantity");
    }
}
