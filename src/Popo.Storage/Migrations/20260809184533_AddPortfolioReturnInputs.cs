using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioReturnInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioCashFlows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioCashFlows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioValuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalValue = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioValuations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCashFlows_Date",
                table: "PortfolioCashFlows",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioValuations_Date",
                table: "PortfolioValuations",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioCashFlows");

            migrationBuilder.DropTable(
                name: "PortfolioValuations");
        }
    }
}
