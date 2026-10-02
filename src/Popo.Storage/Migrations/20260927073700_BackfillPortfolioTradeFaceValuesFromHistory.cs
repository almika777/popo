using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPortfolioTradeFaceValuesFromHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "PortfolioTrades" AS trade
                SET "FaceValue" = history."FaceValue"
                FROM "MoexHistoryYieldsEntities" AS history
                WHERE history."SecId" = trade."SecId"
                  AND history."BoardId" = trade."BoardId"
                  AND history."TradeDate" = trade."TradeDate"
                  AND history."FaceValue" > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
