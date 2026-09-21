using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyMarketFundOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoneyMarketFundOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SecId = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    Price = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    Commission = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyMarketFundOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyMarketFundOperations_Date_SecId",
                table: "MoneyMarketFundOperations",
                columns: new[] { "Date", "SecId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoneyMarketFundOperations");
        }
    }
}
