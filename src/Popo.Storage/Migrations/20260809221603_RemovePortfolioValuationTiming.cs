using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class RemovePortfolioValuationTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "PortfolioValuations" AS duplicate
                USING "PortfolioValuations" AS canonical
                WHERE duplicate."Date" = canonical."Date"
                  AND duplicate."Timing" = 1
                  AND canonical."Timing" = 0;
                """);

            migrationBuilder.DropIndex(
                name: "IX_PortfolioValuations_Date_Timing",
                table: "PortfolioValuations");

            migrationBuilder.DropColumn(
                name: "Timing",
                table: "PortfolioValuations");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioValuations_Date",
                table: "PortfolioValuations",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioValuations_Date",
                table: "PortfolioValuations");

            migrationBuilder.AddColumn<int>(
                name: "Timing",
                table: "PortfolioValuations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioValuations_Date_Timing",
                table: "PortfolioValuations",
                columns: new[] { "Date", "Timing" },
                unique: true);
        }
    }
}
