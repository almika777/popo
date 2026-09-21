using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyVolumeStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyVolumeStatistics",
                columns: table => new
                {
                    SecId = table.Column<string>(type: "text", nullable: false),
                    Average = table.Column<double>(type: "double precision", nullable: false),
                    Median = table.Column<double>(type: "double precision", nullable: false),
                    HistoryThroughDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyVolumeStatistics", x => x.SecId);
                });

            migrationBuilder.Sql("""
                INSERT INTO "DailyVolumeStatistics" ("SecId", "Average", "Median", "HistoryThroughDate", "CalculatedAt")
                WITH daily_volume AS (
                    SELECT "SecId", "TradeDate", SUM("Volume") AS "Volume"
                    FROM "MoexHistoryYieldsEntities"
                    WHERE "Volume" IS NOT NULL
                    GROUP BY "SecId", "TradeDate"
                ), ranked_volume AS (
                    SELECT "SecId", "TradeDate", "Volume",
                           ROW_NUMBER() OVER (PARTITION BY "SecId" ORDER BY "TradeDate" DESC) AS row_number
                    FROM daily_volume
                )
                SELECT "SecId", AVG("Volume"),
                       percentile_cont(0.5) WITHIN GROUP (ORDER BY "Volume"),
                       MAX("TradeDate"), statement_timestamp()
                FROM ranked_volume
                WHERE row_number <= 20
                GROUP BY "SecId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyVolumeStatistics");
        }
    }
}
