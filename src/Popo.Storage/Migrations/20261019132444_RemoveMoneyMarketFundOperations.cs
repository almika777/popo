using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMoneyMarketFundOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "ArchivedMoneyMarketFunds"
                    (LIKE "MoneyMarketFunds" INCLUDING ALL);
                DO $archive$
                DECLARE
                    archive_unique_index record;
                BEGIN
                    FOR archive_unique_index IN
                        SELECT indexrelid::regclass AS "Name"
                        FROM pg_index
                        WHERE indrelid = '"ArchivedMoneyMarketFunds"'::regclass
                            AND indisunique
                            AND NOT indisprimary
                    LOOP
                        EXECUTE format('DROP INDEX %s', archive_unique_index."Name");
                    END LOOP;
                END;
                $archive$;
                INSERT INTO "ArchivedMoneyMarketFunds"
                    ("Id", "SecId", "BoardId", "Quantity", "AveragePrice")
                SELECT fund."Id", fund."SecId", fund."BoardId", fund."Quantity", fund."AveragePrice"
                FROM "MoneyMarketFunds" AS fund
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "ArchivedMoneyMarketFunds" AS archived
                    WHERE archived."SecId" = fund."SecId"
                        AND archived."BoardId" = fund."BoardId")
                ON CONFLICT ("Id") DO NOTHING;

                CREATE TABLE IF NOT EXISTS "ArchivedMoneyMarketFundOperations"
                    (LIKE "MoneyMarketFundOperations" INCLUDING ALL);
                INSERT INTO "ArchivedMoneyMarketFundOperations"
                    ("Id", "SecId", "Date", "Side", "Quantity", "Price", "Commission")
                SELECT "Id", "SecId", "Date", "Side", "Quantity", "Price", "Commission"
                FROM "MoneyMarketFundOperations"
                ON CONFLICT ("Id") DO NOTHING;
                """);

            migrationBuilder.Sql("""
                DO $migration$
                BEGIN
                    IF EXISTS (
                        WITH RECURSIVE operation_rows AS (
                            SELECT operation."SecId", operation."Date", operation."Id", operation."Side",
                                operation."Quantity", operation."Price", operation."Commission",
                                ROW_NUMBER() OVER (
                                    PARTITION BY UPPER(operation."SecId")
                                    ORDER BY operation."Date", operation."Id") AS "SequenceNumber"
                            FROM "MoneyMarketFundOperations" AS operation
                        ), positions("Id", "SecId", "SequenceNumber", "Quantity", "Cost") AS (
                            SELECT fund."Id", fund."SecId", 0::bigint, fund."Quantity",
                                fund."Quantity" * fund."AveragePrice"
                            FROM "MoneyMarketFunds" AS fund
                            UNION ALL
                            SELECT position."Id", position."SecId", operation."SequenceNumber",
                                CASE WHEN operation."Side" = 0
                                    THEN position."Quantity" + operation."Quantity"
                                    ELSE position."Quantity" - operation."Quantity"
                                END,
                                CASE WHEN operation."Side" = 0
                                    THEN position."Cost" + operation."Quantity" * operation."Price" + operation."Commission"
                                    WHEN position."Quantity" = 0 THEN position."Cost"
                                    ELSE position."Cost" - operation."Quantity" * (position."Cost" / position."Quantity")
                                END
                            FROM positions AS position
                            JOIN operation_rows AS operation
                                ON UPPER(operation."SecId") = UPPER(position."SecId")
                                AND operation."SequenceNumber" = position."SequenceNumber" + 1
                        )
                        SELECT 1 FROM positions WHERE "Quantity" < 0 LIMIT 1
                    ) THEN
                        RAISE EXCEPTION 'Money market fund operation history produces a negative position.';
                    END IF;
                END;
                $migration$;
                """);

            migrationBuilder.Sql("""
                WITH RECURSIVE operation_rows AS (
                    SELECT operation."SecId", operation."Date", operation."Id", operation."Side",
                        operation."Quantity", operation."Price", operation."Commission",
                        ROW_NUMBER() OVER (
                            PARTITION BY UPPER(operation."SecId")
                            ORDER BY operation."Date", operation."Id") AS "SequenceNumber"
                    FROM "MoneyMarketFundOperations" AS operation
                ), positions("Id", "SecId", "SequenceNumber", "Quantity", "Cost") AS (
                    SELECT fund."Id", fund."SecId", 0::bigint, fund."Quantity",
                        fund."Quantity" * fund."AveragePrice"
                    FROM "MoneyMarketFunds" AS fund
                    UNION ALL
                    SELECT position."Id", position."SecId", operation."SequenceNumber",
                        CASE WHEN operation."Side" = 0
                            THEN position."Quantity" + operation."Quantity"
                            ELSE position."Quantity" - operation."Quantity"
                        END,
                        CASE WHEN operation."Side" = 0
                            THEN position."Cost" + operation."Quantity" * operation."Price" + operation."Commission"
                            WHEN position."Quantity" = 0 THEN position."Cost"
                            ELSE position."Cost" - operation."Quantity" * (position."Cost" / position."Quantity")
                        END
                    FROM positions AS position
                    JOIN operation_rows AS operation
                        ON UPPER(operation."SecId") = UPPER(position."SecId")
                        AND operation."SequenceNumber" = position."SequenceNumber" + 1
                ), latest_positions AS (
                    SELECT DISTINCT ON ("Id") "Id", "Quantity", "Cost"
                    FROM positions
                    ORDER BY "Id", "SequenceNumber" DESC
                )
                UPDATE "MoneyMarketFunds" AS fund
                SET "Quantity" = position."Quantity",
                    "AveragePrice" = CASE WHEN position."Quantity" = 0 THEN 0
                        ELSE position."Cost" / position."Quantity" END
                FROM latest_positions AS position
                WHERE fund."Id" = position."Id";
                """);

            migrationBuilder.DropTable(
                name: "MoneyMarketFundOperations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoneyMarketFundOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Commission = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Price = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    Quantity = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    SecId = table.Column<string>(type: "text", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyMarketFundOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyMarketFundOperations_Date_SecId",
                table: "MoneyMarketFundOperations",
                columns: new[] { "Date", "SecId" });

            migrationBuilder.Sql("""
                INSERT INTO "MoneyMarketFunds" ("Id", "SecId", "BoardId", "Quantity", "AveragePrice")
                SELECT "Id", "SecId", "BoardId", "Quantity", "AveragePrice"
                FROM "ArchivedMoneyMarketFunds" AS original_fund
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "MoneyMarketFunds" AS current_fund
                    WHERE current_fund."SecId" = original_fund."SecId"
                        AND current_fund."BoardId" = original_fund."BoardId"
                        AND current_fund."Id" <> original_fund."Id")
                ON CONFLICT ("Id") DO UPDATE SET
                    "SecId" = EXCLUDED."SecId",
                    "BoardId" = EXCLUDED."BoardId",
                    "Quantity" = EXCLUDED."Quantity",
                    "AveragePrice" = EXCLUDED."AveragePrice";

                INSERT INTO "MoneyMarketFundOperations"
                    ("Id", "SecId", "Date", "Side", "Quantity", "Price", "Commission")
                SELECT operation."Id", operation."SecId", operation."Date", operation."Side",
                    operation."Quantity", operation."Price", operation."Commission"
                FROM "ArchivedMoneyMarketFundOperations" AS operation
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "ArchivedMoneyMarketFunds" AS original_fund
                    WHERE UPPER(original_fund."SecId") = UPPER(operation."SecId"))
                    OR EXISTS (
                        SELECT 1
                        FROM "ArchivedMoneyMarketFunds" AS original_fund
                        JOIN "MoneyMarketFunds" AS current_fund
                            ON current_fund."Id" = original_fund."Id"
                        WHERE UPPER(original_fund."SecId") = UPPER(operation."SecId"))
                ON CONFLICT ("Id") DO NOTHING;
                """);
        }
    }
}
