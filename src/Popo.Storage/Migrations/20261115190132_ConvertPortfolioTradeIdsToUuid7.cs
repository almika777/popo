using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPortfolioTradeIdsToUuid7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "PortfolioTrades"
                        WHERE substring("Id"::text FROM 15 FOR 1) <> '7'
                          AND "TradeDate" < DATE '1970-01-01'
                    ) THEN
                        RAISE EXCEPTION 'UUIDv7 не поддерживает даты сделок до 1970-01-01';
                    END IF;
                END $$;

                WITH new_ids AS MATERIALIZED (
                    SELECT "Id" AS old_id,
                           lpad(to_hex((extract(epoch FROM ("TradeDate"::timestamp AT TIME ZONE 'UTC')) * 1000)::bigint), 12, '0') AS timestamp_hex,
                           replace(gen_random_uuid()::text, '-', '') AS random_hex
                    FROM "PortfolioTrades"
                    WHERE substring("Id"::text FROM 15 FOR 1) <> '7'
                )
                UPDATE "PortfolioTrades" AS trade
                SET "Id" = (new_ids.timestamp_hex || '7' || substring(new_ids.random_hex FROM 14))::uuid
                FROM new_ids
                WHERE trade."Id" = new_ids.old_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Исходные идентификаторы сделок не сохраняются; автоматический откат невозможен.");
        }
    }
}
