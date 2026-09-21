using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Popo.Storage;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260808120000_RebuildPositionsFromCashFlows")]
public partial class RebuildPositionsFromCashFlows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Positions" AS position
            SET "Quantity" = CASE
                    WHEN (
                        SELECT "Type"
                        FROM "CashFlows"
                        WHERE "SecId" = position."SecId"
                          AND "Type" IN ('Amortization', 'Redemption')
                        ORDER BY "Date" DESC, "Id" DESC
                        LIMIT 1) = 'Redemption'
                    THEN 0
                    ELSE position."Quantity"
                END,
                "CurrentFaceValue" = CASE
                    WHEN (
                        SELECT "Type"
                        FROM "CashFlows"
                        WHERE "SecId" = position."SecId"
                          AND "Type" IN ('Amortization', 'Redemption')
                        ORDER BY "Date" DESC, "Id" DESC
                        LIMIT 1) = 'Redemption'
                    THEN 0
                    ELSE COALESCE((
                        SELECT "FaceValue"
                        FROM "MoexBondSecurities"
                        WHERE "SecId" = position."SecId"
                        ORDER BY "BoardId"
                        LIMIT 1), 0)
                END
            WHERE EXISTS (
                SELECT 1
                FROM "CashFlows"
                WHERE "SecId" = position."SecId"
                  AND "Type" IN ('Amortization', 'Redemption'));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
