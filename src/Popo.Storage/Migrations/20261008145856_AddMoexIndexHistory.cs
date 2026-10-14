using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddMoexIndexHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoexIndexHistories",
                columns: table => new
                {
                    SecId = table.Column<string>(type: "text", nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Close = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoexIndexHistories", x => new { x.SecId, x.TradeDate });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoexIndexHistories");
        }
    }
}
