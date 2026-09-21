using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioValuationTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
