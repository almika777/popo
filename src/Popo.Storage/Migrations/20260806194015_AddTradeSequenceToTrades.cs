using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeSequenceToTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TradeSequence",
                table: "Trades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                WITH ordered AS (
                    SELECT "TradeId",
                           ROW_NUMBER() OVER (
                               PARTITION BY "SecId", "BoardId", "TradeDate"
                               ORDER BY "TradeId")::integer AS "Sequence"
                    FROM "Trades"
                )
                UPDATE "Trades" AS trades
                SET "TradeSequence" = ordered."Sequence"
                FROM ordered
                WHERE trades."TradeId" = ordered."TradeId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_SecId_BoardId_TradeDate_TradeSequence",
                table: "Trades",
                columns: new[] { "SecId", "BoardId", "TradeDate", "TradeSequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trades_SecId_BoardId_TradeDate_TradeSequence",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "TradeSequence",
                table: "Trades");
        }
    }
}
