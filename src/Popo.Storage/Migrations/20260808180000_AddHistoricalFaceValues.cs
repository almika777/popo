using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260808180000_AddHistoricalFaceValues")]
public partial class AddHistoricalFaceValues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "FaceValue",
            table: "Trades",
            type: "double precision",
            nullable: false,
            defaultValue: 0d);

        migrationBuilder.AddColumn<double>(
            name: "FaceValue",
            table: "CashFlows",
            type: "double precision",
            nullable: false,
            defaultValue: 0d);

        migrationBuilder.Sql("""
            UPDATE "Trades" AS trade
            SET "FaceValue" = COALESCE((
                SELECT security."FaceValue"
                FROM "MoexBondSecurities" AS security
                WHERE security."SecId" = trade."SecId"
                  AND security."BoardId" = trade."BoardId"
            ), 0);

            UPDATE "CashFlows" AS cash_flow
            SET "FaceValue" = COALESCE(
                (
                    SELECT trade."FaceValue"
                    FROM "Trades" AS trade
                    WHERE trade."TradeId" = cash_flow."TradeId"
                ),
                (
                    SELECT security."FaceValue"
                    FROM "MoexBondSecurities" AS security
                    WHERE security."SecId" = cash_flow."SecId"
                      AND security."BoardId" = cash_flow."BoardId"
                ),
                0);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FaceValue", table: "CashFlows");
        migrationBuilder.DropColumn(name: "FaceValue", table: "Trades");
    }
}
